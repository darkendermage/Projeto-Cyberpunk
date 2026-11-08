using System.Text.Json;
using CyberpunkDb.Application.Characters;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit.Characters;

// The portrait's framing
public class PortraitFocusTests
{
    [Fact]
    public void Portrait_focus_is_clamped()
    {
        var identity = new CharacterIdentity { Name = "V", PortraitFocus = new PortraitFocus(150, -5, 9) }.Normalize();

        identity.PortraitFocus.Should().Be(new PortraitFocus(100, 0, 3));
    }

    [Fact]
    public void Missing_focus_stays_null_and_means_center()
    {
        var identity = JsonSerializer.Deserialize<CharacterIdentity>("""{"name":"V"}""", new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Normalize();

        identity.PortraitFocus.Should().BeNull();
        new PortraitFocus().Should().Be(new PortraitFocus(50, 50, 1));
    }
}
