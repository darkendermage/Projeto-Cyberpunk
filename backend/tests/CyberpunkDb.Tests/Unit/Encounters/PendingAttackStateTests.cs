using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Rules;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit.Encounters;

/// <summary>Fights saved before round 4 load without a pending attack (D1).</summary>
public class PendingAttackStateTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Old_encounter_json_loads()
    {
        var state = JsonSerializer.Deserialize<EncounterState>("""{"combatants":[{"id":"a","kind":"Generic","name":"Ganger","ammo":{}}],"round":2}""", Json)!;

        state.Pending.Should().BeNull();
        state.Combatants.Single().Jammed.Should().BeEmpty();
    }

    [Fact]
    public void Pending_attack_round_trips_with_its_fixed_dice()
    {
        var pending = new PendingAttack(3, new AttackRequest(1, "a", ["b"], "w0", AttackMode.Single, 5, AttackRoll: 10, AttackExtra: 4,
            DefenseRolls: new() { ["b"] = 7 }, DamageDice: [6, 6, 1], Interactive: true), "b", PendingStep.CriticalInjury, ["b"]);

        var back = JsonSerializer.Deserialize<EncounterState>(JsonSerializer.Serialize(new EncounterState { Pending = pending }, Json), Json)!.Pending!;

        back.Step.Should().Be(PendingStep.CriticalInjury);
        back.Request.AttackExtra.Should().Be(4);
        back.Request.DamageDice.Should().Equal(6, 6, 1);
        back.Decided.Should().Equal("b");
    }
}
