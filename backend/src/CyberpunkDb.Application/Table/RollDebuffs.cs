using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Table;

// What weighs on the roller when the dice hit the chat, so nobody forgets it: the Wound State penalty and other
// action penalties, active Critical Injuries and conditions. Kind: "penalty" | "injury" | "condition".
public sealed record RollDebuff(string Kind, string Label, int? Value = null, string? Detail = null);

public static class RollDebuffs
{
    // Book names of the Wound States that carry a penalty (CP:R p.220)
    private static string? WoundName(WoundState w) => w switch
    {
        WoundState.Seriously => "Seriously Wounded",
        WoundState.Mortally => "Mortally Wounded",
        _ => null,
    };

    public static IReadOnlyList<RollDebuff> For(CharacterReport report, IEnumerable<string> conditions)
    {
        var list = new List<RollDebuff>();
        foreach (var p in report.ActionPenalties.Where(p => p.Value != 0))
            list.Add(new RollDebuff("penalty", p.Label, p.Value, L.T("em todas as Ações", "to all Actions")));
        // a Wound State without its own penalty line (Mortally Wounded's Death Saves, for example)
        if (WoundName(report.Health.WoundState) is { } wound && !list.Any(d => d.Label.Contains(wound, StringComparison.OrdinalIgnoreCase)))
            list.Add(new RollDebuff("penalty", wound, null, null));
        foreach (var i in report.ActiveInjuries.Where(i => i.Status == InjuryStatus.Active))
            list.Add(new RollDebuff("injury", i.Name, null, i.Effect));
        foreach (var c in conditions.Distinct()) list.Add(new RollDebuff("condition", c.Split(':')[0], null, null));
        return list;
    }

    public static IReadOnlyList<RollDebuff> For(FighterView f)
    {
        var list = new List<RollDebuff>();
        if (WoundName(f.WoundState) is { } wound || f.ActionPenalty != 0)
            list.Add(new RollDebuff("penalty", WoundName(f.WoundState) ?? L.T("Penalidade", "Penalty"), f.ActionPenalty == 0 ? null : f.ActionPenalty,
                L.T("em todas as Ações", "to all Actions")));
        foreach (var i in f.Injuries) list.Add(new RollDebuff("injury", i.Name, null, i.Effect));
        foreach (var c in f.Conditions.Distinct()) list.Add(new RollDebuff("condition", c.Split(':')[0], null, null));
        return list;
    }
}
