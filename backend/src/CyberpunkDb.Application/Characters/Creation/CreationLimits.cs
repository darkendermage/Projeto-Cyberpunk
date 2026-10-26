using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Creation;

/// <summary>A limit in force for one character: the rule value or the campaign variant that replaced it.</summary>
public sealed record EffectiveLimit(int Value, string Source, bool CampaignVariant)
{
    public static EffectiveLimit From(Ruled<int> rule, int? overrideValue) =>
        overrideValue is { } v && v != rule.Value
            ? new EffectiveLimit(v, L.T($"Variante de campanha (regra: {rule.Value}, {rule.Reference})", $"Campaign variant (rule: {rule.Value}, {rule.Reference})"), true)
            : new EffectiveLimit(rule.Value, rule.Reference, false);
}

/// <summary>
/// The creation parameters that apply to one character, with campaign overrides resolved. Null members do not
/// apply to the character's method (e.g. Streetrats have no skill budget).
/// </summary>
public sealed record CreationLimits(
    CreationMethod Method,
    EffectiveLimit? StatPoints,
    EffectiveLimit? StatMin,
    EffectiveLimit? StatMax,
    EffectiveLimit? SkillPoints,
    EffectiveLimit? SkillMin,
    EffectiveLimit? SkillMax,
    EffectiveLimit BasicSkillMin,
    EffectiveLimit? GearBudget,
    EffectiveLimit? FashionBudget,
    EffectiveLimit? StartingCash,
    bool RoleSkillsOnly)
{
    public static CreationLimits For(CharacterRuleBook rules, CreationState creation)
    {
        var m = rules.Document.Creation.Methods;
        var o = creation.Overrides;
        var basic = EffectiveLimit.From(rules.Document.Creation.BasicSkillMin, null);
        return creation.Method switch
        {
            CreationMethod.Streetrat => new CreationLimits(creation.Method,
                null, null, null, null, null, null, basic, null, null,
                EffectiveLimit.From(m.Streetrat.StartingCash, null), RoleSkillsOnly: true),
            CreationMethod.Edgerunner => new CreationLimits(creation.Method,
                null, null, null,
                EffectiveLimit.From(m.Edgerunner.SkillPoints, o.SkillPoints),
                EffectiveLimit.From(m.Edgerunner.SkillMin, null),
                EffectiveLimit.From(m.Edgerunner.SkillMax, o.SkillMax),
                basic, null, null,
                EffectiveLimit.From(m.Edgerunner.StartingCash, null), m.Edgerunner.RoleSkillsOnly.Value),
            _ => new CreationLimits(creation.Method,
                EffectiveLimit.From(m.CompletePackage.StatPoints, o.StatPoints),
                EffectiveLimit.From(m.CompletePackage.StatMin, o.StatMin),
                EffectiveLimit.From(m.CompletePackage.StatMax, o.StatMax),
                EffectiveLimit.From(m.CompletePackage.SkillPoints, o.SkillPoints),
                null,
                EffectiveLimit.From(m.CompletePackage.SkillMax, o.SkillMax),
                basic,
                EffectiveLimit.From(m.CompletePackage.GearBudget, o.GearBudget),
                EffectiveLimit.From(m.CompletePackage.FashionBudget, o.FashionBudget),
                null, RoleSkillsOnly: false),
        };
    }
}

/// <summary>Point costs of character generation.</summary>
public static class CreationCosts
{
    /// <summary>
    /// Skill points spent: every level from 0 times the (x2) multiplier (CP:R p.89–90 examples), minus the free
    /// Cultural Origin levels of the native language (CP:R p.45).
    /// </summary>
    public static int SkillPoints(CharacterRuleBook rules, CharacterSheet sheet)
    {
        var native = NativeLanguageKey(sheet);
        var free = rules.Document.Creation.OriginLanguageLevel.Value;
        var total = 0;
        foreach (var (key, level) in sheet.Skills)
        {
            var def = rules.FindSkill(SkillKey.Parse(key).Id);
            if (def is null) continue;
            var paid = native is not null && key.Equals(native, StringComparison.OrdinalIgnoreCase) ? Math.Max(0, level - free) : level;
            total += paid * rules.SkillCostMultiplier(def);
        }
        return total;
    }

    /// <summary>STAT points spent: the sum of the ten STATs, LUCK included (CP:R p.78).</summary>
    public static int StatPoints(CharacterRuleBook rules, CharacterSheet sheet) =>
        rules.StatOrder.Sum(sheet.Stat);

    /// <summary>
    /// Skill key of the Cultural Origin language, or null when there is none. Streetslang never counts: everyone
    /// speaks it as a Basic Skill and the free levels go to "another primary tongue" (CP:R p.45), so choosing it
    /// can't turn the Basic Skill levels into free ones.
    /// </summary>
    public static string? NativeLanguageKey(CharacterSheet sheet) =>
        sheet.Identity.NativeLanguage is { } lang && !lang.Equals(StreetslangName, StringComparison.OrdinalIgnoreCase)
            ? SkillKey.Make("language", lang)
            : null;

    public const string StreetslangName = "Streetslang";
}
