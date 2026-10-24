using System.Text.Json;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Npcs.Rules;

/// <summary>
/// Effective construction parameters for one level, with Hardening already applied.
/// <see cref="CyberwareBudget"/> null means unlimited (Hardened Boss).
/// </summary>
public sealed record NpcTemplate(
    NpcLevel Level,
    bool Hardened,
    string Label,
    Ruled<int> StatPoints,
    Ruled<int> MaxStat,
    Ruled<int> SkillPoints,
    Ruled<int> MaxSkill,
    Ruled<IntRange> SuggestedHp,
    Ruled<IntRange> SuggestedSp,
    Ruled<IntRange> CombatSkillBase,
    Ruled<IntRange> NonCombatSkillBase,
    Ruled<int> GearBudget,
    Ruled<int?> CyberwareBudget,
    Ruled<WeaponQualityRule> WeaponQuality,
    Ruled<RoleGuideline> RoleRank,
    Ruled<RoleGuideline>? HardenedRoleRank)
{
    public string DisplayName => Hardened ? $"{Label} Hardened" : Label;

    /// <summary>Highest Role Rank the guidelines mention for this template.</summary>
    public int MaxRoleRank => Math.Max(RoleRank.Value.Rank, HardenedRoleRank?.Value.Rank ?? 0);
}

/// <summary>The loaded <c>npc-rules.json</c>: templates, skills, profiles, gangs and cyberware rules. Singleton.</summary>
public sealed class NpcRuleBook
{
    private const string ResourceName = "CyberpunkDb.Application.Npcs.Rules.npc-rules.json";

    private readonly Dictionary<string, SkillDefinition> _skills;
    private readonly Dictionary<string, NpcProfile> _profiles;
    private readonly Dictionary<string, NpcGang> _gangs;
    private readonly Dictionary<string, CyberwareOverride> _overrides;
    private readonly Dictionary<string, int> _cyberSp;

    public NpcRuleBook(NpcRulesDocument document)
    {
        Document = document;
        _skills = document.Skills.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
        _profiles = document.Profiles.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        _gangs = document.Gangs.ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);
        _overrides = document.Cyberware.Overrides.ToDictionary(o => o.Slug, StringComparer.OrdinalIgnoreCase);
        _cyberSp = document.Armor.CyberwareStoppingPower.ToDictionary(c => c.Slug, c => c.Sp, StringComparer.OrdinalIgnoreCase);
        StatKeys = document.Stats.Keys;
    }

    public NpcRulesDocument Document { get; }
    public IReadOnlyList<string> StatKeys { get; }
    public IReadOnlyList<SkillDefinition> Skills => Document.Skills;
    public IReadOnlyList<NpcProfile> Profiles => Document.Profiles;
    public IReadOnlyList<NpcGang> Gangs => Document.Gangs;

    public static NpcRuleBook LoadEmbedded()
    {
        using var stream = typeof(NpcRuleBook).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"Recurso {ResourceName} não encontrado.");
        return new NpcRuleBook(JsonSerializer.Deserialize<NpcRulesDocument>(stream, NpcRulesDocument.JsonOptions)
                               ?? throw new InvalidOperationException("npc-rules.json vazio."));
    }

    public SkillDefinition? FindSkill(string id) => _skills.GetValueOrDefault(id);
    public NpcProfile? FindProfile(string? id) => id is null ? null : _profiles.GetValueOrDefault(id);
    public NpcGang? FindGang(string? id) => id is null ? null : _gangs.GetValueOrDefault(id);
    public CyberwareOverride? FindOverride(string slug) => _overrides.GetValueOrDefault(slug);
    public int? CyberwareStoppingPower(string slug) => _cyberSp.TryGetValue(slug, out var sp) ? sp : null;

    public int SkillCostMultiplier(SkillDefinition skill) =>
        skill.DoubleCost && Document.SkillCost.AppliesToNpcBudget.Value ? Document.SkillCost.DoubleCost.Value : 1;

    public string? SkillIdFor(WeaponSkill skill) => Document.WeaponSkillMap.GetValueOrDefault(skill);

    /// <summary>Free skill levels of DGD p.151, resolved for this NPC's native language and home.</summary>
    public Dictionary<string, int> FreeSkillLevels(string nativeLanguage, string home)
    {
        var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Document.FreeSkills.Entries)
        {
            var spec = entry.Specialization switch
            {
                "@native" => nativeLanguage,
                "@home" => home,
                var s => s,
            };
            var key = SkillKey.Make(entry.Skill, spec);
            levels[key] = Math.Max(levels.GetValueOrDefault(key), entry.Level);
        }
        return levels;
    }

    public NpcTemplate Template(NpcLevel level, bool hardened)
    {
        var t = Document.Templates.FirstOrDefault(x => x.Level == level)
                ?? throw new InvalidOperationException($"Template {level} ausente em npc-rules.json.");
        if (!hardened)
            return new NpcTemplate(level, false, t.Label, t.StatPoints, t.MaxStat, t.SkillPoints, t.MaxSkill,
                t.SuggestedHp, t.SuggestedSp, t.CombatSkillBase, t.NonCombatSkillBase, t.GearBudget, t.CyberwareBudget,
                t.WeaponQuality, t.RoleRank, null);

        var h = t.Hardened;
        var cyber = h.CyberwareBudget is { Unlimited: true } u
            ? new Ruled<int?> { Value = null, Unlimited = true, Source = u.Source }
            : Add(t.CyberwareBudget, h.CyberwareBudget);
        return new NpcTemplate(level, true, t.Label,
            Add(t.StatPoints, h.StatPoints),
            h.MaxStat ?? t.MaxStat,
            t.SkillPoints, t.MaxSkill, t.SuggestedHp, t.SuggestedSp, t.CombatSkillBase, t.NonCombatSkillBase,
            Add(t.GearBudget, h.GearBudget),
            cyber,
            t.WeaponQuality, t.RoleRank, h.RoleRank);
    }

    public IEnumerable<NpcTemplate> AllTemplates() =>
        Document.Templates.SelectMany(t => new[] { Template(t.Level, false), Template(t.Level, true) });

    private static Ruled<int> Add(Ruled<int> baseValue, Ruled<int>? delta) => delta is null
        ? baseValue
        : new Ruled<int> { Value = baseValue.Value + delta.Value, Source = $"{baseValue.Source} + Hardened: {delta.Source}" };

    private static Ruled<int?> Add(Ruled<int?> baseValue, Ruled<int?>? delta) => delta is null || baseValue.Value is null
        ? baseValue
        : new Ruled<int?> { Value = baseValue.Value + (delta.Value ?? 0), Source = $"{baseValue.Source} + Hardened: {delta.Source}" };
}
