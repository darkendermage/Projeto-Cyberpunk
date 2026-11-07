using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>Attacks from the cards under the map: the player's own on their Turn, the GM's with anyone, and the undo.</summary>
[Collection(ApiCollection.Name)]
public class TableQuickAttackApiTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _gm = factory.CreateClient();
    private readonly HttpClient _player = factory.CreatePlayerClient();

    private async Task<T> Send<T>(HttpClient client, HttpMethod method, string url, object? body = null, string? seat = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        if (seat is not null) request.Headers.Add("X-Seat", seat);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<CharacterDetailDto> FinalizedSolo(string name)
    {
        var c = await Send<CharacterDetailDto>(_gm, HttpMethod.Post, "/api/characters",
            new CreateCharacterRequest { Name = name, Method = CreationMethod.Streetrat, Role = "Solo", NativeLanguage = "English", Home = "Watson" },
            expected: HttpStatusCode.Created);
        c = await Send<CharacterDetailDto>(_gm, HttpMethod.Post, $"/api/characters/{c.Id}/creation/template-row", new TemplateRowRequest(c.Version, 6));
        c = await Send<CharacterDetailDto>(_gm, HttpMethod.Put, $"/api/characters/{c.Id}", new SaveSheetRequest(c.Version, c.Sheet with
        {
            Creation = c.Sheet.Creation with { StartingChoices = new Dictionary<string, int> { ["weapons:2"] = 0, ["cyberware:2"] = 0 } },
        }, null));
        return await Send<CharacterDetailDto>(_gm, HttpMethod.Post, $"/api/characters/{c.Id}/creation/finalize", new VersionRequest(c.Version));
    }

    private sealed record Setup(int EncounterId, int SceneId, SeatDto Seat, int CharacterId, string SoloId, string GangerId);

    /// <summary>A Solo and a ganger on the live scene, both tokens visible; Initiative rolled.</summary>
    private async Task<Setup> FightOnTheTable(string name)
    {
        var solo = await FinalizedSolo(name);
        var seat = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest($"Jogador de {name}", solo.Id), expected: HttpStatusCode.Created);
        var e = await Send<JsonNode>(_gm, HttpMethod.Post, "/api/encounters", new { name = $"Tiroteio de {name}" }, expected: HttpStatusCode.Created);
        var id = e["id"]!.GetValue<int>();
        e = await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{id}/combatants", new { version = V(e), side = "Ally", characterId = solo.Id });
        e = await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{id}/combatants",
            new { version = V(e), side = "Enemy", generic = new { name = "Ganger", attackBase = 10, damage = "2d6", weaponClass = "MediumPistol" } });
        e = await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{id}/initiative", new { version = V(e) });

        var scene = await Send<SceneView>(_gm, HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest($"Rua de {name}"), expected: HttpStatusCode.Created);
        scene = await Send<SceneView>(_gm, HttpMethod.Put, $"/api/table/scenes/{scene.Id}", new UpdateSceneRequest(scene.Version, scene.Name, scene.Grid, null, 0, 0, id));
        scene = await Send<SceneView>(_gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/encounter-tokens");
        await Send<SceneView>(_gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/batch", new SceneBatchRequest(ShowTokens: scene.Tokens.Select(t => t.Id).ToList()));
        await Send<SceneView>(_gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");

        var queue = e["queue"]!.AsArray();
        string IdOf(string kind) => queue.First(f => f!["kind"]!.GetValue<string>() == kind)!["id"]!.GetValue<string>();
        return new Setup(id, scene.Id, seat, solo.Id, IdOf("Pc"), IdOf("Generic"));
    }

    private static int V(JsonNode e) => e["version"]!.GetValue<int>();

    private async Task<JsonNode> Fight(int id) => await Send<JsonNode>(_gm, HttpMethod.Get, $"/api/encounters/{id}");

    private async Task SetActive(Setup s, string combatantId) =>
        await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/combatants/{combatantId}/active", new { version = V(await Fight(s.EncounterId)) });

    private async Task<string> GunOf(Setup s)
    {
        var fight = await Send<PlayerEncounterDto>(_player, HttpMethod.Get, "/api/table/play/encounter", seat: s.Seat.Token);
        var gun = fight.Queue.Single(f => f.Own).Weapons!.First(w => !w.Melee).Key;
        // Starting weapons come unloaded.
        var sheet = await Send<CharacterDetailDto>(_gm, HttpMethod.Get, $"/api/characters/{s.CharacterId}");
        await Send<CharacterDetailDto>(_gm, HttpMethod.Post, $"/api/characters/{s.CharacterId}/weapons/reload", new ReloadRequest(sheet.Version, gun));
        return gun;
    }

    private static int RoundsOf(JsonNode fight, string id, string weapon) =>
        fight["queue"]!.AsArray().First(f => f!["id"]!.GetValue<string>() == id)!["weapons"]!.AsArray()
            .First(w => w!["key"]!.GetValue<string>() == weapon)!["rounds"]!.GetValue<int>();

    private static int HpOf(JsonNode fight, string id) => fight["queue"]!.AsArray().First(f => f!["id"]!.GetValue<string>() == id)!["hp"]!.GetValue<int>();

    [Fact]
    public async Task A_player_attacks_on_their_turn_and_the_fight_takes_the_damage()
    {
        var s = await FightOnTheTable("Gatilho");
        await SetActive(s, s.SoloId);
        var gun = await GunOf(s);
        var before = HpOf(await Fight(s.EncounterId), s.GangerId);

        TableAttack? hit = null;
        for (var i = 0; i < 8 && hit is null; i++)
        {
            var attack = await Send<TableAttack>(_player, HttpMethod.Post, "/api/table/play/attack",
                new QuickAttackRequest(gun, AttackMode.Single, [s.GangerId]), s.Seat.Token);
            attack.AttackerId.Should().Be(s.SoloId);
            attack.Seq.Should().Be(i + 1);
            var target = attack.Targets.Single();
            // Everyone reads the roll, the DV and the damage dealt (C5); only how much went into an NPC's HP stays hidden.
            target.Dv.Should().NotBeNull();
            target.Through.Should().BeNull();
            if (target.Hit)
            {
                target.Damage.Should().NotBeNull();
                target.DamageDice.Should().NotBeEmpty();
                hit = attack;
            }
        }

        hit.Should().NotBeNull("eight shots at close range should land at least once");
        HpOf(await Fight(s.EncounterId), s.GangerId).Should().BeLessThan(before);

        // The chat carries the attack: the GM with every number, the player without the NPC's HP.
        var gmLine = (await Send<List<TableMessageDto>>(_gm, HttpMethod.Get, "/api/table/messages")).Last(m => m.Kind == TableMessageKind.Attack);
        gmLine.Attack!.Targets.Single().Dv.Should().NotBeNull();
        gmLine.AuthorSeatId.Should().Be(s.Seat.Id);
        var playerLine = (await Send<List<TableMessageDto>>(_player, HttpMethod.Get, "/api/table/play/messages", seat: s.Seat.Token)).Last(m => m.Kind == TableMessageKind.Attack);
        playerLine.Attack!.Targets.Single().Dv.Should().NotBeNull();
        playerLine.Attack.Targets.Single().Through.Should().BeNull();
        playerLine.Attack.Seq.Should().Be(gmLine.Attack.Seq);
    }

    [Fact]
    public async Task A_player_cannot_attack_out_of_turn()
    {
        var s = await FightOnTheTable("Apressado");
        await SetActive(s, s.GangerId);
        var gun = await GunOf(s);
        await Send<JsonNode>(_player, HttpMethod.Post, "/api/table/play/attack", new QuickAttackRequest(gun, AttackMode.Single, [s.GangerId]), s.Seat.Token, HttpStatusCode.Conflict);
        await Send<JsonNode>(_player, HttpMethod.Post, "/api/table/play/attack", new QuickAttackRequest(gun, AttackMode.Explosive, [s.GangerId]), s.Seat.Token, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_gm_attacks_from_the_map_and_undoes_it()
    {
        var s = await FightOnTheTable("Desfeito");
        var gun = await GunOf(s);
        var hpBefore = HpOf(await Fight(s.EncounterId), s.GangerId);
        var ammoBefore = RoundsOf(await Fight(s.EncounterId), s.SoloId, gun);

        // The GM may attack with anyone, whoever's Turn it is.
        var attack = await Send<TableAttack>(_gm, HttpMethod.Post, $"/api/table/scenes/{s.SceneId}/attack",
            new GmQuickAttackRequest(s.SoloId, gun, AttackMode.Single, [s.GangerId]));
        attack.Targets.Single().Dv.Should().NotBeNull();
        RoundsOf(await Fight(s.EncounterId), s.SoloId, gun).Should().Be(ammoBefore - 1);

        var undone = await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/undo-attack", new { version = V(await Fight(s.EncounterId)) });
        undone["undoneAttack"]!.GetValue<int>().Should().Be(attack.Seq);
        HpOf(undone, s.GangerId).Should().Be(hpBefore);
        RoundsOf(await Fight(s.EncounterId), s.SoloId, gun).Should().Be(ammoBefore);

        var line = (await Send<List<TableMessageDto>>(_gm, HttpMethod.Get, "/api/table/messages")).Last(m => m.Kind == TableMessageKind.Attack);
        line.Attack!.Undone.Should().BeTrue();

        // Only once, and only right after the attack.
        await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/undo-attack", new { version = V(await Fight(s.EncounterId)) }, expected: HttpStatusCode.Conflict);
        await Send<TableAttack>(_gm, HttpMethod.Post, $"/api/table/scenes/{s.SceneId}/attack", new GmQuickAttackRequest(s.SoloId, gun, AttackMode.Single, [s.GangerId]));
        await SetActive(s, s.GangerId);
        await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/undo-attack", new { version = V(await Fight(s.EncounterId)) }, expected: HttpStatusCode.Conflict);
    }
}
