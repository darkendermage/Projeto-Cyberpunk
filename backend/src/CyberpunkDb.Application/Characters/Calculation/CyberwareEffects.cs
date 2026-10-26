using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Calculation;

/// <summary>An effect from <c>cyberwareEffects</c> that the installed pieces trigger, with the pieces' names.</summary>
public sealed record ActiveCyberwareEffect(CyberwareEffect Effect, string Label, int Count)
{
    /// <summary>Bonus before any STAT cap: per installation when the effect stacks, once otherwise.</summary>
    public int Bonus => Effect.Stacks ? Effect.Add * Count : Effect.Add;
}

/// <summary>A cyberware effect as shown on the sheet.</summary>
public sealed record CyberEffectLine(string Item, string Description, string Source);

public static class CyberwareEffects
{
    /// <summary>
    /// Effects of the pieces that work right now: installed implants not covered by a glove, and the options of worn
    /// hosts such as Smart Glasses (other inventory items don't count).
    /// </summary>
    public static List<ActiveCyberwareEffect> Active(CharacterRuleBook rules, CharacterSheet sheet, NpcCatalog catalog)
    {
        var installed = WearableCyberware.Working(rules, sheet, catalog)
            .GroupBy(i => i.Slug, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), g.First().Name), StringComparer.OrdinalIgnoreCase);

        var active = new List<ActiveCyberwareEffect>();
        foreach (var e in rules.Document.CyberwareEffects.Effects)
        {
            if (e.AllOf.Length > 0)
            {
                if (e.AllOf.All(installed.ContainsKey))
                    active.Add(new ActiveCyberwareEffect(e, string.Join(" + ", e.AllOf.Select(s => installed[s].Name)), 1));
                continue;
            }
            var present = e.Items.Where(installed.ContainsKey).ToList();
            var count = present.Sum(s => installed[s].Count);
            if (present.Count > 0 && count >= e.MinCount)
                active.Add(new ActiveCyberwareEffect(e, string.Join(" / ", present.Select(s => installed[s].Name)), count));
        }
        return active;
    }

    public static List<CyberEffectLine> Lines(CharacterRuleBook rules, IEnumerable<ActiveCyberwareEffect> active) =>
        active.Select(a => new CyberEffectLine(a.Label, Describe(rules, a), CharacterRuleBook.Page(a.Effect.Source))).ToList();

    private static string Describe(CharacterRuleBook rules, ActiveCyberwareEffect a)
    {
        var e = a.Effect;
        if (e.Note is { Length: > 0 } note) return L.D(note);
        if (e.Stat is { } stat)
        {
            var perInstallation = e.Stacks ? L.T(" por instalação", " per installation") : "";
            var upTo = e.Max is { } max ? L.T($", até {max}", $", up to {max}") : "";
            var installed = e.Stacks && a.Count > 1 ? L.T($" ({a.Count} instaladas)", $" ({a.Count} installed)") : "";
            return $"{stat} +{e.Add}{perInstallation}{upTo}{installed}.";
        }
        var skills = string.Join(", ", e.Skills.Select(id => rules.FindSkill(id) is { } s ? L.SkillName(s.Name, s.NamePt) : id));
        return L.T($"+{e.Add} em {skills}", $"+{e.Add} to {skills}") + (e.Condition is { Length: > 0 } c ? $" ({L.D(c)})" : "") + ".";
    }
}
