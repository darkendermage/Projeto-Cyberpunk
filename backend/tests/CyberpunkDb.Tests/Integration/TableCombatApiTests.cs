using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace CyberpunkDb.Tests.Integration;

/// <summary>The fight linked to the active scene, live on the GM's and the players' screens.</summary>
[Collection(ApiCollection.Name)]
public class TableCombatApiTests(ApiFactory factory)
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

    /// <summary>A finalized Streetrat Solo, as in EncountersApiTests.</summary>
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

    private sealed record Setup(int EncounterId, int SceneId, SeatDto Seat, int CharacterId, string HiddenCombatantId);

    /// <summary>A Solo and two gangers on an active scene; one ganger's token is hidden from the players.</summary>
    private async Task<Setup> FightOnTheTable(string name)
    {
        var solo = await FinalizedSolo(name);
        var seat = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest($"Jogador de {name}", solo.Id), expected: HttpStatusCode.Created);
        var e = await Send<JsonNode>(_gm, HttpMethod.Post, "/api/encounters", new { name = $"Tiroteio de {name}" }, expected: HttpStatusCode.Created);
        var id = e["id"]!.GetValue<int>();
        e = await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{id}/combatants", new { version = e["version"]!.GetValue<int>(), side = "Ally", characterId = solo.Id });
        e = await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{id}/combatants", new { version = e["version"]!.GetValue<int>(), side = "Enemy", generic = new { name = "Ganger" }, count = 2 });

        var scene = await Send<SceneView>(_gm, HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest($"Rua de {name}"), expected: HttpStatusCode.Created);
        scene = await Send<SceneView>(_gm, HttpMethod.Put, $"/api/table/scenes/{scene.Id}", new UpdateSceneRequest(scene.Version, scene.Name, scene.Grid, null, 0, 0, id));
        scene = await Send<SceneView>(_gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/encounter-tokens");
        var hidden = scene.Tokens.Last();
        await Send<SceneView>(_gm, HttpMethod.Put, $"/api/table/scenes/{scene.Id}/tokens/{hidden.Id}",
            new TokenInput(hidden.Label, hidden.X, hidden.Y, CombatantId: hidden.CombatantId, Visibility: TokenVisibility.Gm));
        await Send<SceneView>(_gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");
        return new Setup(id, scene.Id, seat, solo.Id, hidden.CombatantId!);
    }

    [Fact]
    public async Task Players_see_the_fight_without_the_gms_numbers_or_hidden_enemies()
    {
        var s = await FightOnTheTable("Kaze");
        var fight = await Send<PlayerEncounterDto>(_player, HttpMethod.Get, "/api/table/play/encounter", seat: s.Seat.Token);

        fight.Queue.Should().HaveCount(2);
        fight.Queue.Should().NotContain(f => f.Id == s.HiddenCombatantId);
        var own = fight.Queue.Single(f => f.Own);
        own.CharacterId.Should().Be(s.CharacterId);
        own.Hp.Should().BeGreaterThan(0);
        own.Weapons.Should().NotBeNull();
        var ganger = fight.Queue.Single(f => !f.Own);
        ganger.Hp.Should().BeNull();
        ganger.MaxHp.Should().BeNull();
        ganger.Weapons.Should().BeNull();

        var raw = await _player.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/table/play/encounter") { Headers = { { "X-Seat", s.Seat.Token } } });
        (await raw.Content.ReadAsStringAsync()).Should().NotContain("\"log\"");
    }

    [Fact]
    public async Task Enemies_added_to_a_live_scene_stay_hidden_until_the_gm_shows_them()
    {
        var solo = await FinalizedSolo("Ambush");
        var seat = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Vítima", solo.Id), expected: HttpStatusCode.Created);
        var e = await Send<JsonNode>(_gm, HttpMethod.Post, "/api/encounters", new { name = "Emboscada ao vivo" }, expected: HttpStatusCode.Created);
        var id = e["id"]!.GetValue<int>();
        var scene = await Send<SceneView>(_gm, HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Rua ao vivo"), expected: HttpStatusCode.Created);
        scene = await Send<SceneView>(_gm, HttpMethod.Put, $"/api/table/scenes/{scene.Id}", new UpdateSceneRequest(scene.Version, scene.Name, scene.Grid, null, 0, 0, id));
        await Send<SceneView>(_gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");

        e = await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{id}/combatants", new { version = e["version"]!.GetValue<int>(), side = "Ally", characterId = solo.Id });
        await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{id}/combatants", new { version = e["version"]!.GetValue<int>(), side = "Enemy", generic = new { name = "Atirador" }, count = 2 });

        // Before any token: enemies don't show in the players' Initiative list.
        var fight = await Send<PlayerEncounterDto>(_player, HttpMethod.Get, "/api/table/play/encounter", seat: seat.Token);
        fight.Queue.Should().ContainSingle().Which.Own.Should().BeTrue();

        // Tokens created on the live scene: enemies come out hidden, allies visible.
        var placed = await Send<SceneView>(_gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/encounter-tokens");
        placed.Tokens.Where(t => t.Color == "#ff2a6d").Should().HaveCount(2).And.OnlyContain(t => t.Visibility == TokenVisibility.Gm);
        placed.Tokens.Single(t => t.CharacterId == solo.Id).Visibility.Should().Be(TokenVisibility.All);
        (await Send<PlayerStateDto>(_player, HttpMethod.Get, "/api/table/play/state", seat: seat.Token)).Scene!.Tokens.Should().ContainSingle();
    }

    [Fact]
    public async Task A_player_gets_their_own_skills_for_quick_rolls()
    {
        var solo = await FinalizedSolo("Lucky");
        var seat = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Dona da Lucky", solo.Id), expected: HttpStatusCode.Created);
        var sheet = await Send<PlayerCharacterDto>(_player, HttpMethod.Get, "/api/table/play/character", seat: seat.Token);
        sheet.Id.Should().Be(solo.Id);
        sheet.MaxHp.Should().BeGreaterThan(0);
        sheet.Skills.Should().Contain(s => s.SkillId == "handgun" && s.Total > 0);
        sheet.Stats.Should().Contain(s => s.Key == "REF");

        var loner = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Sem ficha"), expected: HttpStatusCode.Created);
        (await _player.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/table/play/character") { Headers = { { "X-Seat", loner.Token } } }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Every_combat_action_reaches_the_screens_live()
    {
        var s = await FightOnTheTable("Mox");
        await using var player = TableFoundationApiTests.Hub(factory, s.Seat.Token, playerPort: true);
        await using var gm = TableFoundationApiTests.Hub(factory, null, playerPort: false);
        var playerFights = new List<PlayerEncounterDto?>();
        var gmFights = new List<JsonElement>();
        var playerGot = new SemaphoreSlim(0);
        var gmGot = new SemaphoreSlim(0);
        player.On<PlayerEncounterDto?>("encounter", f => { lock (playerFights) playerFights.Add(f); playerGot.Release(); });
        gm.On<JsonElement>("encounter", f => { lock (gmFights) gmFights.Add(f); gmGot.Release(); });
        await player.StartAsync();
        await gm.StartAsync();
        await Task.Delay(300);

        var e = await Send<JsonNode>(_gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}");
        await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/initiative", new { version = e["version"]!.GetValue<int>() });

        (await playerGot.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();
        (await gmGot.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();
        lock (playerFights)
        {
            var seen = playerFights.Last()!;
            seen.Queue.Should().OnlyContain(f => f.Initiative != null);
            seen.Queue.Should().NotContain(f => f.Id == s.HiddenCombatantId);
        }
        lock (gmFights) gmFights.Last().GetProperty("queue").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task A_fight_off_the_players_screens_only_reaches_the_gm()
    {
        var s = await FightOnTheTable("Brick");
        var other = await Send<SceneView>(_gm, HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Intervalo"), expected: HttpStatusCode.Created);
        await Send<SceneView>(_gm, HttpMethod.Post, $"/api/table/scenes/{other.Id}/activate");

        await using var player = TableFoundationApiTests.Hub(factory, s.Seat.Token, playerPort: true);
        var got = 0;
        player.On<JsonElement>("encounter", _ => Interlocked.Increment(ref got));
        await player.StartAsync();

        var e = await Send<JsonNode>(_gm, HttpMethod.Get, $"/api/encounters/{s.EncounterId}");
        await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{s.EncounterId}/initiative", new { version = e["version"]!.GetValue<int>() });
        await Task.Delay(500);
        got.Should().Be(0);
        (await _player.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/table/play/encounter") { Headers = { { "X-Seat", s.Seat.Token } } }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
