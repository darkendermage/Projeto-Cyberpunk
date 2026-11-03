using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>A player puts their own token on the live scene; the GM starts a fight from the tokens on the map.</summary>
[Collection(ApiCollection.Name)]
public class TablePlayerTokensAndFightsApiTests(ApiFactory factory)
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

    private Task<SceneView> Gm(HttpMethod method, string url, object? body = null, HttpStatusCode expected = HttpStatusCode.OK) =>
        Send<SceneView>(_gm, method, url, body, expected: expected);

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

    private async Task<SceneView> LiveScene(string name)
    {
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest(name), HttpStatusCode.Created);
        return await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");
    }

    private Task<SeatDto> NewSeat(string label, int? characterId = null) =>
        Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest(label, characterId), expected: HttpStatusCode.Created);

    // ------------------------------------------------------------------ the player's own token

    [Fact]
    public async Task Player_places_their_character_token_once()
    {
        var solo = await FinalizedSolo("Placer");
        var seat = await NewSeat("Jogadora do Placer", solo.Id);
        var scene = await LiveScene("Rua do Placer");

        var view = await Send<SceneView>(_player, HttpMethod.Post, "/api/table/play/my-token", new PlaceTokenRequest(3, 4), seat.Token);

        var token = view.Tokens.Single(t => t.CharacterId == solo.Id);
        token.Should().Match<TokenView>(t => t.X == 3 && t.Y == 4 && t.OwnerSeatId == seat.Id && t.Controllable && t.Visibility == TokenVisibility.All);
        token.Label.Should().Be("Placer");
        token.Color.Should().Be(seat.Color);
        (await Gm(HttpMethod.Get, $"/api/table/scenes/{scene.Id}")).Tokens.Should().ContainSingle(t => t.CharacterId == solo.Id);
        await Send<JsonNode>(_player, HttpMethod.Post, "/api/table/play/my-token", new PlaceTokenRequest(5, 5), seat.Token, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_seat_without_a_character_places_a_token_with_its_own_name()
    {
        var seat = await NewSeat("Convidada");
        await LiveScene("Sem ficha");

        var view = await Send<SceneView>(_player, HttpMethod.Post, "/api/table/play/my-token", new PlaceTokenRequest(1, 1), seat.Token);

        view.Tokens.Should().ContainSingle(t => t.OwnerSeatId == seat.Id && t.Label == "Convidada" && t.CharacterId == null);
    }

    [Fact]
    public async Task The_players_token_joins_the_linked_fight()
    {
        var solo = await FinalizedSolo("Lutador");
        var seat = await NewSeat("Jogador do Lutador", solo.Id);
        var e = await Send<JsonNode>(_gm, HttpMethod.Post, "/api/encounters", new { name = "Briga do Lutador" }, expected: HttpStatusCode.Created);
        var fightId = e["id"]!.GetValue<int>();
        e = await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{fightId}/combatants", new { version = e["version"]!.GetValue<int>(), side = "Ally", characterId = solo.Id });
        var combatantId = e["queue"]!.AsArray().Single(f => f!["characterId"]?.GetValue<int?>() == solo.Id)!["id"]!.GetValue<string>();
        var scene = await LiveScene("Bar do Lutador");
        await Gm(HttpMethod.Put, $"/api/table/scenes/{scene.Id}", new UpdateSceneRequest(scene.Version, scene.Name, scene.Grid, null, 0, 0, fightId));

        var view = await Send<SceneView>(_player, HttpMethod.Post, "/api/table/play/my-token", new PlaceTokenRequest(2, 2), seat.Token);

        view.Tokens.Single(t => t.CharacterId == solo.Id).CombatantId.Should().Be(combatantId);
    }

    [Fact]
    public async Task A_token_the_gm_made_for_the_character_counts_as_the_players()
    {
        var solo = await FinalizedSolo("Ja tem");
        var seat = await NewSeat("Jogador do Ja tem", solo.Id);
        var scene = await LiveScene("Já tem token");
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Ja tem", 1, 1, CharacterId: solo.Id));

        await Send<JsonNode>(_player, HttpMethod.Post, "/api/table/play/my-token", new PlaceTokenRequest(4, 4), seat.Token, HttpStatusCode.Conflict);
    }

    // ------------------------------------------------------------------ a fight from the map

    [Fact]
    public async Task Gm_starts_a_fight_from_the_tokens_on_the_map()
    {
        var solo = await FinalizedSolo("Da Mesa");
        var npc = await NpcPortraitsApiTests.SavedNpc(_gm, "Capanga da Mesa");
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Luta da Mesa"), HttpStatusCode.Created);
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Da Mesa", 1, 1, CharacterId: solo.Id));
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Capanga", 4, 1, NpcId: npc));
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Smasher", 5, 1, CompendiumNpcId: "adam-smasher"));
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Ganger", 6, 1));
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Barril", 7, 1));
        string Id(string label) => scene.Tokens.Single(t => t.Label == label).Id;

        var view = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/fight", new CreateFightRequest("Tiroteio na mesa",
        [
            new FightTokenEntry(Id("Da Mesa"), CombatSide.Ally), new FightTokenEntry(Id("Capanga"), CombatSide.Enemy),
            new FightTokenEntry(Id("Smasher"), CombatSide.Enemy), new FightTokenEntry(Id("Ganger"), CombatSide.Enemy),
        ]));

        view.EncounterId.Should().NotBeNull();
        var fight = await Send<EncounterDetailDto>(_gm, HttpMethod.Get, $"/api/encounters/{view.EncounterId}");
        fight.Name.Should().Be("Tiroteio na mesa");
        fight.Queue.Should().HaveCount(4);
        TokenView Token(string label) => view.Tokens.Single(t => t.Label == label);
        FighterView Fighter(string label) => fight.Queue.Single(f => f.Id == Token(label).CombatantId);
        Fighter("Da Mesa").Should().Match<FighterView>(f => f.Kind == CombatantKind.Pc && f.Side == CombatSide.Ally);
        Fighter("Capanga").Should().Match<FighterView>(f => f.Kind == CombatantKind.Npc && f.Side == CombatSide.Enemy && f.Name == "Capanga");
        Fighter("Smasher").Should().Match<FighterView>(f => f.Kind == CombatantKind.Npc && f.MaxHp > 0);
        Fighter("Ganger").Should().Match<FighterView>(f => f.Kind == CombatantKind.Generic && f.Name == "Ganger");
        Token("Barril").CombatantId.Should().BeNull();
    }

    [Fact]
    public async Task A_fight_needs_tokens_of_the_scene()
    {
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Luta vazia"), HttpStatusCode.Created);

        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/fight", new CreateFightRequest("Nada", []), HttpStatusCode.BadRequest);
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/fight", new CreateFightRequest("Fantasma", [new FightTokenEntry("T99", CombatSide.Enemy)]), HttpStatusCode.BadRequest);
        (await Send<List<EncounterSummaryDto>>(_gm, HttpMethod.Get, "/api/encounters")).Should().NotContain(e => e.Name == "Fantasma" || e.Name == "Nada");
    }
}
