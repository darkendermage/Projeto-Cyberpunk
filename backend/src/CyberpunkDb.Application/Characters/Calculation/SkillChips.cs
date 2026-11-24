using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Rules;

namespace CyberpunkDb.Application.Characters.Calculation;

public sealed record SkillChip(string Key, string Label, int Price);

// A Skill Chip is made for one skill: the skill shows in its name, sets its price and is trained at +3 while it is
// installed, unless the skill was already higher (CP:R p.358).
public static class SkillChips
{
    public static SkillChipRule? Rule(CharacterRuleBook rules, CatalogItem item) =>
        rules.Document.Cyberware.SkillChip.Value is { } r && r.Slug.Equals(item.Slug, StringComparison.OrdinalIgnoreCase) ? r : null;

    // null when the item isn't a Skill Chip, or no skill came and none is required yet (the install preview)
    public static SkillChip? Resolve(CharacterRuleBook rules, CatalogItem item, string? skillKey, bool required)
    {
        if (Rule(rules, item) is not { } rule) return null;
        if (string.IsNullOrWhiteSpace(skillKey))
            return required ? throw RequestValidationException.For("skillKey", L.T("Escolha a perícia do Skill Chip.", "Pick the Skill Chip's skill.")) : null;
        var key = SkillKey.Normalize(skillKey);
        var (id, spec) = SkillKey.Parse(key);
        var def = rules.FindSkill(id) ?? throw RequestValidationException.For("skillKey", L.T($"Perícia desconhecida: {id}.", $"Unknown skill: {id}."));
        if (def.Specialized && spec is null)
            throw RequestValidationException.For("skillKey", L.T($"{def.Name} precisa de especialização.", $"{def.Name} needs a specialization."));
        return new SkillChip(key, Label(def, spec), def.DoubleCost ? rule.PriceDoubleCost : rule.Price);
    }

    public static string? LabelOf(CharacterRuleBook rules, string? key)
    {
        if (key is null) return null;
        var (id, spec) = SkillKey.Parse(key);
        return rules.FindSkill(id) is { } def ? Label(def, spec) : key;
    }

    private static string Label(SkillDefinition def, string? spec) => spec is null ? def.Name : $"{def.Name} ({spec})";
}
