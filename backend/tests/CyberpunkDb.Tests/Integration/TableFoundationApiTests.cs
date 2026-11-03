using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberpunkDb.Application.Table;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CyberpunkDb.Tests.Integration;

/// <summary>The virtual table's foundation: the players' port, seat links, uploaded images and the live channel.</summary>
[Collection(ApiCollection.Name)]
public class TableFoundationApiTests(ApiFactory factory)
{
    private readonly HttpClient _gm = factory.CreateClient();
    private readonly HttpClient _player = factory.CreatePlayerClient();

    /// <summary>The smallest valid PNG (1x1).</summary>
    internal static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private async Task<SeatDto> NewSeat(string label = "Johnny")
    {
        var response = await _gm.PostAsJsonAsync("/api/table/seats", new CreateSeatRequest(label));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SeatDto>())!;
    }

    private Task<HttpResponseMessage> AsSeat(HttpClient client, string url, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null) request.Headers.Add("X-Seat", token);
        return client.SendAsync(request);
    }

    internal static HubConnection Hub(ApiFactory factory, string? token, bool playerPort)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/table"), o =>
            {
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                if (playerPort) o.Headers[ApiFactory.PortHeader] = ApiFactory.PlayerPort.ToString();
                if (token is not null) o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
            .Build();
    }

    /// <summary>Waits for the server to close a connection it refused after the handshake.</summary>
    private static async Task<bool> ClosedWithin(HubConnection hub, TimeSpan timeout)
    {
        var closed = new TaskCompletionSource();
        hub.Closed += _ => { closed.TrySetResult(); return Task.CompletedTask; };
        try { await hub.StartAsync(); }
        catch (Exception) { return true; }
        return await Task.WhenAny(closed.Task, Task.Delay(timeout)) == closed.Task;
    }

    [Theory]
    [InlineData("/api/items?ids=1")]
    [InlineData("/api/characters")]
    [InlineData("/api/app/update")]
    [InlineData("/api/health")]
    [InlineData("/api/table/seats")]
    [InlineData("/api/encounters")]
    public async Task The_players_port_hides_everything_but_the_table(string url)
    {
        (await _player.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _gm.GetAsync(url)).StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/catalog/weapons")]
    [InlineData("/mesa")]
    [InlineData("/characters/1")]
    public async Task Pages_other_than_a_seat_link_send_players_to_the_table(string url)
    {
        // Opening the bare tunnel address would load the whole app, whose data the port refuses ("connection lost").
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(ApiFactory.PortHeader, ApiFactory.PlayerPort.ToString());
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/mesa/j/");
        (await client.GetAsync("/mesa/j/abc")).StatusCode.Should().NotBe(HttpStatusCode.Redirect);
        (await client.GetAsync("/assets/index-abc.js")).StatusCode.Should().NotBe(HttpStatusCode.Redirect);
        (await _gm.GetAsync(url)).StatusCode.Should().NotBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task The_players_port_refuses_writes_outside_the_player_api()
    {
        (await _player.PostAsJsonAsync("/api/app/update", new { version = "9.9.9" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _player.PostAsJsonAsync("/api/table/seats", new CreateSeatRequest("Intruso"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var upload = new MultipartFormDataContent { { new ByteArrayContent(Png), "file", "map.png" } };
        (await _player.PostAsync("/api/table/media", upload)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _player.DeleteAsync("/index.html")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_seat_link_opens_the_player_api_until_it_is_revoked()
    {
        var seat = await NewSeat("Rogue");
        seat.Token.Length.Should().BeGreaterThanOrEqualTo(22);
        seat.Color.Should().MatchRegex("^#[0-9a-f]{6}$");

        var me = await AsSeat(_player, "/api/table/play/me", seat.Token);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await me.Content.ReadFromJsonAsync<PlayerSeatDto>())!.Label.Should().Be("Rogue");

        (await AsSeat(_player, "/api/table/play/me", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await AsSeat(_player, "/api/table/play/me", "not-a-seat")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await _gm.PostAsync($"/api/table/seats/{seat.Id}/revoke", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AsSeat(_player, "/api/table/play/me", seat.Token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var list = await _gm.GetFromJsonAsync<List<SeatDto>>("/api/table/seats");
        list!.Single(s => s.Id == seat.Id).Revoked.Should().BeTrue();
    }

    [Fact]
    public async Task Seats_need_a_name_and_an_existing_character()
    {
        (await _gm.PostAsJsonAsync("/api/table/seats", new CreateSeatRequest("  "))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _gm.PostAsJsonAsync("/api/table/seats", new CreateSeatRequest("Alt", 999999))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Images_go_to_disk_and_both_ports_download_them()
    {
        using var upload = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        upload.Add(file, "file", "map.png");
        var response = await _gm.PostAsync("/api/table/media", upload);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var media = (await response.Content.ReadFromJsonAsync<MediaDto>())!;
        media.ContentType.Should().Be("image/png");
        Directory.GetFiles(factory.MediaRoot).Should().Contain(f => f.EndsWith(media.Id.ToString("N")));

        var download = await _player.GetAsync(media.Url);
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(Png);
        download.Headers.CacheControl!.ToString().Should().Contain("immutable");
    }

    [Fact]
    public async Task Uploads_that_are_not_images_are_refused()
    {
        using var upload = new MultipartFormDataContent();
        var file = new ByteArrayContent("<script>alert(1)</script>"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Add(file, "file", "fake.png");
        (await _gm.PostAsync("/api/table/media", upload)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _player.GetAsync($"/api/table/media/{Guid.NewGuid():N}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Other_sites_cannot_drive_the_table_as_the_gm()
    {
        var evil = new HttpRequestMessage(HttpMethod.Get, "/api/table/seats");
        evil.Headers.Add("Origin", "https://evil.example");
        (await _gm.SendAsync(evil)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var dev = new HttpRequestMessage(HttpMethod.Get, "/api/table/seats");
        dev.Headers.Add("Origin", "http://localhost:5173");
        (await _gm.SendAsync(dev)).StatusCode.Should().Be(HttpStatusCode.OK);

        var negotiate = new HttpRequestMessage(HttpMethod.Post, "/hubs/table/negotiate?negotiateVersion=1");
        negotiate.Headers.Add("Origin", "https://evil.example");
        (await _gm.SendAsync(negotiate)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_rebound_domain_cannot_pose_as_the_gm()
    {
        // DNS rebinding: a site's own name pointed at 127.0.0.1, so its Origin and the Host match.
        var rebound = new HttpRequestMessage(HttpMethod.Get, "/api/table/seats");
        rebound.Headers.Host = "rebind.evil:5090";
        rebound.Headers.Add("Origin", "http://rebind.evil:5090");
        (await _gm.SendAsync(rebound)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var noOrigin = new HttpRequestMessage(HttpMethod.Post, "/hubs/table/negotiate?negotiateVersion=1");
        noOrigin.Headers.Host = "rebind.evil:5090";
        (await _gm.SendAsync(noOrigin)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var local = new HttpRequestMessage(HttpMethod.Get, "/api/table/seats");
        local.Headers.Host = "127.0.0.1:5090";
        (await _gm.SendAsync(local)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_hub_takes_the_gm_and_seated_players_only()
    {
        await using var gm = Hub(factory, null, playerPort: false);
        await gm.StartAsync();
        gm.State.Should().Be(HubConnectionState.Connected);

        var seat = await NewSeat("Panam");
        await using var player = Hub(factory, seat.Token, playerPort: true);
        await player.StartAsync();
        player.State.Should().Be(HubConnectionState.Connected);
        // The hub registers the connection right after the handshake the client waits for.
        List<int>? online = [];
        for (var i = 0; i < 50 && !online!.Contains(seat.Id); i++)
        {
            await Task.Delay(100);
            online = await _gm.GetFromJsonAsync<List<int>>("/api/table/seats/online");
        }
        online.Should().Contain(seat.Id);

        await using var stranger = Hub(factory, null, playerPort: true);
        (await ClosedWithin(stranger, TimeSpan.FromSeconds(10))).Should().BeTrue();

        await using var forged = Hub(factory, "forged-token", playerPort: true);
        (await ClosedWithin(forged, TimeSpan.FromSeconds(10))).Should().BeTrue();
    }

    [Fact]
    public async Task Revoking_a_seat_throws_its_player_out()
    {
        var seat = await NewSeat("Judy");
        await using var player = Hub(factory, seat.Token, playerPort: true);
        var closed = new TaskCompletionSource();
        player.Closed += _ => { closed.TrySetResult(); return Task.CompletedTask; };
        await player.StartAsync();

        await _gm.PostAsync($"/api/table/seats/{seat.Id}/revoke", null);
        (await Task.WhenAny(closed.Task, Task.Delay(TimeSpan.FromSeconds(10)))).Should().Be(closed.Task);
    }
}
