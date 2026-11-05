using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>Barriers from the book (CP:R p.182–183) on the scenery layer, and their link to the fight's cover.</summary>
[Collection(ApiCollection.Name)]
public class TableBarriersApiTests(ApiFactory factory)
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

    private Task<SceneView> NewScene(string name) => Gm(HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest(name), HttpStatusCode.Created);

    private Task<EncounterDetailDto> Fight(int id) => Send<EncounterDetailDto>(_gm, HttpMethod.Get, $"/api/encounters/{id}");

    [Fact]
    public async Task A_barrier_from_the_book_sits_on_the_scenery_layer_and_takes_damage()
    {
        var scene = await NewScene("Barreiras soltas");

        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/barriers", new BarrierRequest("Balcão", "wood", true, null, 2, 3, 3, 1));

        var barrier = scene.Tokens.Single();
        barrier.Layer.Should().Be(TokenLayer.Scenery);
        barrier.Barrier.Should().BeEquivalentTo(new { Material = "wood", Thick = true, MaxHp = 20, Hp = 20, CoverId = (string?)null });
        barrier.Should().Match<TokenView>(t => t.Label == "Balcão" && t.X == 2 && t.Y == 3 && t.Width == 3 && t.Height == 1);

        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/barriers/{barrier.Id}/damage", new BarrierDamageRequest(25));
        scene.Tokens.Single().Barrier!.Hp.Should().Be(0, "a barrier never goes under 0");
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/barriers/{barrier.Id}/damage", new BarrierDamageRequest(-8));
        scene.Tokens.Single().Barrier!.Hp.Should().Be(8, "a negative amount repairs, up to the max");

        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/barriers", new BarrierRequest("Cortina", "plaster", false, null, 1, 1), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_barrier_on_a_scene_with_a_fight_is_the_fights_cover()
    {
        var scene = await NewScene("Barreira no tiroteio");
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Ganger", 1, 1));
        var ganger = scene.Tokens.Single().Id;
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/barriers", new BarrierRequest(null, "concrete", true, null, 4, 4, 2, 1));
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/fight", new CreateFightRequest("Tiroteio com muro", [new FightTokenEntry(ganger, CombatSide.Enemy)]));

        var wall = scene.Tokens.Single(t => t.Barrier is not null);
        wall.Barrier!.CoverId.Should().NotBeNull("starting a fight turns the scene's barriers into its cover");
        var fight = await Fight(scene.EncounterId!.Value);
        fight.Covers.Should().ContainSingle(c => c.Id == wall.Barrier.CoverId && c.Material == "concrete" && c.Hp == 25);

        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/barriers", new BarrierRequest("Vidro", "bulletproof-glass", true, null, 6, 6));
        var glass = scene.Tokens.Single(t => t.Label == "Vidro");
        (await Fight(scene.EncounterId!.Value)).Covers.Should().Contain(c => c.Id == glass.Barrier!.CoverId && c.Hp == 30);

        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/barriers/{glass.Id}/damage", new BarrierDamageRequest(12));
        (await Fight(scene.EncounterId!.Value)).Covers.Single(c => c.Id == glass.Barrier!.CoverId).Hp.Should().Be(18);
        scene.Tokens.Single(t => t.Id == glass.Id).Barrier!.Hp.Should().Be(18, "the token shows the cover's HP");

        var e = await Fight(scene.EncounterId!.Value);
        await Send<JsonNode>(_gm, HttpMethod.Post, $"/api/encounters/{e.Id}/covers/{glass.Barrier!.CoverId}/damage", new CoverDamageRequest(e.Version, 18));
        (await Gm(HttpMethod.Get, $"/api/table/scenes/{scene.Id}")).Tokens.Single(t => t.Id == glass.Id).Barrier!.Hp.Should().Be(0, "damage done on the combat table shows on the map");
    }

    [Fact]
    public async Task A_destroyed_barrier_does_not_stop_a_fight_from_starting()
    {
        var scene = await NewScene("Barreira destruida");
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Ganger", 1, 1));
        var ganger = scene.Tokens.Single().Id;
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/barriers", new BarrierRequest("Caixote", "wood", false, null, 3, 3));
        var crate = scene.Tokens.Single(t => t.Barrier is not null).Id;
        await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/barriers/{crate}/damage", new BarrierDamageRequest(5));

        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/fight", new CreateFightRequest("Sem caixote", [new FightTokenEntry(ganger, CombatSide.Enemy)]));

        scene.Tokens.Single(t => t.Id == crate).Barrier!.CoverId.Should().BeNull("a barrier at 0 HP is no cover");
        (await Fight(scene.EncounterId!.Value)).Covers.Should().BeEmpty();
    }

    [Fact]
    public async Task Any_token_can_go_to_the_scenery_layer_and_a_barrier_keeps_its_data_when_edited()
    {
        var scene = await NewScene("Cenario");
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/tokens", new TokenInput("Carro", 1, 1, 2, 1, Layer: TokenLayer.Scenery));
        scene.Tokens.Single().Layer.Should().Be(TokenLayer.Scenery);

        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/barriers", new BarrierRequest("Porta", "steel", false, 20, 3, 3));
        var door = scene.Tokens.Single(t => t.Barrier is not null);
        door.Barrier!.MaxHp.Should().Be(20, "the book's example HP can be given");
        scene = await Gm(HttpMethod.Put, $"/api/table/scenes/{scene.Id}/tokens/{door.Id}", new TokenInput("Porta do cofre", 3, 3, Layer: TokenLayer.Token));
        var edited = scene.Tokens.Single(t => t.Id == door.Id);
        edited.Barrier!.MaxHp.Should().Be(20);
        edited.Layer.Should().Be(TokenLayer.Scenery, "a barrier always sits on the scenery layer");
        edited.Label.Should().Be("Porta do cofre");
    }

    [Fact]
    public async Task Players_see_the_barriers_hp_but_cannot_place_them()
    {
        var scene = await NewScene("Barreira vista");
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");
        scene = await Gm(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/barriers", new BarrierRequest("Rocha", "stone", true, null, 2, 2));
        var seat = await Send<SeatDto>(_gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Olha a rocha"), expected: HttpStatusCode.Created);

        var state = await Send<PlayerStateDto>(_player, HttpMethod.Get, "/api/table/play/state", seat: seat.Token);

        state.Scene!.Tokens.Single(t => t.Barrier is not null).Barrier!.Hp.Should().Be(40);
        state.Scene.Tokens.Single(t => t.Barrier is not null).Controllable.Should().BeFalse();
        (await _player.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/table/scenes/{scene.Id}/barriers") { Headers = { { "X-Seat", seat.Token } } }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
