using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters;

// Writes carry the Version the client last saw, so a stale tab gets a 409 instead of overwriting.

public sealed record CharacterSummaryDto(
    int Id, string Name, string? Handle, string? Role, int? RoleRank, CreationMethod Method, CharacterStatus Status,
    int ErrorCount, int WarningCount, int MaxHp, int CurrentHp, int Humanity, int HumanityMax, int Balance,
    bool HasPortrait, DateTime CreatedAt, DateTime UpdatedAt, PortraitFocus? PortraitFocus = null);

public sealed record CharacterDetailDto(
    int Id, int Version, CharacterStatus Status, bool HasPortrait, DateTime CreatedAt, DateTime UpdatedAt,
    CharacterSheet Sheet, CharacterReport Report, CharacterValidationReport Validation,
    // e.g. "roll a Critical Injury"
    IReadOnlyList<string> Notices);

// CanUndo: IP spend that's still the latest step. Undone: its IP came back.
public sealed record CharacterEventDto(
    long Id, CharacterEventKind Kind, string Action, int? Amount, string? Before, string? After, string Description,
    bool IsManual, DateTime CreatedAt, bool CanUndo = false, bool Undone = false);

public sealed record CreateCharacterRequest
{
    public string Name { get; init; } = "";
    public string? Handle { get; init; }
    public CreationMethod Method { get; init; }
    public string Role { get; init; } = "";
    public string? NativeLanguage { get; init; }
    // CP:R p.45, drives the native language suggestions
    public string? CulturalRegion { get; init; }
    public string? Home { get; init; }
    public CampaignOverrides? Overrides { get; init; }
}

// see CharacterService.SaveSheetAsync
public sealed record SaveSheetRequest(int Version, CharacterSheet Sheet, string? ManualReason = null);

public sealed record VersionRequest(int Version);

public sealed record ResetCreationRequest(int Version, CreationMethod Method, string Role);

public sealed record TemplateRowRequest(int Version, int? Row);

// missing STATs are rolled on the server
public sealed record StatRollsRequest(int Version, Dictionary<string, int>? Rolls);

public sealed record SkillChoiceRequest(int Version, string SkillId, string Specialization);

// Cyberware is installed on purchase during creation or with Install. Equip for weapons/armor (armor needs Worn).
// Received (Trade/Gift/Loot) skips payment, only after creation.
public sealed record PurchaseRequest(
    int Version, int ItemId, int Quantity = 1, Caliber? Caliber = null, BudgetPool? Pool = null,
    bool? Install = null, bool MeatLimb = false, string? HostInstanceId = null, bool Acknowledge = false,
    bool Equip = false, ArmorSlot? Worn = null, ItemOrigin? Received = null, string? Note = null);

// Acknowledge confirms requirements we can only show as text.
public sealed record InstallCyberwareRequest(
    int Version, int? ItemId = null, string? FromInventory = null, bool MeatLimb = false, string? HostInstanceId = null,
    bool Acknowledge = false, BudgetPool? Pool = null,
    // preview only: pretend this piece was removed first (replacement)
    string? Replacing = null,
    // side picked on the body, pieces on that side come first
    BodySide? Side = null);

public sealed record RemoveCyberwareRequest(int Version, string InstanceId, bool Keep = true);

public sealed record ReplaceCyberwareRequest(
    int Version, string InstanceId, int ItemId, bool MeatLimb = false, string? HostInstanceId = null, bool Acknowledge = false, bool Keep = true,
    BodySide? Side = null);

// Kind: "standard", "extreme"
public sealed record TherapyRequest(int Version, string Kind, string Payment = "clinic", int? Roll = null);

public sealed record AttachRequest(int Version, string WeaponInstanceId, string AttachmentInstanceId);

public sealed record WearableInstallRequest(int Version, string WearableInstanceId, int? ItemId = null, string? OptionInstanceId = null, BudgetPool? Pool = null);

public sealed record WearableOptionRequest(int Version, string WearableInstanceId, string OptionInstanceId);

public sealed record WearableSideRequest(int Version, string WearableInstanceId, BodySide? Side);

public sealed record PreviewPieceDto(
    string InstanceId, string Name, string Slug, int Price, int HumanityLoss, string? HumanityLossDice, string? Host,
    BodySide? Side, bool MeatLimb, string Region);

public sealed record InstallPreviewDto(
    bool CanInstall,
    string? Error,
    IReadOnlyList<PreviewPieceDto> Pieces,
    int Price,
    BudgetPool Pool,
    int Available,
    int AvailableAfter,
    int HumanityNow,
    int HumanityLossMin,
    int HumanityLossMax,
    bool HumanityLossRolled,
    int EmpNow,
    int EmpAfterWorst,
    int TherapyCapAfter,
    IReadOnlyList<string> TextRequirements,
    IReadOnlyList<string> Warnings);

public sealed record InstanceRequest(int Version, string InstanceId);

public sealed record EquipRequest(int Version, string InstanceId, ItemState State, ArmorSlot? Worn = null);

// HalveArmor: melee and Martial Arts (CP:R p.176)
public sealed record DamageRequest(
    int Version, int Amount, InjuryLocation Location = InjuryLocation.Body, bool BypassArmor = false, string? Description = null,
    bool HalveArmor = false);

public sealed record HealRequest(int Version, int Amount, string? Description = null);

// Roll == null -> server rolls
public sealed record DeathSaveRequest(int Version, int? Roll = null);

public sealed record AblateRequest(int Version, ArmorSlot Location, int Amount = 1, string? Description = null);

public sealed record RepairArmorRequest(int Version, string InstanceId, int? Sp = null);

public sealed record AddInjuryRequest(
    int Version, InjuryLocation Location, string Name, string? Detail = null, bool ApplyBonusDamage = true);

public sealed record UpdateInjuryRequest(int Version, InjuryStatus Status, string? Note = null);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum FireMode { Single, Autofire, Suppressive }

public sealed record FireRequest(int Version, string InstanceId, FireMode Mode = FireMode.Single, int? Rounds = null);

public sealed record RoleSplitRequest(int Version, string Role, Dictionary<string, int> Split);

// CP:R p.344
public sealed record WeaponQualityRequest(int Version, string InstanceId, WeaponQuality Quality);

public sealed record ReloadRequest(int Version, string InstanceId, string? AmmoInstanceId = null);

public sealed record AdjustBalanceRequest(int Version, int Amount, string Reason);

public sealed record LuckRequest(int Version, int Spent);

public sealed record PayMonthsRequest(int Version, int Months = 1);

// negative = correction
public sealed record AwardIpRequest(int Version, int Amount, string Reason);

public sealed record ImproveSkillRequest(int Version, string SkillKey);
public sealed record UndoProgressionRequest(int Version, long EventId);

public sealed record ImproveRoleRequest(int Version, string Role);

// multiclassing (CP:R p.142)
public sealed record AddRoleRequest(int Version, string Role);

// Quantity in units, or rounds for ammo
public sealed record RemoveItemRequest(int Version, string InstanceId, int? Quantity = null, int? SaleAmount = null, string? Reason = null);

// all from the rules files
public sealed record CharacterRulesDto(
    string About,
    IReadOnlyDictionary<string, string> Books,
    IReadOnlyList<string> StatOrder,
    IReadOnlyList<CreationMethodDto> Methods,
    IReadOnlyList<RoleDto> Roles,
    IReadOnlyList<SkillDto> Skills,
    IReadOnlyList<string> BasicSkills,
    int OriginLanguageLevel,
    IReadOnlyList<CulturalOriginDto> CulturalOrigins,
    IReadOnlyList<string> Homes,
    IReadOnlyList<LifepathTableDto> LifepathTables,
    LifepathRelationsDto Relations,
    IReadOnlyList<CriticalInjuryDto> CriticalInjuries,
    IReadOnlyList<NamedValueDto> Lifestyles,
    IReadOnlyList<HousingDto> Housing,
    IReadOnlyList<RuleFactDto> Facts,
    CyberwareRulesDto Cyberware);

public sealed record CreationMethodDto(
    CreationMethod Method, string Label, string Summary, string Source,
    int? StatPoints, int? StatMin, int? StatMax, int? SkillPoints, int? SkillMin, int? SkillMax,
    int? GearBudget, int? FashionBudget, int? StartingCash, IReadOnlyList<Rules.StatPreset> StatPresets);

public sealed record RoleDto(
    string Id, string Ability, int AbilityPage, IReadOnlyList<int[]> StatTable, string StatTableSource,
    IReadOnlyDictionary<string, int> Skills, string SkillsSource, IReadOnlyList<string> ChoiceSkills,
    IReadOnlyList<StartingSectionDto> Starting, IReadOnlyList<LifepathQuestionDto> Lifepath, string LifepathSource,
    RoleAbilityDto AbilityInfo);

public sealed record RoleAbilityDto(string Name, string Source, string Summary, IReadOnlyList<string> Points, Rules.RoleSplitRule? Split = null);

public sealed record StartingSectionDto(string Key, string Label, string Source, int? HumanityLoss, IReadOnlyList<StartingGroupDto> Groups);

public sealed record StartingGroupDto(int Index, IReadOnlyList<StartingOptionDto> Options);

public sealed record StartingOptionDto(int Index, string Label, IReadOnlyList<StartingItemDto> Items);

public sealed record StartingItemDto(string Slug, int? ItemId, string Name, int Qty, Caliber? Caliber, int? Rounds, ArmorSlot? Worn, bool MeatArm);

public sealed record LifepathQuestionDto(string Id, string Label, string LabelPt, string Dice, IReadOnlyList<string> Options, string? ShowIfQuestion, string? ShowIfValue);

public sealed record CulturalOriginDto(string Region, IReadOnlyList<string> Languages);

public sealed record LifepathTableDto(string Id, string Label, string LabelPt, string Dice, int Page, IReadOnlyList<string> Options);

public sealed record LifepathRelationsDto(
    IReadOnlyList<string> FriendRelationship, IReadOnlyList<string> EnemyWho, IReadOnlyList<string> EnemyCause,
    IReadOnlyList<string> EnemyResources, IReadOnlyList<string> EnemyRevenge, IReadOnlyList<string> LoveOutcome, string Source);

public sealed record CriticalInjuryDto(
    InjuryLocation Location, int Roll, string Name, string Effect, string? QuickFix, string Treatment, bool DeathSave, bool QuickFixHeals);

public sealed record NamedValueDto(string Id, string Label, int Value);

public sealed record StoreItemDto(
    int Id, string Slug, string Name, ItemCategory Category, int? Price, string? Source, int? Page,
    bool Fashion, bool Cyberware, IReadOnlyList<Caliber> Calibers, int? PerPack, string? Summary,
    CyberwareType? CyberType = null, BodyLocation? BodyLocation = null, int? HumanityLoss = null, string? HumanityLossDice = null,
    bool Foundational = false, string? Requirements = null, string? Description = null, string? Effect = null);

public enum StoreSort { Category, Name, Price }

public sealed record StorePageDto(IReadOnlyList<StoreItemDto> Items, int Total, int Page, int PageSize);

public sealed record CyberwareRulesDto(
    IReadOnlyList<string> MeatArmOptions, IReadOnlyList<string> MeatLegOptions, IReadOnlyList<string> AnyLimbOptions,
    IReadOnlyList<string> Coverings, IReadOnlyList<TherapyKindDto> Therapies, IReadOnlyList<string> TherapyPayments, string TherapySource);

public sealed record TherapyKindDto(string Id, string Label, int Cost, string Recovery, int Dv, int Materials, string Source);

public sealed record HousingDto(string Id, string Label, int? Rent, int? Buy, bool Corporate);
