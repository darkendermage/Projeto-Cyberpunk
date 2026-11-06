using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Domain.Entities;

namespace CyberpunkDb.Application.Table;

/// <summary>One target of an attack as the chat shows it; the numbers are null where the reader may not see them.</summary>
public sealed record TableAttackTarget(
    string Id, string Name, bool Pc, bool Hit, int? Damage, int? Through, string Location, int? Dv, int? DefenseRoll,
    IReadOnlyList<int> DamageDice, string? CriticalInjury);

/// <summary>
/// An attack of the fight linked to the live scene, as a chat line: it drives the attack card, the effect on the map and
/// the dice. <see cref="Seq"/> is the fight's attack number; <see cref="Undone"/> is set when the GM takes it back.
/// </summary>
public sealed record TableAttack(
    int EncounterId, int Seq, string AttackerId, string AttackerName, bool AttackerPc, string Weapon, AttackMode Mode, bool Melee,
    int? AttackRoll, int AttackTotal, IReadOnlyList<TableAttackTarget> Targets, bool Undone = false);

public static class TableAttacks
{
    /// <summary>The attack the fight just resolved, with every number (the GM's view).</summary>
    public static TableAttack From(EncounterDetailDto detail, AttackResult a)
    {
        bool IsPc(string id) => detail.Queue.FirstOrDefault(f => f.Id == id)?.Kind == CombatantKind.Pc;
        return new TableAttack(detail.Id, a.Seq, a.AttackerId, a.Attacker, IsPc(a.AttackerId), a.Weapon, a.Mode, a.Melee, a.AttackRoll, a.AttackTotal,
            a.Targets.Select(t => new TableAttackTarget(t.TargetId, t.TargetName, IsPc(t.TargetId), t.Hit, t.Damage, t.Through, t.Location, t.Dv,
                t.DefenseRoll, t.DamageDice, t.CriticalInjury)).ToList());
    }

    /// <summary>
    /// What a player reads: a hit or a miss on everyone, but the damage, the DV and the defense roll only on PCs, the same
    /// line the fight draws for NPCs everywhere else (their Wound State, never their numbers).
    /// </summary>
    public static TableAttack ForSeat(TableAttack a) => a with
    {
        Targets = a.Targets.Select(t => t.Pc ? t : t with { Damage = null, Through = null, Dv = null, DefenseRoll = null, DamageDice = [] }).ToList(),
    };

    /// <summary>
    /// Meters between two tokens, center to center, the way the ruler counts them: squares between their cells with a
    /// diagonal as one, or the straight line on a scene without a grid.
    /// </summary>
    public static double Meters(SceneGrid grid, SceneToken a, SceneToken b)
    {
        var (ax, ay) = (a.X + a.Width / 2, a.Y + a.Height / 2);
        var (bx, by) = (b.X + b.Width / 2, b.Y + b.Height / 2);
        if (grid.Type == GridType.None) return Math.Sqrt(Math.Pow(bx - ax, 2) + Math.Pow(by - ay, 2)) * grid.MetersPerCell;
        var squares = Math.Max(Math.Abs(Math.Floor(bx) - Math.Floor(ax)), Math.Abs(Math.Floor(by) - Math.Floor(ay)));
        return squares * grid.MetersPerCell;
    }
}
