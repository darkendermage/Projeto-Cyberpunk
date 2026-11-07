using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit.Table;

/// <summary>The links the GM pastes for the table's music (G).</summary>
public class YouTubeLinkTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ", null)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=42", "dQw4w9WgXcQ", null)]
    [InlineData("https://www.youtube.com/playlist?list=PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG", null, "PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ&list=PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG", "dQw4w9WgXcQ", "PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG")]
    [InlineData("youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ", null)]
    public void Parse_accepts_known_forms(string url, string? video, string? list)
    {
        var (v, l) = YouTubeLink.Parse(url);

        v.Should().Be(video);
        l.Should().Be(list);
    }

    [Theory]
    [InlineData("https://evil.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://www.youtube.com/")]
    [InlineData("")]
    public void Parse_rejects_other_hosts_and_bad_ids(string url)
    {
        var act = () => YouTubeLink.Parse(url);

        act.Should().Throw<RequestValidationException>();
    }
}
