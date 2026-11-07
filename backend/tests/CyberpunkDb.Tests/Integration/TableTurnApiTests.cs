using System.Net;
using CyberpunkDb.Application.Table;
using FluentAssertions;

namespace CyberpunkDb.Tests.Integration;

/// <summary>The player passes their own Turn (C3).</summary>
[Collection(ApiCollection.Name)]
public class TableTurnApiTests(ApiFactory factory)
{
    private readonly TableScenario _t = new(factory);

    [Fact]
    public async Task Player_ends_own_turn()
    {
        var s = await _t.FightOnTheTable("PassaTurno");
        await _t.MakeActive(s.EncounterId, s.PcCombatantId);

        var fight = await _t.Send<PlayerEncounterDto>(_t.Player, HttpMethod.Post, "/api/table/play/end-turn", seat: s.Seat.Token);

        fight.ActiveId.Should().NotBe(s.PcCombatantId);
    }

    [Fact]
    public async Task Player_cannot_end_someone_elses_turn()
    {
        var s = await _t.FightOnTheTable("TurnoAlheio");
        await _t.MakeActive(s.EncounterId, s.GangerIds[0]);

        var response = await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/end-turn", seat: s.Seat.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task No_fight_is_409()
    {
        var solo = await _t.FinalizedSolo("SemCombate");
        var seat = await _t.Send<SeatDto>(_t.Gm, HttpMethod.Post, "/api/table/seats", new CreateSeatRequest("Sem combate", solo.Id), expected: HttpStatusCode.Created);

        var response = await _t.Raw(_t.Player, HttpMethod.Post, "/api/table/play/end-turn", seat: seat.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
