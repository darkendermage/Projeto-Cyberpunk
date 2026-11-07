using System.Net;
using System.Text.Json;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>The table's music (G): the GM loads a YouTube link and plays it; every screen follows over the hub.</summary>
[Collection(ApiCollection.Name)]
public class TableMusicApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    [Fact]
    public async Task Play_sets_playing_and_timestamp()
    {
        await _t.Send<TableMusicStateDto>(_t.Gm, HttpMethod.Put, "/api/table/music", new LoadMusicRequest("https://youtu.be/dQw4w9WgXcQ", "Teste"));

        var before = DateTime.UtcNow.AddSeconds(-5);
        var state = await _t.Send<TableMusicStateDto>(_t.Gm, HttpMethod.Post, "/api/table/music/play", new MusicPositionRequest(12));

        state.Music!.Playing.Should().BeTrue();
        state.Music.VideoId.Should().Be("dQw4w9WgXcQ");
        state.Music.PositionSeconds.Should().Be(12);
        state.Music.UpdatedAt.Should().BeAfter(before);

        var paused = await _t.Send<TableMusicStateDto>(_t.Gm, HttpMethod.Post, "/api/table/music/pause", new MusicPositionRequest(null));
        paused.Music!.Playing.Should().BeFalse();
        paused.Music.PositionSeconds.Should().BeGreaterThanOrEqualTo(12);
    }

    [Fact]
    public async Task Recent_keeps_8_deduped()
    {
        for (var i = 0; i < 10; i++)
            await _t.Send<TableMusicStateDto>(_t.Gm, HttpMethod.Put, "/api/table/music", new LoadMusicRequest($"https://youtu.be/abcdefghij{i}", $"Faixa {i}"));
        var state = await _t.Send<TableMusicStateDto>(_t.Gm, HttpMethod.Put, "/api/table/music", new LoadMusicRequest("https://youtu.be/abcdefghij9", "Faixa 9 de novo"));

        state.Recent.Should().HaveCount(8);
        state.Recent.Count(r => r.VideoId == "abcdefghij9").Should().Be(1);
        state.Recent[0].VideoId.Should().Be("abcdefghij9");
    }

    [Fact]
    public async Task Bad_link_is_400()
    {
        (await _t.Raw(_t.Gm, HttpMethod.Put, "/api/table/music", new LoadMusicRequest("https://evil.com/x", null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Player_state_includes_music_and_seat_receives_the_event()
    {
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Ouvinte"), expected: HttpStatusCode.Created);
        await using var hub = TableFoundationApiTests.Hub(factory, seat.Token, playerPort: true);
        var got = new SemaphoreSlim(0);
        hub.On<JsonElement>("music", _ => got.Release());
        await hub.StartAsync();

        await _t.Send<TableMusicStateDto>(_t.Gm, HttpMethod.Put, "/api/table/music", new LoadMusicRequest("https://youtu.be/dQw4w9WgXcQ", null));

        (await got.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        var state = await _t.Send<PlayerStateDto>(_t.Player, HttpMethod.Get, "/api/table/play/state", seat: seat.Token);
        state.Music!.VideoId.Should().Be("dQw4w9WgXcQ");
        (await _t.Raw(_t.Player, HttpMethod.Put, "/api/table/music", new LoadMusicRequest("https://youtu.be/dQw4w9WgXcQ", null), seat.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "only the GM controls the music");
    }
}
