using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Rules;

// Records for character-rules.json. Values the engine reads are Ruled<T> (with the book page); plain tables keep
// their page in Source.

public sealed record CharacterRulesDocument
{
    public string About { get; init; } = "";
    public int Version { get; init; }
    public Dictionary<string, string> Books { get; init; } = [];
    // sheet order (CP:R p.73)
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

// creation

public sealed record CreationRules
{
    public Ruled<int> RoleAbilityStartRank { get; init; } = new();
    public Ruled<int> OriginLanguageLevel { get; init; } = new();
    public Ruled<bool> OriginLanguageFree { get; init; } = new();
    // @home = the character's home
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

// roles

public sealed record RoleRules
{
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
    public string Source { get; init; } = "";
    // PT summary of the pages in Source
    public string Summary { get; init; } = "";
    public string[] Points { get; init; } = [];
    // null when the rank isn't split
    public RoleSplitRule? Split { get; init; }
    public RoleSkillBonus[] SkillBonuses { get; init; } = [];
}

// Max is "rank" or "2xrank", PerKeyMax "rank" or null. Allowed limits some keys (Fumble Recovery: 0 or 4).
public sealed record RoleSplitRule
{
    public string Source { get; init; } = "";
    public RoleSplitKey[] Keys { get; init; } = [];
    public string Max { get; init; } = "rank";
    public string? PerKeyMax { get; init; }
    public Dictionary<string, int[]> Allowed { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public int TotalFor(int rank) => Max == "2xrank" ? rank * 2 : rank;
}

public sealed record RoleSplitKey(string Id, string Label, string? LabelPt);

// from: "rank" or "split:field"
public sealed record RoleSkillBonus
{
    public string[] Skills { get; init; } = [];
    public string From { get; init; } = "rank";
    public string Label { get; init; } = "";
    public string Source { get; init; } = "";
}

// CP:R p.74-77, STATs in statOrder
public sealed record StatTable
{
    public string Source { get; init; } = "";
    public int[][] Rows { get; init; } = [];
}

// Streetrat levels (CP:R p.86-87) and the Edgerunner list (p.88-89). @choose = player picks (instrument, science).
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

// Each group lists alternatives ("Shotgun or Assault Rifle"); pick one option per group.
public sealed record StartingSection
{
    public string Source { get; init; } = "";
    // CP:R p.117, cyberware section only
    public int? HumanityLoss { get; init; }
    public StartingItem[][][] Groups { get; init; } = [];
}

public sealed record StartingItem
{
    public string Slug { get; init; } = "";
    public int Qty { get; init; } = 1;
    public Caliber? Caliber { get; init; }
    // catalog prices packs of 10
    public int? Rounds { get; init; }
    public ArmorSlot? Worn { get; init; }
    // CP:R p.115
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
    // "1d6", "1d10" or "choose"
    public string Dice { get; init; } = "";
    public string[] Options { get; init; } = [];
    // only asked when another answer has this value
    public LifepathCondition? ShowIf { get; init; }
}

public sealed record LifepathCondition(string Question, string Value);

// lifepath

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

// derived values

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

// therapy

public sealed record TherapyRules
{
    // clinic (full price), Medtech (materials only) or free (GM)
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

// combat

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
    public bool DeathSave { get; init; }
    public int? MovePenalty { get; init; }
    public int? ActionPenalty { get; init; }
    public bool QuickFixHeals { get; init; }
}

// cyberware, progression, economy

public sealed record CharacterCyberwareRules
{
    public Ruled<string[]> MeatArmOptions { get; init; } = new();
    public Ruled<string[]> MeatLegOptions { get; init; } = new();
    public Ruled<MeatLimbs> MeatLimbs { get; init; } = new();
    // coverings and Hardened Shielding (CP:R p.116)
    public Ruled<AnyLimbOption[]> AnyLimbOptions { get; init; } = new();
    public Ruled<bool> OneCoveringPerLimb { get; init; } = new();
    public Ruled<FbcRule> Fbc { get; init; } = new();
    public Ruled<bool> RemovalRestoresHumanity { get; init; } = new();
    public Ruled<bool> RemovedPiecesKept { get; init; } = new();
    // Smart Glasses, Battleglove, Smart Ears...
    public Ruled<WearableHost[]> Wearables { get; init; } = new();
    public Ruled<int> WearableHumanityLoss { get; init; } = new();
    // IR5 p.99
    public Ruled<string[]> FingerOptions { get; init; } = new();
}

public sealed record AnyLimbOption(string Slug, bool Covering);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WearableAccepts { Cybereye, Cyberarm, Cyberaudio }

public sealed record WearableHost
{
    public string Slug { get; init; } = "";
    public WearableAccepts Accepts { get; init; }
    public int Slots { get; init; }
    // e.g. Smart Glasses
    public bool PairedAsOne { get; init; }
    // e.g. Smart Lens
    public bool NoPaired { get; init; }
    // Dynalar Xtra-Dex
    public int Cyberfingers { get; init; }
    public string[] BuiltIn { get; init; } = [];
    public bool BuiltInUsesSlots { get; init; }
    public bool CoversArm { get; init; }
    public bool FingersStillWork { get; init; }
    public int? MaxWorn { get; init; }
    public string? Requirement { get; init; }
    public string Source { get; init; } = "";
}

// Interface RED 3 p.93
public sealed record FbcRule
{
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
    // Exec Teamwork, can't be rented
    public bool Corporate { get; init; }
}

// cyberware effects on the sheet

public sealed record CyberwareEffectsRules
{
    public string About { get; init; } = "";
    public CyberwareEffect[] Effects { get; init; } = [];
}

// STAT bonus, skill bonus (only a note when Condition limits it) or a text-only effect.
public sealed record CyberwareEffect
{
    public string[] Items { get; init; } = [];
    // e.g. Chemskin + Techhair
    public string[] AllOf { get; init; } = [];
    // three Light Tattoos
    public int MinCount { get; init; } = 1;
    public string? Stat { get; init; }
    public string[] Skills { get; init; } = [];
    public int Add { get; init; }
    public bool Stacks { get; init; }
    public int? Max { get; init; }
    public string? Condition { get; init; }
    public string? Note { get; init; }
    public string Source { get; init; } = "";
}
