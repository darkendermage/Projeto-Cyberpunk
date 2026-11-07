using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Compendium;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CyberpunkDb.Tests.Integration;

/// <summary>Tokens of saved and compendium NPCs show the NPC's image, to the GM and to the players who see the token.</summary>
[Collection(ApiCollection.Name)]
public class TableTokenImagesApiTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static readonly byte[] OtherPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
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

    private async Task<(SceneView Scene, SeatDto Seat)> LiveScene(string name)
    {
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest(name), HttpStatusCode.Created);
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");
        var seat = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest($"Jogador de {name}"), expected: HttpStatusCode.Created);
        return (scene, seat);
    }

    private async Task<int> NpcWithImage(string name)
    {
        var id = await NpcPortraitsApiTests.SavedNpc(_gm, name);
        (await NpcPortraitsApiTests.PutImage(_gm, $"/api/npcs/{id}/portrait", TableFoundationApiTests.Png)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return id;
    }

    private Task<PlayerStateDto> PlayerState(SeatDto seat) => Send<PlayerStateDto>(_player, HttpMethod.Get, "/api/table/play/state", seat: seat.Token);

    [Fact]
    public async Task Saved_npc_token_shows_its_image_to_gm_and_player()
    {
        var npc = await NpcWithImage("Royce de Teste");
        var (scene, seat) = await LiveScene("Imagens de NPC");

        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Royce", 1, 1, NpcId: npc));
        var gmToken = scene.Tokens.Single(t => t.Label == "Royce");
        gmToken.NpcId.Should().Be(npc);
        gmToken.ImageUrl.Should().StartWith($"/api/npcs/{npc}/portrait?v=");

        var playerToken = (await PlayerState(seat)).Scene!.Tokens.Single(t => t.Label == "Royce");
        playerToken.ImageUrl.Should().StartWith("/api/table/play/tokens/");
        var image = await _player.GetAsync(playerToken.ImageUrl);
        image.StatusCode.Should().Be(HttpStatusCode.OK);
        (await image.Content.ReadAsByteArrayAsync()).Should().Equal(TableFoundationApiTests.Png);
    }

    [Fact]
    public async Task Compendium_fight_token_shows_the_book_npc_image()
    {
        (await NpcPortraitsApiTests.PutImage(_gm, "/api/npc-compendium/adam-smasher/portrait", TableFoundationApiTests.Png)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var e = await Send<JsonNode>(_gm, HttpMethod.Post, "/api/encounters", new { name = "Smasher na mesa" }, expected: HttpStatusCode.Created);
        var fight = e["id"]!.GetValue<int>();
        await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{fight}/combatants", new { version = e["version"]!.GetValue<int>(), side = "Enemy", compendiumNpcId = "adam-smasher" });
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Arasaka Tower"), HttpStatusCode.Created);
        scene = await Gm(HttpMethod.Put, $"/api/table/scenes/{scene.Id}", new UpdateSceneRequest(scene.Version, scene.Name, scene.Grid, null, 0, 0, fight));

        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/encounter-tokens");

        var token = scene.Tokens.Single();
        token.CompendiumNpcId.Should().Be("adam-smasher");
        token.ImageUrl.Should().StartWith("/api/npc-compendium/adam-smasher/portrait?v=");
    }

    [Fact]
    public async Task Hidden_token_image_is_404_for_players()
    {
        var npc = await NpcWithImage("Oculto");
        var (scene, seat) = await LiveScene("Emboscada com imagem");
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Oculto", 1, 1, NpcId: npc, Visibility: TokenVisibility.Gm));
        var hidden = scene.Tokens.Single(t => t.Label == "Oculto");

        var response = await _player.GetAsync($"/api/table/play/tokens/{hidden.Id}/portrait?seat={seat.Token}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Token_media_wins_over_npc_image()
    {
        var npc = await NpcWithImage("Com mídia");
        var media = await (await _gm.PostAsync("/api/table/media", new MultipartFormDataContent
        {
            { new ByteArrayContent(OtherPng) { Headers = { ContentType = new("image/png") } }, "file", "t.png" },
        })).Content.ReadFromJsonAsync<MediaDto>();
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Mídia primeiro"), HttpStatusCode.Created);

        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Com mídia", 1, 1, ImageMediaId: media!.Id, NpcId: npc));

        scene.Tokens.Single().ImageUrl.Should().StartWith("/api/table/media/");
    }

    [Fact]
    public async Task Two_sources_are_rejected()
    {
        var npc = await NpcWithImage("Duplo");
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Duas origens"), HttpStatusCode.Created);

        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Duplo", 1, 1, NpcId: npc, CompendiumNpcId: "adam-smasher"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_npc_is_rejected_but_a_deleted_one_keeps_working()
    {
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("NPC sumido"), HttpStatusCode.Created);
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Fantasma", 1, 1, NpcId: 999999), HttpStatusCode.BadRequest);
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Fantasma", 1, 1, CompendiumNpcId: "nao-existe"), HttpStatusCode.BadRequest);

        var npc = await NpcWithImage("Vai sumir");
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Vai sumir", 1, 1, NpcId: npc));
        var token = scene.Tokens.Single();
        (await _gm.DeleteAsync($"/api/npcs/{npc}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        scene = await Gm(HttpMethod.Put, $"/api/table/scenes/{scene.Id}/tokens/{token.Id}", new TokenInput("Sumiu", 2, 2, NpcId: npc));

        var updated = scene.Tokens.Single();
        updated.Label.Should().Be("Sumiu");
        updated.NpcId.Should().Be(npc);
        updated.ImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task Updating_keeps_the_source()
    {
        var npc = await NpcWithImage("Fica");
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Origem fica"), HttpStatusCode.Created);
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Fica", 1, 1, NpcId: npc));

        scene = await Gm(HttpMethod.Put, $"/api/table/scenes/{scene.Id}/tokens/{scene.Tokens.Single().Id}", new TokenInput("Ficou", 3, 3, NpcId: npc));

        scene.Tokens.Single().NpcId.Should().Be(npc);
    }

    [Fact]
    public async Task Changing_the_image_sends_the_scene_again()
    {
        var npc = await NpcWithImage("Ao vivo");
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Troca ao vivo"), HttpStatusCode.Created);
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Ao vivo", 1, 1, NpcId: npc));
        var before = scene.Tokens.Single().ImageUrl;
        await using var gm = TableFoundationApiTests.Hub(factory, null, playerPort: false);
        var pushed = new TaskCompletionSource<SceneView>(TaskCreationOptions.RunContinuationsAsynchronously);
        gm.On<SceneView>("scene", v => { if (v.Id == scene.Id) pushed.TrySetResult(v); });
        await gm.StartAsync();
        await Task.Delay(20);

        await NpcPortraitsApiTests.PutImage(_gm, $"/api/npcs/{npc}/portrait", OtherPng);

        var view = await pushed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        view.Tokens.Single().ImageUrl.Should().NotBe(before);
    }

    /// <summary>Records every SQL query a context runs.</summary>
    private sealed class SqlLog : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            lock (Commands) Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, ct);
        }
    }

    [Fact]
    public async Task Scene_views_and_stamps_read_image_dates_without_the_image_bytes()
    {
        var npc = await NpcWithImage("Sem bytes");
        (await NpcPortraitsApiTests.PutImage(_gm, "/api/npc-compendium/adam-smasher/portrait", TableFoundationApiTests.Png)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Sem bytes"), HttpStatusCode.Created);
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Sem bytes", 1, 1, NpcId: npc));
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Smasher", 2, 1, CompendiumNpcId: "adam-smasher"));
        var log = new SqlLog();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={factory.DbPath};Pooling=False").AddInterceptors(log).Options);
        var compendium = factory.Services.GetRequiredService<NpcCompendium>();
        var scenes = new SceneService(db, new SceneLocks(), new NullTableNotifier(), compendium, new TableSessionService(db, new NullTableNotifier()));
        var portraits = new NpcPortraitService(db, compendium, scenes);

        var view = await scenes.GetAsync(scene.Id, default);
        await portraits.SavedStampsAsync(default);
        await portraits.CompendiumStampsAsync(default);

        view.Tokens.Should().OnlyContain(t => t.ImageUrl != null);
        var imageQueries = log.Commands.Where(c => c.Contains("Portraits")).ToList();
        imageQueries.Should().HaveCountGreaterThanOrEqualTo(4);
        imageQueries.Should().NotContain(c => c.Contains(".\"Data\""), "a scene or a list only needs when each image changed, never its bytes");
    }

    [Fact]
    public async Task Changing_the_image_changes_the_url()
    {
        var npc = await NpcWithImage("Troca");
        var scene = await Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest("Imagem nova"), HttpStatusCode.Created);
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Troca", 1, 1, NpcId: npc));
        var before = scene.Tokens.Single().ImageUrl;
        await Task.Delay(20);

        (await NpcPortraitsApiTests.PutImage(_gm, $"/api/npcs/{npc}/portrait", OtherPng)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = (await Gm(HttpMethod.Get, $"/api/table/scenes/{scene.Id}")).Tokens.Single().ImageUrl;
        after.Should().NotBeNull().And.NotBe(before);
    }
}
