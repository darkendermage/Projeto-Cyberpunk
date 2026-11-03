using CyberpunkDb.Api.Desktop;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit.Desktop;

public class TunnelServiceTests
{
    [Theory]
    [InlineData("2026-11-02T10:00:00Z INF |  https://brave-neon-city-1234.trycloudflare.com                                |", "https://brave-neon-city-1234.trycloudflare.com")]
    [InlineData("INF Your quick Tunnel has been created! Visit it at: https://a-b.trycloudflare.com", "https://a-b.trycloudflare.com")]
    [InlineData("INF Starting tunnel tunnelID=abc", null)]
    [InlineData("ERR https://api.trycloudflare.com/tunnel failed", null)]
    public void The_public_address_is_read_from_cloudflared_output(string line, string? expected) =>
        TunnelService.ParseUrl(line).Should().Be(expected);
}
