using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Api.Desktop;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>The tunnel controls on the GM's side; the test host has no cloudflared.</summary>
[Collection(ApiCollection.Name)]
public class TableTunnelApiTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _gm = factory.CreateClient();

    [Fact]
    public async Task Without_cloudflared_the_gm_is_offered_the_download_and_start_is_refused()
    {
        var status = await _gm.GetFromJsonAsync<TunnelStatus>("/api/table/tunnel", Json);
        status!.State.Should().Be(TunnelState.Missing);
        status.Url.Should().BeNull();
        status.LocalUrl.Should().Be($"http://localhost:{ApiFactory.PlayerPort}");

        (await _gm.PostAsync("/api/table/tunnel/start", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _gm.PostAsync("/api/table/tunnel/stop", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Players_cannot_reach_the_tunnel_controls()
    {
        var player = factory.CreatePlayerClient();
        (await player.GetAsync("/api/table/tunnel")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await player.PostAsync("/api/table/tunnel/download", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
