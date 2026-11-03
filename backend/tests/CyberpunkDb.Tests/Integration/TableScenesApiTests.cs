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

/// <summary>Scenes, tokens and areas of the virtual table, as the GM and the players use them.</summary>
[Collection(ApiCollection.Name)]
public class TableScenesApiTests(ApiFactory factory)
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

    private async Task<HttpStatusCode> Status(HttpClient client, HttpMethod method, string url, object? body = null, string? seat = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        if (seat is not null) request.Headers.Add("X-Seat", seat);
        return (await client.SendAsync(request)).StatusCode;
    }

    private Task<SceneView> NewScene(string name) => Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest(name), HttpStatusCode.Created);

    private Task<SeatDto> NewSeat(string label, int? characterId = null) =>
        Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest(label, characterId), expected: HttpStatusCode.Created);

    private async Task<int> NewCharacter(string name)
    {
        var c = await Send<CharacterDetailDto>(_gm, HttpMethod.Post, "/api/characters",
            new CreateCharacterRequest { Name = name, Method = CreationMethod.Streetrat, Role = "Solo", NativeLanguage = "English", Home = "Watson" },
            expected: HttpStatusCode.Created);
        return c.Id;
    }

    private static TokenView Token(SceneView scene, string label) => scene.Tokens.Single(t => t.Label == label);

    [Fact]
    public async Task The_gm_prepares_a_scene_and_edits_carry_the_version()
    {
        var scene = await NewScene("Afterlife");
        var grid = new SceneGrid { SizePx = 64, MetersPerCell = 2, OffsetX = 3 };
        var updated = await Gm(HttpMethod.Put, $"/api/table/scenes/{scene.Id}",
            new UpdateSceneRequest(scene.Version, "Afterlife — porão", grid, null, 1920, 1080, null));
        updated.Name.Should().Be("Afterlife — porão");
        updated.Grid.SizePx.Should().Be(64);
        updated.Version.Should().Be(scene.Version + 1);

        await Gm(HttpMethod.Put, $"/api/table/scenes/{scene.Id}",
            new UpdateSceneRequest(scene.Version, "Velha", grid, null, 0, 0, null), HttpStatusCode.Conflict);
        await Gm(HttpMethod.Put, $"/api/table/scenes/{scene.Id}",
            new UpdateSceneRequest(updated.Version, "Grade ruim", grid with { SizePx = 2 }, null, 0, 0, null), HttpStatusCode.BadRequest);
        await Gm(HttpMethod.Put, $"/api/table/scenes/{scene.Id}",
            new UpdateSceneRequest(updated.Version, "Sem mapa", grid, Guid.NewGuid(), 10, 10, null), HttpStatusCode.BadRequest);

        // Token moves don't touch the version: a GM editing the grid is not refused because a player moved.
        var withToken = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Barril", 2, 3));
        var moved = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens/{Token(withToken, "Barril").Id}/move", new TokenMoveRequest(5.5, 1, 90));
        Token(moved, "Barril").Should().Match<TokenView>(t => t.X == 5.5 && t.Y == 1 && t.Rotation == 90);
        moved.Version.Should().Be(updated.Version);

        var copy = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/duplicate", null, HttpStatusCode.Created);
        copy.Active.Should().BeFalse();
        Token(copy, "Barril").X.Should().Be(5.5);

        var removed = await Gm(HttpMethod.Delete, $"/api/table/scenes/{scene.Id}/tokens/{Token(moved, "Barril").Id}");
        removed.Tokens.Should().BeEmpty();
        (await Status(_gm, HttpMethod.Delete, $"/api/table/scenes/{copy.Id}")).Should().Be(HttpStatusCode.NoContent);
        (await Status(_gm, HttpMethod.Get, $"/api/table/scenes/{copy.Id}")).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Players_see_and_move_only_what_is_theirs()
    {
        var characterId = await NewCharacter("V");
        var v = await NewSeat("Jogadora da V", characterId);
        var other = await NewSeat("Outro jogador");
        var scene = await NewScene("Beco");
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("V", 1, 1, CharacterId: characterId));
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Drone", 2, 1, OwnerSeatId: v.Id));
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Carro", 3, 1, Locked: true, OwnerSeatId: v.Id));
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Ganger", 4, 1));
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Emboscada", 5, 1, Visibility: TokenVisibility.Gm));
        var gmView = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens",
            new TokenInput("Bilhete", 6, 1, Visibility: TokenVisibility.Seats, VisibleSeatIds: [other.Id]));
        gmView.Tokens.Should().HaveCount(6);
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");

        var state = await Send<PlayerStateDto>(_player, HttpMethod.Get, "/api/table/play/state", seat: v.Token);
        state.Seat.CharacterId.Should().Be(characterId);
        state.Scene!.Tokens.Select(t => t.Label).Should().BeEquivalentTo("V", "Drone", "Carro", "Ganger");
        state.Scene.Tokens.Where(t => t.Controllable).Select(t => t.Label).Should().BeEquivalentTo("V", "Drone");
        (await Send<PlayerStateDto>(_player, HttpMethod.Get, "/api/table/play/state", seat: other.Token))
            .Scene!.Tokens.Select(t => t.Label).Should().Contain("Bilhete").And.NotContain("Emboscada");

        var moved = await Send<SceneView>(_player, HttpMethod.Post, $"/api/table/play/tokens/{Token(state.Scene, "V").Id}/move", new TokenMoveRequest(7, 8), v.Token);
        Token(moved, "V").Should().Match<TokenView>(t => t.X == 7 && t.Y == 8);
        (await Status(_player, HttpMethod.Post, $"/api/table/play/tokens/{Token(state.Scene, "Drone").Id}/move", new TokenMoveRequest(2, 2), v.Token))
            .Should().Be(HttpStatusCode.OK);

        (await Status(_player, HttpMethod.Post, $"/api/table/play/tokens/{Token(state.Scene, "Ganger").Id}/move", new TokenMoveRequest(0, 0), v.Token))
            .Should().Be(HttpStatusCode.BadRequest);
        (await Status(_player, HttpMethod.Post, $"/api/table/play/tokens/{Token(state.Scene, "Carro").Id}/move", new TokenMoveRequest(0, 0), v.Token))
            .Should().Be(HttpStatusCode.BadRequest);
        (await Status(_player, HttpMethod.Post, $"/api/table/play/tokens/{Token(gmView, "Emboscada").Id}/move", new TokenMoveRequest(0, 0), v.Token))
            .Should().Be(HttpStatusCode.NotFound);
        (await Status(_player, HttpMethod.Post, $"/api/table/play/tokens/{Token(state.Scene, "V").Id}/move", new TokenMoveRequest(0, 0), other.Token))
            .Should().Be(HttpStatusCode.BadRequest);
        (await Status(_player, HttpMethod.Post, $"/api/table/play/tokens/{Token(state.Scene, "V").Id}/move", new TokenMoveRequest(double.MaxValue, 0), v.Token))
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Players_place_and_remove_only_their_own_areas()
    {
        var seat = await NewSeat("Granadeiro");
        var scene = await NewScene("Pátio");
        var withGmArea = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/templates", new TemplateInput(TemplateShape.Square, 2, 2, 10, Label: "Fumaça"));
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");

        var placed = await Send<SceneView>(_player, HttpMethod.Post, "/api/table/play/templates", new TemplateInput(TemplateShape.Circle, 4, 4, 5, Label: "Granada"), seat.Token);
        var mine = placed.Templates.Single(t => t.Label == "Granada");
        mine.OwnerSeatId.Should().Be(seat.Id);

        var gmArea = withGmArea.Templates.Single();
        (await Status(_player, HttpMethod.Delete, $"/api/table/play/templates/{gmArea.Id}", seat: seat.Token)).Should().Be(HttpStatusCode.BadRequest);
        (await Status(_player, HttpMethod.Put, $"/api/table/play/templates/{mine.Id}", new TemplateInput(TemplateShape.Cone, 4, 4, 6, 45), seat.Token))
            .Should().Be(HttpStatusCode.OK);
        var after = await Send<SceneView>(_player, HttpMethod.Delete, $"/api/table/play/templates/{mine.Id}", seat: seat.Token);
        after.Templates.Should().ContainSingle(t => t.Id == gmArea.Id);
    }

    [Fact]
    public async Task A_linked_fight_puts_one_token_per_combatant_on_the_map()
    {
        var encounter = await Send<JsonNode>(_gm, HttpMethod.Post, "/api/encounters", new { name = "Emboscada no beco" }, expected: HttpStatusCode.Created);
        var id = encounter["id"]!.GetValue<int>();
        await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{id}/combatants",
            new { version = encounter["version"]!.GetValue<int>(), side = "Enemy", generic = new { name = "Ganger" }, count = 3 });

        var scene = await NewScene("Beco escuro");
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/encounter-tokens", null, HttpStatusCode.BadRequest);
        var linked = await Gm(HttpMethod.Put, $"/api/table/scenes/{scene.Id}",
            new UpdateSceneRequest(scene.Version, scene.Name, scene.Grid, null, 0, 0, id));
        var placed = await Gm(HttpMethod.Post, $"/api/table/scenes/{linked.Id}/encounter-tokens");
        placed.Tokens.Should().HaveCount(3).And.OnlyContain(t => t.CombatantId != null && t.Color == "#ff2a6d");
        placed.Tokens.Select(t => t.X).Should().OnlyHaveUniqueItems();

        // Running it again only adds combatants that have no token yet.
        (await Gm(HttpMethod.Post, $"/api/table/scenes/{linked.Id}/encounter-tokens")).Tokens.Should().HaveCount(3);
    }

    [Fact]
    public async Task Deleting_the_linked_fight_unlinks_the_scene_and_keeps_it_editable()
    {
        var encounter = await Send<JsonNode>(_gm, HttpMethod.Post, "/api/encounters", new { name = "Luta descartada" }, expected: HttpStatusCode.Created);
        var id = encounter["id"]!.GetValue<int>();
        var scene = await NewScene("Depois da luta");
        scene = await Gm(HttpMethod.Put, $"/api/table/scenes/{scene.Id}", new UpdateSceneRequest(scene.Version, scene.Name, scene.Grid, null, 0, 0, id));

        (await Status(_gm, HttpMethod.Delete, $"/api/encounters/{id}")).Should().Be(HttpStatusCode.NoContent);
        var after = await Gm(HttpMethod.Get, $"/api/table/scenes/{scene.Id}");
        after.EncounterId.Should().BeNull();
        await Gm(HttpMethod.Put, $"/api/table/scenes/{scene.Id}", new UpdateSceneRequest(after.Version, "Renomeada", after.Grid, null, 0, 0, null));
    }

    [Fact]
    public async Task Changes_reach_the_screens_at_once_and_hidden_tokens_never_reach_players()
    {
        var seat = await NewSeat("Netrunner");
        var scene = await NewScene("Arasaka Tower");
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");

        await using var player = TableFoundationApiTests.Hub(factory, seat.Token, playerPort: true);
        var views = new List<SceneView?>();
        var got = new SemaphoreSlim(0);
        player.On<SceneView?>("scene", v => { lock (views) views.Add(v); got.Release(); });
        await using var gm = TableFoundationApiTests.Hub(factory, null, playerPort: false);
        var gmViews = new List<SceneView>();
        var gmGot = new SemaphoreSlim(0);
        gm.On<SceneView>("scene", v => { lock (gmViews) gmViews.Add(v); gmGot.Release(); });
        await player.StartAsync();
        await gm.StartAsync();
        await Task.Delay(300);

        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Guarda", 1, 1));
        (await got.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();
        (await gmGot.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Torre", 2, 2, Visibility: TokenVisibility.Gm));
        (await got.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();
        (await gmGot.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();

        lock (views) views.Last()!.Tokens.Select(t => t.Label).Should().Equal("Guarda");
        lock (gmViews) gmViews.Last().Tokens.Select(t => t.Label).Should().Equal("Guarda", "Torre");

        // Switching scenes switches every screen; an inactive scene's edits stay with the GM.
        var other = await NewScene("Esgoto");
        await Gm(HttpMethod.Post, $"/api/table/scenes/{other.Id}/activate");
        (await got.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();
        lock (views) views.Last()!.Id.Should().Be(other.Id);
        var before = views.Count;
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Fora de cena", 3, 3));
        await Task.Delay(500);
        lock (views) views.Count.Should().Be(before);
    }

    [Fact]
    public async Task Pings_and_drag_previews_go_live_without_saving()
    {
        var seat = await NewSeat("Nomad");
        var scene = await NewScene("Badlands");
        var placed = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Moto", 1, 1, OwnerSeatId: seat.Id));
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Bando", 4, 4));
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");

        await using var player = TableFoundationApiTests.Hub(factory, seat.Token, playerPort: true);
        await using var gm = TableFoundationApiTests.Hub(factory, null, playerPort: false);
        var pings = new TaskCompletionSource<JsonElement>();
        var drags = new TaskCompletionSource<JsonElement>();
        gm.On<JsonElement>("ping", p => pings.TrySetResult(p));
        gm.On<JsonElement>("tokenDrag", d => drags.TrySetResult(d));
        await player.StartAsync();
        await gm.StartAsync();

        await player.InvokeAsync("Ping", scene.Id, 3.5, 2.0);
        var ping = await pings.Task.WaitAsync(TimeSpan.FromSeconds(10));
        ping.GetProperty("label").GetString().Should().Be("Nomad");
        ping.GetProperty("color").GetString().Should().Be(seat.Color);
        ping.GetProperty("sceneId").GetInt32().Should().Be(scene.Id);

        // Someone else's token: no preview goes out.
        await player.InvokeAsync("DragToken", scene.Id, "T2", 9.0, 9.0);
        // A scene other than the live one: token ids repeat across scenes, so nothing goes out.
        await player.InvokeAsync("DragToken", scene.Id + 1000, Token(placed, "Moto").Id, 7.0, 7.0);
        await player.InvokeAsync("DragToken", scene.Id, Token(placed, "Moto").Id, 2.5, 1.5);
        var drag = await drags.Task.WaitAsync(TimeSpan.FromSeconds(10));
        drag.GetProperty("tokenId").GetString().Should().Be(Token(placed, "Moto").Id);
        drag.GetProperty("x").GetDouble().Should().Be(2.5);
        drag.GetProperty("sceneId").GetInt32().Should().Be(scene.Id);
        Token(await Gm(HttpMethod.Get, $"/api/table/scenes/{scene.Id}"), "Moto").X.Should().Be(1);
    }

    [Fact]
    public async Task Players_load_portraits_of_visible_character_tokens_with_their_link()
    {
        var characterId = await NewCharacter("Johnny");
        using (var upload = new MultipartFormDataContent { { new ByteArrayContent(TableFoundationApiTests.Png), "file", "j.png" } })
        {
            ((ByteArrayContent)upload.First()).Headers.ContentType = new("image/png");
            (await _gm.PutAsync($"/api/characters/{characterId}/portrait", upload)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        var seat = await NewSeat("Rockerboy", characterId);
        var scene = await NewScene("Palco");
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Johnny", 1, 1, CharacterId: characterId));
        var gmView = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");
        Token(gmView, "Johnny").ImageUrl.Should().Be($"/api/characters/{characterId}/portrait");

        var state = await Send<PlayerStateDto>(_player, HttpMethod.Get, "/api/table/play/state", seat: seat.Token);
        var url = Token(state.Scene!, "Johnny").ImageUrl!;
        url.Should().StartWith("/api/table/play/tokens/").And.Contain("seat=");
        var image = await _player.GetAsync(url);
        image.StatusCode.Should().Be(HttpStatusCode.OK);
        (await image.Content.ReadAsByteArrayAsync()).Should().Equal(TableFoundationApiTests.Png);
        (await _player.GetAsync(url.Split('?')[0])).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
