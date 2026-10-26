using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Npcs.Rules;

/// <summary>
/// A configured value and where it comes from: a book page (<see cref="Source"/>) or a rule adopted by the
/// application (<see cref="Complementary"/>). Never both empty — see <see cref="NpcRuleBook"/> load checks.
/// </summary>
public sealed record Ruled<T>
{
    public T Value { get; init; } = default!;
    public string? Source { get; init; }
    public bool Complementary { get; init; }
    public string? Note { get; init; }
    /// <summary>Only for budgets: no limit at all (Hardened Boss cyberware).</summary>
    public bool Unlimited { get; init; }

    public string Reference => Source ?? (Complementary ? L.T($"Regra complementar da aplicação{(Note is null ? "" : $": {Note}")}", $"Complementary rule of the app{(Note is null ? "" : $": {L.D(Note)}")}") : "");
}

public sealed record IntRange(int Min, int Max)
{
    public bool Contains(int value) => value >= Min && value <= Max;
    public override string ToString() => Min == Max ? $"{Min}" : $"{Min}–{Max}";
}

public sealed record RoleGuideline(int Rank, RolePresence Presence);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RolePresence { Optional, Might, Likely, Has }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WeaponQualityRule { Any, PoorOnly, NoExcellent }

public sealed record HardeningRules
{
    public Ruled<int>? StatPoints { get; init; }
    public Ruled<int>? MaxStat { get; init; }
    public Ruled<int>? GearBudget { get; init; }
    public Ruled<int?>? CyberwareBudget { get; init; }
    public Ruled<RoleGuideline>? RoleRank { get; init; }
}

public sealed record TemplateRules
{
    public NpcLevel Level { get; init; }
    public string Label { get; init; } = "";
    public Ruled<string> Summary { get; init; } = new();
    public Ruled<int> StatPoints { get; init; } = new();
    public Ruled<int> MaxStat { get; init; } = new();
    public Ruled<int> SkillPoints { get; init; } = new();
    public Ruled<int> MaxSkill { get; init; } = new();
    public Ruled<IntRange> SuggestedHp { get; init; } = new();
    public Ruled<IntRange> SuggestedSp { get; init; } = new();
    public Ruled<IntRange> CombatSkillBase { get; init; } = new();
    public Ruled<IntRange> NonCombatSkillBase { get; init; } = new();
    public Ruled<int> GearBudget { get; init; } = new();
    public Ruled<int?> CyberwareBudget { get; init; } = new();
    public Ruled<WeaponQualityRule> WeaponQuality { get; init; } = new();
    public Ruled<RoleGuideline> RoleRank { get; init; } = new();
    public HardeningRules Hardened { get; init; } = new();
}

public sealed record StatRules
{
    public string[] Keys { get; init; } = [];
    public string[] ArmorPenalized { get; init; } = [];
    public Ruled<int> Min { get; init; } = new();
    public Ruled<int> EmpMin { get; init; } = new();
    public Ruled<int> AbsoluteMax { get; init; } = new();
    public Ruled<int> InputMax { get; init; } = new();
    public Ruled<string> Luck { get; init; } = new();
}

public sealed record DerivedRules
{
    public Ruled<int> HpBase { get; init; } = new();
    public Ruled<int> HpPerAverage { get; init; } = new();
    public Ruled<string> SeriouslyWounded { get; init; } = new();
    public Ruled<string> DeathSave { get; init; } = new();
    public Ruled<string> ImplantedLinearFrame { get; init; } = new();
    public Ruled<string> SkillBase { get; init; } = new();
    public Ruled<string> ArmorPenalty { get; init; } = new();
    public Ruled<string> ArmorLayering { get; init; } = new();
}

public sealed record SkillCostRules
{
    public Ruled<int> DoubleCost { get; init; } = new();
    public Ruled<bool> AppliesToNpcBudget { get; init; } = new();
    public Ruled<int> MaxLevelInput { get; init; } = new();
}

public sealed record SkillDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>Portuguese label shown on character sheets; <see cref="Name"/> keeps the book's English name.</summary>
    public string? NamePt { get; init; }
    public string Stat { get; init; } = "";
    public string Category { get; init; } = "";
    public bool Combat { get; init; }
    public bool DoubleCost { get; init; }
    public bool Specialized { get; init; }
    public int Page { get; init; }
}

public sealed record FreeSkillEntry(string Skill, string? Specialization, int Level);

public sealed record FreeSkillRules
{
    public string Source { get; init; } = "";
    public FreeSkillEntry[] Entries { get; init; } = [];
}

public sealed record QualityPriceRow(int Standard, int Poor, int Excellent);

public sealed record WeaponQualityPricing
{
    public string Source { get; init; } = "";
    public QualityPriceRow[] Rows { get; init; } = [];
    public Ruled<string> ExoticRule { get; init; } = new();
}

public sealed record CyberwareStoppingPower(string Slug, int Sp, string Source);

public sealed record ArmorRules
{
    public Ruled<bool> OneArmorPerLocation { get; init; } = new();
    public CyberwareStoppingPower[] CyberwareStoppingPower { get; init; } = [];
}

public sealed record SlotPool
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public CyberwareType[] CyberwareTypes { get; init; } = [];
    public int Slots { get; init; }
    public string Source { get; init; } = "";
}

public sealed record CapacityRule
{
    public string Label { get; init; } = "";
    public BodyLocation? BodyLocation { get; init; }
    public CyberwareType[] CyberwareTypes { get; init; } = [];
    public string[] Slugs { get; init; } = [];
    public bool FoundationalOnly { get; init; }
    public int Max { get; init; }
    public string? Source { get; init; }
    public bool Complementary { get; init; }
    public string? Note { get; init; }
    public string Reference => Source ?? L.T($"Regra complementar da aplicação{(Note is null ? "" : $": {Note}")}", $"Complementary rule of the app{(Note is null ? "" : $": {L.D(Note)}")}");
}

public sealed record ExclusiveGroup(string Label, string[] Slugs, int Max, string Source);

public sealed record CyberwareOverride(string Slug, int? SlotsRequired, int? SlotsProvided, string Source, string? Note);

public sealed record CyberwareRules
{
    public Ruled<int> OptionSlotDefault { get; init; } = new();
    public Ruled<bool> InstallCostIncluded { get; init; } = new();
    public Ruled<string> StackingNote { get; init; } = new();
    public SlotPool[] Pools { get; init; } = [];
    public CapacityRule[] Capacity { get; init; } = [];
    public ExclusiveGroup[] ExclusiveGroups { get; init; } = [];
    public CyberwareOverride[] Overrides { get; init; } = [];
    public Ruled<bool> AutoPickExcludesFreeTextRequirements { get; init; } = new();
}

public sealed record GeneratorRules
{
    public Ruled<int> MaxAttempts { get; init; } = new();
    public Ruled<IntRangeF> CyberwareSpendRange { get; init; } = new();
    public Ruled<int> UnlimitedCyberwareReference { get; init; } = new();
    public Ruled<double> SecondaryWeaponChance { get; init; } = new();
    public Ruled<double> GearSpendFloor { get; init; } = new();
    public Ruled<double> CoreBookWeight { get; init; } = new();
    public Ruled<double> ExoticWeaponWeight { get; init; } = new();
    /// <summary>Chance of taking the handle from the gang's own list instead of the general one.</summary>
    public Ruled<double> GangHandleChance { get; init; } = new();
    public string[] Languages { get; init; } = [];
    public string[] Homes { get; init; } = [];
    public string[] Handles { get; init; } = [];
}

public sealed record IntRangeF(double Min, double Max);

public sealed record WeaponPreference
{
    public WeaponSkill[] Skills { get; init; } = [];
    public WeaponClass[] Classes { get; init; } = [];
}

public sealed record ProfileWeapons
{
    public WeaponPreference? Primary { get; init; }
    public WeaponPreference? Secondary { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArmorStyle { Light, Medium, Heavy }

public sealed record NpcProfile
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public string? Role { get; init; }
    public Dictionary<string, double> StatWeights { get; init; } = [];
    public string[] KeyCombatSkills { get; init; } = [];
    public string[] KeyNonCombatSkills { get; init; } = [];
    public Dictionary<string, double> SecondarySkills { get; init; } = [];
    public ProfileWeapons Weapons { get; init; } = new();
    public ArmorStyle ArmorStyle { get; init; }
    public Dictionary<string, double> Cyberware { get; init; } = [];
    public Dictionary<string, double> Gear { get; init; } = [];
    /// <summary>Gear of which the generator buys at most one, first (a Netrunner's Cyberdeck, a Tech's bag).</summary>
    public string[] SignatureGear { get; init; } = [];
    /// <summary>Implants installed first when the budget allows (a Netrunner's Neural Link and Interface Plugs).</summary>
    public string[] SignatureCyberware { get; init; } = [];
}

/// <summary>
/// How a gang bends a profile. Weights are added to the profile's own (STAT weights are multiplied); weapon
/// classes the gang favors move to the front when the profile's weapon skill can use them. All of it is the
/// application's reading of the gang's description, so the whole block is complementary.
/// </summary>
public sealed record GangBias
{
    /// <summary>Weights of the profiles drawn when the profile is left random.</summary>
    public Dictionary<string, double> Profiles { get; init; } = [];
    public Dictionary<string, double> StatWeights { get; init; } = [];
    public Dictionary<string, double> SecondarySkills { get; init; } = [];
    public Dictionary<string, double> Cyberware { get; init; } = [];
    public Dictionary<string, double> Gear { get; init; } = [];
    public WeaponClass[] WeaponClasses { get; init; } = [];
    public WeaponClass[] ForbiddenWeaponClasses { get; init; } = [];
    public ArmorStyle? ArmorStyle { get; init; }
    /// <summary>Replaces <see cref="GeneratorRules.CyberwareSpendRange"/> for this gang.</summary>
    public IntRangeF? CyberwareSpendRange { get; init; }
    /// <summary>Never picked by the generator for this gang; the user's required implants still win.</summary>
    public string[] ForbiddenCyberware { get; init; } = [];
    public bool AllCyberwareForbidden { get; init; }
}

/// <summary>A Night City gang laid over a profile: identity, look and the gear it favors.</summary>
public sealed record NpcGang
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    /// <summary>"2045" (Time of the Red) or "2077" (Edgerunners Mission Kit).</summary>
    public string Era { get; init; } = "";
    public string Kind { get; init; } = "";
    public Ruled<string> Description { get; init; } = new();
    public Ruled<string> Aesthetic { get; init; } = new();
    /// <summary>Districts from <see cref="GeneratorRules.Homes"/>; empty means the gang has no fixed turf.</summary>
    public Ruled<string[]> Territory { get; init; } = new();
    /// <summary>Native language weights; empty keeps the general list.</summary>
    public Ruled<Dictionary<string, double>> Languages { get; init; } = new();
    /// <summary>Themed handles, mixed with the general list.</summary>
    public Ruled<string[]> Handles { get; init; } = new();
    /// <summary>What the books say the gang carries (encounter tables, member stat blocks).</summary>
    public Ruled<string>[] Loadout { get; init; } = [];
    public Ruled<GangBias> Bias { get; init; } = new();
}

public sealed record RolePresenceRule(string Label, double Probability, bool Complementary);

public sealed record EmpGuideline
{
    public Ruled<int> PiecesPerPoint { get; init; } = new();
}

/// <summary>Root of <c>npc-rules.json</c>.</summary>
public sealed record NpcRulesDocument
{
    public string About { get; init; } = "";
    public int Version { get; init; }
    public Dictionary<string, string> Books { get; init; } = [];
    public StatRules Stats { get; init; } = new();
    public DerivedRules Derived { get; init; } = new();
    public SkillCostRules SkillCost { get; init; } = new();
    public TemplateRules[] Templates { get; init; } = [];
    public Ruled<string>[] HardenedGuidance { get; init; } = [];
    public Dictionary<RolePresence, RolePresenceRule> RolePresence { get; init; } = [];
    public string[] Roles { get; init; } = [];
    public EmpGuideline EmpGuideline { get; init; } = new();
    public FreeSkillRules FreeSkills { get; init; } = new();
    public SkillDefinition[] Skills { get; init; } = [];
    public Dictionary<WeaponSkill, string> WeaponSkillMap { get; init; } = [];
    public WeaponQualityPricing WeaponQualityPricing { get; init; } = new();
    public ArmorRules Armor { get; init; } = new();
    public CyberwareRules Cyberware { get; init; } = new();
    public GeneratorRules Generator { get; init; } = new();
    public NpcProfile[] Profiles { get; init; } = [];
    public NpcGang[] Gangs { get; init; } = [];

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
}
