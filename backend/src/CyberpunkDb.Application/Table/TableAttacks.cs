using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Domain.Entities;

namespace CyberpunkDb.Application.Table;

// numbers are null where the reader can't see them
public sealed record TableAttackTarget(
    string Id, string Name, bool Pc, bool Hit, int? Damage, int? Through, string Location, int? Dv, int? DefenseRoll,
    IReadOnlyList<int> DamageDice, string? CriticalInjury, IReadOnlyList<string>? Notes = null);

// Drives the attack card, the map effect and the dice. Undone is set when the GM takes it back.
public sealed record TableAttack(
    int EncounterId, int Seq, string AttackerId, string AttackerName, bool AttackerPc, string Weapon, AttackMode Mode, bool Melee,
    int? AttackRoll, int AttackTotal, IReadOnlyList<TableAttackTarget> Targets, bool Undone = false, int? AttackExtra = null,
    IReadOnlyList<int>? CritDice = null, string? DiceColor = null, IReadOnlyList<string>? Notes = null);

// either the resolved attack or the step it waits on
public sealed record QuickAttackResult(TableAttack? Attack, PendingView? Pending);

public static class TableAttacks
{
    // GM view, every number
    public static TableAttack From(EncounterDetailDto detail, AttackResult a)
    {
        bool IsPc(string id) => detail.Queue.FirstOrDefault(f => f.Id == id)?.Kind == CombatantKind.Pc;
        // Precision Attack goes on the card (already inside the total)
        var attacker = detail.Queue.FirstOrDefault(f => f.Id == a.AttackerId);
        var precision = Math.Min(3, (attacker?.Awareness("precisionAttack") ?? 0) / 3);
        IReadOnlyList<string> notes = precision > 0 ? [$"Precision Attack +{precision} (CP:R p.146)"] : [];
        return new TableAttack(detail.Id, a.Seq, a.AttackerId, a.Attacker, IsPc(a.AttackerId), a.Weapon, a.Mode, a.Melee, a.AttackRoll, a.AttackTotal,
            a.Targets.Select(t => new TableAttackTarget(t.TargetId, t.TargetName, IsPc(t.TargetId), t.Hit, t.Damage, t.Through, t.Location, t.Dv,
                t.DefenseRoll, t.DamageDice, t.CriticalInjury, AwarenessNotes(t.Notes))).ToList(), AttackExtra: a.AttackExtra, CritDice: a.CritDice, Notes: notes);
    }

    // Spot Weakness, Damage Deflection
    private static IReadOnlyList<string> AwarenessNotes(IReadOnlyList<string> notes) =>
        notes.Where(n => n.StartsWith("Spot Weakness", StringComparison.Ordinal) || n.StartsWith("Damage Deflection", StringComparison.Ordinal)).ToList();

    // Players see the rolls and damage, so the whole table can follow. Only how much an NPC lost stays hidden.
    public static TableAttack ForSeat(TableAttack a) => a with
    {
        Targets = a.Targets.Select(t => t.Pc ? t : t with { Through = null }).ToList(),
    };

    // Center to center like the ruler: diagonals count as one square, straight line when there's no grid.
    public static double Meters(SceneGrid grid, SceneToken a, SceneToken b)
    {
        var (ax, ay) = (a.X + a.Width / 2, a.Y + a.Height / 2);
        var (bx, by) = (b.X + b.Width / 2, b.Y + b.Height / 2);
        if (grid.Type == GridType.None) return Math.Sqrt(Math.Pow(bx - ax, 2) + Math.Pow(by - ay, 2)) * grid.MetersPerCell;
        var squares = Math.Max(Math.Abs(Math.Floor(bx) - Math.Floor(ax)), Math.Abs(Math.Floor(by) - Math.Floor(ay)));
        return squares * grid.MetersPerCell;
    }
}
