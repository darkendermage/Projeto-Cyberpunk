using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>Images over the map as layers (round 5): the GM's and the players', with locks, hiding and order.</summary>
[Collection(ApiCollection.Name)]
public class TableImagesApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    private async Task<MediaDto> Upload(HttpClient client, string? seat = null, byte[]? bytes = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, seat is null ? "/api/table/media" : "/api/table/play/media")
        {
            Content = new MultipartFormDataContent { { new ByteArrayContent(bytes ?? TableFoundationApiTests.Png), "file", "img.png" } },
        };
        if (seat is not null) request.Headers.Add("X-Seat", seat);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MediaDto>(TableScenario.Json))!;
    }

    /// <summary>A live scene and a seat (no fight needed).</summary>
    private async Task<(SceneView Scene, SeatDto Seat, SeatDto Other)> LiveScene(string name)
    {
        var scene = await _t.Send<SceneView>(_t.Gm, HttpMethod.Post, "/api/table/scenes", new CreateSceneRequest($"Imagens {name}"), expected: HttpStatusCode.Created);
        await _t.Send<SceneView>(_t.Gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/activate");
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest($"Pintor {name}"), expected: HttpStatusCode.Created);
        var other = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest($"Outro {name}"), expected: HttpStatusCode.Created);
        return (scene, seat, other);
    }

    private static ImageInput Input(Guid? media, double x = 5, double y = 5, double w = 2, double h = 2, bool locked = false, bool hidden = false) =>
        new(media, "Mesa", x, y, w, h, 0, 1, locked, hidden);

    [Fact]
    public async Task Gm_adds_moves_resizes_and_orders_images()
    {
        var (scene, _, _) = await LiveScene("Gm");
        var media = await Upload(_t.Gm);
        var v = await _t.Send<SceneView>(_t.Gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/images", Input(media.Id));
        v = await _t.Send<SceneView>(_t.Gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/images", Input(media.Id, 8, 8));
        var first = v.Images!.Single(i => i.X == 5);
        var second = v.Images!.Single(i => i.X == 8);
        second.Order.Should().BeGreaterThan(first.Order, "a new image goes on top");
        first.Url.Should().Contain(media.Id.ToString("N"));

        v = await _t.Send<SceneView>(_t.Gm, HttpMethod.Put, $"/api/table/scenes/{scene.Id}/images/{first.Id}", new ImageInput(null, "Tapete", 3.5, 4, 6.25, 3, 45, 0.5));
        var moved = v.Images!.Single(i => i.Id == first.Id);
        moved.Should().Match<SceneImageView>(i => i.X == 3.5 && i.Width == 6.25 && i.Rotation == 45 && i.Opacity == 0.5 && i.Name == "Tapete");

        v = await _t.Send<SceneView>(_t.Gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/images/{first.Id}/order", new ImageOrderRequest(null));
        v.Images!.Single(i => i.Id == first.Id).Order.Should().BeGreaterThan(v.Images!.Single(i => i.Id == second.Id).Order, "null puts it on top");

        (await _t.Raw(_t.Gm, HttpMethod.Put, $"/api/table/scenes/{scene.Id}/images/{first.Id}", new ImageInput(null, "x", 1, 1, 0.1, 2))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Player_adds_and_edits_own_image()
    {
        var (_, seat, _) = await LiveScene("Dono");
        var media = await Upload(_t.Player, seat.Token);

        var v = await _t.Send<SceneView>(_t.Player, HttpMethod.Post, "/api/table/play/images", Input(media.Id), seat.Token);
        var mine = v.Images!.Single(i => i.OwnerSeatId == seat.Id);
        mine.Editable.Should().BeTrue();

        v = await _t.Send<SceneView>(_t.Player, HttpMethod.Put, $"/api/table/play/images/{mine.Id}", new ImageInput(null, "Minha", 9, 9, 3, 3), seat.Token);
        v.Images!.Single(i => i.Id == mine.Id).X.Should().Be(9);
        (await _t.Raw(_t.Player, HttpMethod.Delete, $"/api/table/play/images/{mine.Id}", seat: seat.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Player_cannot_touch_others_image()
    {
        var (scene, seat, other) = await LiveScene("Alheia");
        var media = await Upload(_t.Gm);
        var v = await _t.Send<SceneView>(_t.Player, HttpMethod.Post, "/api/table/play/images", Input(media.Id), seat.Token);
        var theirs = v.Images!.Single(i => i.OwnerSeatId == seat.Id);
        var gmView = await _t.Send<SceneView>(_t.Gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/images", Input(media.Id, 1, 1));
        var gms = gmView.Images!.Single(i => i.OwnerSeatId == null);

        foreach (var id in new[] { theirs.Id, gms.Id })
        {
            (await _t.Raw(_t.Player, HttpMethod.Put, $"/api/table/play/images/{id}", new ImageInput(null, "x", 1, 1, 2, 2), other.Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await _t.Raw(_t.Player, HttpMethod.Post, $"/api/table/play/images/{id}/order", new ImageOrderRequest(null), other.Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await _t.Raw(_t.Player, HttpMethod.Delete, $"/api/table/play/images/{id}", seat: other.Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Gm_lock_blocks_owner()
    {
        var (scene, seat, _) = await LiveScene("Trava");
        var media = await Upload(_t.Player, seat.Token);
        var v = await _t.Send<SceneView>(_t.Player, HttpMethod.Post, "/api/table/play/images", Input(media.Id), seat.Token);
        var mine = v.Images!.Single(i => i.OwnerSeatId == seat.Id);
        await _t.Send<SceneView>(_t.Gm, HttpMethod.Put, $"/api/table/scenes/{scene.Id}/images/{mine.Id}", Input(null, locked: true));

        (await _t.Raw(_t.Player, HttpMethod.Put, $"/api/table/play/images/{mine.Id}", Input(null, x: 7), seat.Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _t.Raw(_t.Player, HttpMethod.Put, $"/api/table/play/images/{mine.Id}", Input(null, locked: false), seat.Token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var state = await _t.Send<PlayerStateDto>(_t.Player, HttpMethod.Get, "/api/table/play/state", seat: seat.Token);
        state.Scene!.Images!.Single(i => i.Id == mine.Id).Should().Match<SceneImageView>(i => i.LockedByGm && !i.Editable);

        // A lock the player put on their own image they can take off.
        await _t.Send<SceneView>(_t.Gm, HttpMethod.Put, $"/api/table/scenes/{scene.Id}/images/{mine.Id}", Input(null, locked: false));
        await _t.Send<SceneView>(_t.Player, HttpMethod.Put, $"/api/table/play/images/{mine.Id}", Input(null, locked: true), seat.Token);
        await _t.Send<SceneView>(_t.Player, HttpMethod.Put, $"/api/table/play/images/{mine.Id}", Input(null, locked: false), seat.Token);
    }

    [Fact]
    public async Task Hidden_image_not_in_player_view()
    {
        var (scene, seat, _) = await LiveScene("Oculta");
        var media = await Upload(_t.Gm);
        await _t.Send<SceneView>(_t.Gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/images", Input(media.Id, hidden: true));

        var raw = await _t.Raw(_t.Player, HttpMethod.Get, "/api/table/play/state", seat: seat.Token);
        var body = await raw.Content.ReadAsStringAsync();
        body.Should().NotContain(media.Id.ToString("N"));
        JsonNode.Parse(body)!["scene"]!["images"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Order_renumbers_after_moves_and_delete()
    {
        var (scene, _, _) = await LiveScene("Ordem");
        var media = await Upload(_t.Gm);
        SceneView v = null!;
        for (var i = 0; i < 4; i++) v = await _t.Send<SceneView>(_t.Gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/images", Input(media.Id, i, i));
        var bottom = v.Images!.OrderBy(i => i.Order).First();
        var top = v.Images!.OrderBy(i => i.Order).Last();

        v = await _t.Send<SceneView>(_t.Gm, HttpMethod.Post, $"/api/table/scenes/{scene.Id}/images/{bottom.Id}/order", new ImageOrderRequest(top.Id));
        v.Images!.OrderByDescending(i => i.Order).Select(i => i.Id).Take(2).Should().Equal(top.Id, bottom.Id);
        v = await _t.Send<SceneView>(_t.Gm, HttpMethod.Delete, $"/api/table/scenes/{scene.Id}/images/{top.Id}");

        v.Images!.Select(i => i.Order).OrderBy(o => o).Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task Seat_limit_is_10()
    {
        var (_, seat, _) = await LiveScene("Limite");
        var media = await Upload(_t.Player, seat.Token);
        for (var i = 0; i < 10; i++) await _t.Send<SceneView>(_t.Player, HttpMethod.Post, "/api/table/play/images", Input(media.Id, i, 0), seat.Token);

        (await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/images", Input(media.Id), seat.Token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Player_upload_over_2mb_is_400()
    {
        var (_, seat, _) = await LiveScene("Grande");
        var big = new byte[2 * 1024 * 1024 + 10];
        TableFoundationApiTests.Png.CopyTo(big, 0);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/table/play/media")
        {
            Content = new MultipartFormDataContent { { new ByteArrayContent(big), "file", "big.png" } },
        };
        request.Headers.Add("X-Seat", seat.Token);

        (await _t.Player.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void Old_scene_json_loads()
    {
        var state = SceneService.Deserialize("""{"grid":{},"background":{},"tokens":[],"templates":[]}""");

        state.Images.Should().BeEmpty();
    }
}
