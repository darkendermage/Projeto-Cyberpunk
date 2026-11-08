using System.Net;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

// Every player of the table on the strip, on the scene or not
[Collection(ApiCollection.Name)]
public class TablePartyApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    private async Task<(SeatDto Seat, int CharacterId)> SeatWithCharacter(string name)
    {
        var c = await _t.FinalizedSolo(name);
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest($"Lugar {name}", c.Id), expected: HttpStatusCode.Created);
        return (seat, c.Id);
    }

    [Fact]
    public async Task Party_lists_seat_without_token_as_not_in_scene()
    {
        var s = await _t.FightOnTheTable("ComToken");
        var (_, away) = await SeatWithCharacter("Ausente");

        var party = await _t.Send<List<PartyMemberDto>>(_t.Gm, HttpMethod.Get, $"/api/table/scenes/{s.SceneId}/party");

        party.Single(p => p.CharacterId == s.CharacterId).InScene.Should().BeTrue();
        var member = party.Single(p => p.CharacterId == away);
        member.InScene.Should().BeFalse();
        member.SeatId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Party_skips_revoked_seats()
    {
        var s = await _t.FightOnTheTable("Revogar");
        var (seat, id) = await SeatWithCharacter("Revogado");
        (await _t.Raw(_t.Gm, HttpMethod.Post, $"/api/table/seats/{seat.Id}/revoke")).EnsureSuccessStatusCode();

        var party = await _t.Send<List<PartyMemberDto>>(_t.Gm, HttpMethod.Get, $"/api/table/scenes/{s.SceneId}/party");

        party.Should().NotContain(p => p.CharacterId == id);
    }

    [Fact]
    public async Task Party_marks_connected_seat_online()
    {
        var s = await _t.FightOnTheTable("Online");
        await using var hub = TableFoundationApiTests.Hub(factory, s.Seat.Token, playerPort: true);
        await hub.StartAsync();
        await Task.Delay(300);

        var party = await _t.Send<List<PartyMemberDto>>(_t.Gm, HttpMethod.Get, $"/api/table/scenes/{s.SceneId}/party");

        party.Single(p => p.CharacterId == s.CharacterId).Online.Should().BeTrue();
    }

    [Fact]
    public async Task Player_party_includes_other_seats_off_scene()
    {
        var s = await _t.FightOnTheTable("Visto");
        var (_, away) = await SeatWithCharacter("LongeDaCena");

        var party = await _t.Send<List<PartyMemberDto>>(_t.Player, HttpMethod.Get, "/api/table/play/party", seat: s.Seat.Token);

        party.Should().Contain(p => p.CharacterId == away && !p.InScene);
        party.Should().Contain(p => p.CharacterId == s.CharacterId && p.InScene);
    }
}
