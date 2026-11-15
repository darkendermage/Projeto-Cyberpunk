using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Calculation;

// an entry of cyberwareEffects triggered by the installed pieces
public sealed record ActiveCyberwareEffect(CyberwareEffect Effect, string Label, int Count)
{
    // before STAT caps; per install when it stacks
    public int Bonus => Effect.Stacks ? Effect.Add * Count : Effect.Add;
}

public sealed record CyberEffectLine(string Item, string Description, string Source);

// What an implant does at the table, for fights and rolls. Slug: the first installed piece behind it.
public sealed record CyberTraitLine(
    string Slug, string Label, string? Kind, int Add, int? Value, IReadOnlyList<string> Stats, IReadOnlyList<string> Injuries,
    string Note, string Source, CyberActivation? Activate);

public static class CyberwareEffects
{
    // Uncovered implants plus options in worn hosts (other inventory items don't count).
    public static List<ActiveCyberwareEffect> Active(CharacterRuleBook rules, CharacterSheet sheet, NpcCatalog catalog)
    {
        var installed = WearableCyberware.Working(rules, sheet, catalog)
            .GroupBy(i => i.Slug, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), g.First().Name), StringComparer.OrdinalIgnoreCase);

        var active = new List<ActiveCyberwareEffect>();
        foreach (var e in rules.Document.CyberwareEffects.Effects)
        {
            if (e.UnlessStat is { } gate && sheet.Stat(gate) >= (e.UnlessMin ?? int.MaxValue)) continue;
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

    public static List<CyberTraitLine> Traits(CharacterRuleBook rules, IEnumerable<ActiveCyberwareEffect> active, CharacterSheet sheet, NpcCatalog catalog)
    {
        var slugs = sheet.Cyberware.Select(c => catalog.Find(c.ItemId)?.Slug).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return active.Where(a => a.Effect.Kind is not null || a.Effect.Activate is not null)
            .Select(a => new CyberTraitLine(a.Effect.Items.Concat(a.Effect.AllOf).FirstOrDefault(slugs.Contains) ?? a.Effect.Items.FirstOrDefault() ?? "",
                a.Label, a.Effect.Kind, a.Effect.Add, a.Effect.Value, a.Effect.Stats, a.Effect.Injuries, Describe(rules, a),
                CharacterRuleBook.Page(a.Effect.Source), a.Effect.Activate))
            .ToList();
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
