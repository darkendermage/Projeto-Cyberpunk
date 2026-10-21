using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Rules;

// Records mirroring character-rules.json. Every value that the rules engine reads is a Ruled<T> with a
// book page or the complementary mark; plain tables carry their page in a Source string.

/// <summary>Root of <c>character-rules.json</c>.</summary>
public sealed record CharacterRulesDocument
{
    public string About { get; init; } = "";
    public int Version { get; init; }
    public Dictionary<string, string> Books { get; init; } = [];
    /// <summary>The ten STATs in Character Sheet order (CP:R p.73); stat-table rows use this order.</summary>
    public string[] StatOrder { get; init; } = [];
    public CreationRules Creation { get; init; } = new();
    public RoleRules[] Roles { get; init; } = [];
    public LifepathRules Lifepath { get; init; } = new();
    public CharacterDerivedRules Derived { get; init; } = new();
    public TherapyRules Therapy { get; init; } = new();
    public CombatRules Combat { get; init; } = new();
    public CharacterCyberwareRules Cyberware { get; init; } = new();
    public CyberwareEffectsRules CyberwareEffects { get; init; } = new();
    public ProgressionRules Progression { get; init; } = new();
    public EconomyRules Economy { get; init; } = new();

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
}

// ------------------------------------------------------------ creation

public sealed record CreationRules
{
    public Ruled<int> RoleAbilityStartRank { get; init; } = new();
    public Ruled<int> OriginLanguageLevel { get; init; } = new();
    public Ruled<bool> OriginLanguageFree { get; init; } = new();
    /// <summary>Skill keys; <c>@home</c> resolves to the character's home.</summary>
    public Ruled<string[]> BasicSkills { get; init; } = new();
    public Ruled<int> BasicSkillMin { get; init; } = new();
    public Ruled<bool> MulticlassAtCreation { get; init; } = new();
    public CreationMethodRules Methods { get; init; } = new();
    public Ruled<bool> SkillCostCountsFromZero { get; init; } = new();
    public Ruled<string> HumanityLossAtCreation { get; init; } = new();
    public StartingHousingRules StartingHousing { get; init; } = new();
}

public sealed record CreationMethodRules
{
    public StreetratRules Streetrat { get; init; } = new();
    public EdgerunnerRules Edgerunner { get; init; } = new();
    public CompletePackageRules CompletePackage { get; init; } = new();
}

public sealed record StreetratRules
{
    public string Label { get; init; } = "";
    public Ruled<string> Summary { get; init; } = new();
    public Ruled<string> Stats { get; init; } = new();
    public Ruled<string> Skills { get; init; } = new();
    public Ruled<string> Gear { get; init; } = new();
    public Ruled<int> StartingCash { get; init; } = new();
}

public sealed record EdgerunnerRules
{
    public string Label { get; init; } = "";
    public Ruled<string> Summary { get; init; } = new();
    public Ruled<string> Stats { get; init; } = new();
    public Ruled<int> SkillPoints { get; init; } = new();
    public Ruled<int> SkillMin { get; init; } = new();
    public Ruled<int> SkillMax { get; init; } = new();
    public Ruled<bool> RoleSkillsOnly { get; init; } = new();
    public Ruled<string> Gear { get; init; } = new();
    public Ruled<int> StartingCash { get; init; } = new();
}

public sealed record CompletePackageRules
{
    public string Label { get; init; } = "";
    public Ruled<string> Summary { get; init; } = new();
    public Ruled<int> StatPoints { get; init; } = new();
    public Ruled<StatPreset[]> StatPresets { get; init; } = new();
    public Ruled<int> StatMin { get; init; } = new();
    public Ruled<int> StatMax { get; init; } = new();
    public Ruled<int> SkillPoints { get; init; } = new();
    public Ruled<int> SkillMax { get; init; } = new();
    public Ruled<int> GearBudget { get; init; } = new();
    public Ruled<bool> UnspentGearKept { get; init; } = new();
    public Ruled<int> FashionBudget { get; init; } = new();
    public Ruled<bool> UnspentFashionKept { get; init; } = new();
}

public sealed record StatPreset(string Label, int Points);

public sealed record StartingHousingRules
{
    public StartingHousing Default { get; init; } = new();
    public StartingHousing Exec { get; init; } = new();
}

public sealed record StartingHousing
{
    public string Housing { get; init; } = "";
    public string Lifestyle { get; init; } = "";
    public bool FirstMonthFree { get; init; }
    public string Source { get; init; } = "";
}

// ------------------------------------------------------------ roles

public sealed record RoleRules
{
    /// <summary>Role name as used everywhere else ("Media", "Solo"...).</summary>
    public string Id { get; init; } = "";
    public RoleAbilityInfo Ability { get; init; } = new();
    public StatTable StatTable { get; init; } = new();
    public RoleSkills Skills { get; init; } = new();
    public StartingPackage Starting { get; init; } = new();
    public RoleLifepath Lifepath { get; init; } = new();
}

public sealed record RoleAbilityInfo
{
    public string Name { get; init; } = "";
    public int Page { get; init; }
    /// <summary>Pages summarized below (the ability can span several).</summary>
    public string Source { get; init; } = "";
    /// <summary>What the ability does, summarized in Portuguese from the pages in <see cref="Source"/>.</summary>
    public string Summary { get; init; } = "";
    public string[] Points { get; init; } = [];
}

/// <summary>1d10 template table (CP:R p.74–77); each row lists the ten STATs in <c>statOrder</c>.</summary>
public sealed record StatTable
{
    public string Source { get; init; } = "";
    public int[][] Rows { get; init; } = [];
}

/// <summary>
/// The Role's 20 skills: the Streetrat levels (CP:R p.86–87) and, by their keys, the Edgerunner list (p.88–89).
/// <c>@home</c> is the character's home; <c>@choose</c> is a specialization the player picks (instrument, science).
/// </summary>
public sealed record RoleSkills
{
    public string Source { get; init; } = "";
    public Dictionary<string, int> Levels { get; init; } = [];
}

public sealed record StartingPackage
{
    public StartingSection Weapons { get; init; } = new();
    public StartingSection Outfit { get; init; } = new();
    public StartingSection Cyberware { get; init; } = new();
}

/// <summary>
/// Groups of starting items. Each group lists alternatives ("Shotgun or Assault Rifle"); the player picks exactly
/// one option per group, and an option may bundle several items.
/// </summary>
public sealed record StartingSection
{
    public string Source { get; init; } = "";
    /// <summary>Preset Humanity Loss of the whole Role package (CP:R p.117); null outside the cyberware section.</summary>
    public int? HumanityLoss { get; init; }
    public StartingItem[][][] Groups { get; init; } = [];
}

public sealed record StartingItem
{
    public string Slug { get; init; } = "";
    public int Qty { get; init; } = 1;
    /// <summary>Generic ammunition: which caliber the rounds are.</summary>
    public Caliber? Caliber { get; init; }
    /// <summary>Generic ammunition: number of rounds (the catalog prices packs of 10).</summary>
    public int? Rounds { get; init; }
    /// <summary>Armor that starts worn on this location.</summary>
    public ArmorSlot? Worn { get; init; }
    /// <summary>Arm option installed as the only piece in a meat arm (CP:R p.115).</summary>
    public bool MeatArm { get; init; }
}

public sealed record RoleLifepath
{
    public string Source { get; init; } = "";
    public LifepathQuestion[] Questions { get; init; } = [];
}

public sealed record LifepathQuestion
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string LabelPt { get; init; } = "";
    /// <summary>"1d6", "1d10" or "choose" (no roll).</summary>
    public string Dice { get; init; } = "";
    public string[] Options { get; init; } = [];
    /// <summary>Branch of the Role flowchart: only asked when another answer has this value.</summary>
    public LifepathCondition? ShowIf { get; init; }
}

public sealed record LifepathCondition(string Question, string Value);

// ------------------------------------------------------------ lifepath

public sealed record LifepathRules
{
    public string Source { get; init; } = "";
    public CulturalOrigin[] CulturalOrigins { get; init; } = [];
    public Ruled<bool> CustomLanguageAllowed { get; init; } = new();
    public LifepathTable[] Tables { get; init; } = [];
    public FriendRules Friends { get; init; } = new();
    public EnemyRules Enemies { get; init; } = new();
    public LoveAffairRules TragicLoveAffairs { get; init; } = new();
}

public sealed record CulturalOrigin(string Region, string[] Languages);

public sealed record LifepathTable
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string LabelPt { get; init; } = "";
    public string Dice { get; init; } = "";
    public int Page { get; init; }
    public string[] Options { get; init; } = [];
}

public sealed record FriendRules
{
    public Ruled<string> Count { get; init; } = new();
    public string[] Relationship { get; init; } = [];
}

public sealed record EnemyRules
{
    public Ruled<string> Count { get; init; } = new();
    public string[] Who { get; init; } = [];
    public string[] Cause { get; init; } = [];
    public string[] Resources { get; init; } = [];
    public RevengeTable Revenge { get; init; } = new();
}

public sealed record RevengeTable
{
    public string Source { get; init; } = "";
    public string[] Options { get; init; } = [];
}

public sealed record LoveAffairRules
{
    public Ruled<string> Count { get; init; } = new();
    public string[] Outcome { get; init; } = [];
}

// ------------------------------------------------------------ derived values

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ThresholdComparison { LessThan, LessThanOrEqual }

public sealed record CharacterDerivedRules
{
    public Ruled<SeriouslyWoundedRule> SeriouslyWounded { get; init; } = new();
    public Ruled<MortallyWoundedRule> MortallyWounded { get; init; } = new();
    public Ruled<bool> WoundStatesReplace { get; init; } = new();
    public Ruled<StabilizationDvs> StabilizationDv { get; init; } = new();
    public Ruled<DeathSaveRule> DeathSave { get; init; } = new();
    public Ruled<int> HumanityPerEmp { get; init; } = new();
    public Ruled<string> EmpFromHumanity { get; init; } = new();
    public Ruled<int> CyberpsychosisBelow { get; init; } = new();
}

public sealed record SeriouslyWoundedRule
{
    public double Fraction { get; init; } = 0.5;
    public bool RoundUp { get; init; } = true;
    public ThresholdComparison Comparison { get; init; }
    public int ActionPenalty { get; init; }
}

public sealed record MortallyWoundedRule
{
    public int BelowHp { get; init; } = 1;
    public int ActionPenalty { get; init; }
    public int MovePenalty { get; init; }
    public int MoveMinimum { get; init; } = 1;
}

public sealed record StabilizationDvs(int Lightly, int Seriously, int Mortally);

public sealed record DeathSaveRule
{
    public string Stat { get; init; } = "BODY";
    public string Die { get; init; } = "1d10";
    public string SucceedsIf { get; init; } = "";
    public bool NaturalTenFails { get; init; }
    public int PenaltyPerSave { get; init; }
    public int PenaltyPerDamageWhileMortal { get; init; }
    public bool ResetsOnStabilization { get; init; }
}

// ------------------------------------------------------------ therapy

public sealed record TherapyRules
{
    /// <summary>How a therapy is paid: at a clinic (full price), by a Medtech (materials only) or free (GM ruling).</summary>
    public Ruled<string[]> Payment { get; init; } = new();
    public Ruled<int> MaxHumanityPerPiece { get; init; } = new();
    public Ruled<int> MaxHumanityPerBorgware { get; init; } = new();
    public Ruled<bool> ZeroLossPiecesExempt { get; init; } = new();
    public Ruled<int> DurationWeeks { get; init; } = new();
    public Ruled<bool> MedtechCannotSelfTreat { get; init; } = new();
    public Ruled<int> HospitalNight { get; init; } = new();
    public TherapyKind[] Kinds { get; init; } = [];
}

public sealed record TherapyKind
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public int Cost { get; init; }
    public string Recovery { get; init; } = "";
    public int Dv { get; init; }
    public int Materials { get; init; }
    public string Source { get; init; } = "";
}

// ------------------------------------------------------------ combat

public sealed record CombatRules
{
    public Ruled<int> AblationPerDamagingHit { get; init; } = new();
    public Ruled<bool> SpDoesNotStack { get; init; } = new();
    public Ruled<bool> ArmorPenaltyOnce { get; init; } = new();
    public Ruled<int> AttachmentSlots { get; init; } = new();
    public Ruled<int> ShieldHp { get; init; } = new();
    public Ruled<AutofireRule> Autofire { get; init; } = new();
    public AutofireDvTable AutofireDv { get; init; } = new();
    public Ruled<SuppressiveFireRule> SuppressiveFire { get; init; } = new();
    public Ruled<CriticalInjuryRule> CriticalInjury { get; init; } = new();
    public Ruled<string> QuickFix { get; init; } = new();
    public Ruled<string> Treatment { get; init; } = new();
    public Ruled<bool> CybertechOnCyberware { get; init; } = new();
    public CriticalInjuryTables CriticalInjuries { get; init; } = new();
}

public sealed record AutofireRule
{
    public int Bullets { get; init; }
    public string Damage { get; init; } = "";
    public string Multiplier { get; init; } = "";
    public bool NoAimedShot { get; init; }
}

public sealed record AutofireDvTable
{
    public AutofireDvRow[] Value { get; init; } = [];
    public string[] Bands { get; init; } = [];
    public string Source { get; init; } = "";
}

public sealed record AutofireDvRow(WeaponClass WeaponClass, int[] Dv);

public sealed record SuppressiveFireRule
{
    public int Bullets { get; init; }
    public int RangeMeters { get; init; }
    public string Check { get; init; } = "";
}

public sealed record CriticalInjuryRule
{
    public string Trigger { get; init; } = "";
    public int BonusDamage { get; init; }
    public bool BypassesArmor { get; init; }
}

public sealed record CriticalInjuryTables
{
    public string Source { get; init; } = "";
    public CriticalInjuryDefinition[] Body { get; init; } = [];
    public CriticalInjuryDefinition[] Head { get; init; } = [];
}

public sealed record CriticalInjuryDefinition
{
    public int Roll { get; init; }
    public string Name { get; init; } = "";
    public string Effect { get; init; } = "";
    public string? QuickFix { get; init; }
    public string Treatment { get; init; } = "";
    /// <summary>Raises the Base Death Save Penalty by 1 while active.</summary>
    public bool DeathSave { get; init; }
    public int? MovePenalty { get; init; }
    public int? ActionPenalty { get; init; }
    /// <summary>The Quick Fix removes the injury for good.</summary>
    public bool QuickFixHeals { get; init; }
}

// ------------------------------------------------------------ cyberware, progression, economy

public sealed record CharacterCyberwareRules
{
    public Ruled<string[]> MeatArmOptions { get; init; } = new();
    public Ruled<string[]> MeatLegOptions { get; init; } = new();
    public Ruled<MeatLimbs> MeatLimbs { get; init; } = new();
    /// <summary>Limb options that go on a Cyberarm or a Cyberleg (CP:R p.116): coverings and Hardened Shielding.</summary>
    public Ruled<AnyLimbOption[]> AnyLimbOptions { get; init; } = new();
    public Ruled<bool> OneCoveringPerLimb { get; init; } = new();
    public Ruled<FbcRule> Fbc { get; init; } = new();
    public Ruled<bool> RemovalRestoresHumanity { get; init; } = new();
    public Ruled<bool> RemovedPiecesKept { get; init; } = new();
}

public sealed record AnyLimbOption(string Slug, bool Covering);

/// <summary>Full Body Conversion exceptions (Interface RED 3 p.93).</summary>
public sealed record FbcRule
{
    /// <summary>An FBC body is Borgware whose requirement names this piece.</summary>
    public string BodyRequirement { get; init; } = "";
    public string[] ExemptSlugs { get; init; } = [];
    public int SlotMultiplier { get; init; } = 1;
}

public sealed record MeatLimbs(int Arms, int Legs);

public sealed record ProgressionRules
{
    public Ruled<int> IpSkillPerLevel { get; init; } = new();
    public Ruled<int> IpDoubleSkillPerLevel { get; init; } = new();
    public Ruled<int> IpRolePerRank { get; init; } = new();
    public Ruled<int> MaxRank { get; init; } = new();
    public Ruled<int> MulticlassMinRank { get; init; } = new();
    public Ruled<int> MulticlassNewRoleRank { get; init; } = new();
    public Ruled<bool> RoleLifepathPerRole { get; init; } = new();
}

public sealed record EconomyRules
{
    public LifestyleTable Lifestyles { get; init; } = new();
    public HousingTable Housing { get; init; } = new();
    public Ruled<int> LifestyleGraceDays { get; init; } = new();
}

public sealed record LifestyleTable
{
    public string Source { get; init; } = "";
    public LifestyleOption[] Options { get; init; } = [];
}

public sealed record LifestyleOption(string Id, string Label, int Monthly);

public sealed record HousingTable
{
    public string Source { get; init; } = "";
    public HousingOption[] Options { get; init; } = [];
}

public sealed record HousingOption
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public int? Rent { get; init; }
    public int? Buy { get; init; }
    /// <summary>Only given by a Corporation (Exec Teamwork); cannot be rented.</summary>
    public bool Corporate { get; init; }
}

// ------------------------------------------------------------ cyberware effects on the sheet

public sealed record CyberwareEffectsRules
{
    public string About { get; init; } = "";
    public CyberwareEffect[] Effects { get; init; } = [];
}

/// <summary>
/// What an installed piece does to the sheet: a STAT bonus (<see cref="Stat"/>), a skill bonus (<see cref="Skills"/>,
/// only shown as a note when <see cref="Condition"/> limits it to a situation) or a game effect shown as text.
/// </summary>
public sealed record CyberwareEffect
{
    /// <summary>Catalog slugs; any one of them installed triggers the effect.</summary>
    public string[] Items { get; init; } = [];
    /// <summary>Catalog slugs that must all be installed (e.g. Chemskin and Techhair).</summary>
    public string[] AllOf { get; init; } = [];
    /// <summary>Installations needed (three Light Tattoos).</summary>
    public int MinCount { get; init; } = 1;
    public string? Stat { get; init; }
    public string[] Skills { get; init; } = [];
    public int Add { get; init; }
    /// <summary>Each installation adds again; otherwise more installations give no extra benefit.</summary>
    public bool Stacks { get; init; }
    /// <summary>The STAT can't be raised above this.</summary>
    public int? Max { get; init; }
    public string? Condition { get; init; }
    public string? Note { get; init; }
    public string Source { get; init; } = "";
}
