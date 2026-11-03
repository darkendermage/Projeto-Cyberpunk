using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit.Table;

public class TableLobbyTests
{
    [Fact]
    public void Starts_closed() => new TableLobby().Open.Should().BeFalse();

    [Fact]
    public void Opens_and_closes()
    {
        var lobby = new TableLobby();
        lobby.SetOpen(true);
        lobby.Open.Should().BeTrue();
        lobby.SetOpen(false);
        lobby.Open.Should().BeFalse();
    }

    [Fact]
    public void TryClaim_wins_once()
    {
        var lobby = new TableLobby();
        lobby.TryClaim(1).Should().BeTrue();
        lobby.TryClaim(1).Should().BeFalse();
        lobby.IsClaimed(1).Should().BeTrue();
        lobby.IsClaimed(2).Should().BeFalse();
    }

    [Fact]
    public void Release_frees_the_seat()
    {
        var lobby = new TableLobby();
        lobby.MarkClaimed(3);
        lobby.Release(3);
        lobby.IsClaimed(3).Should().BeFalse();
        lobby.TryClaim(3).Should().BeTrue();
    }

    [Fact]
    public async Task Parallel_claims_have_one_winner()
    {
        var lobby = new TableLobby();
        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => lobby.TryClaim(1))));
        results.Count(r => r).Should().Be(1);
    }
}
