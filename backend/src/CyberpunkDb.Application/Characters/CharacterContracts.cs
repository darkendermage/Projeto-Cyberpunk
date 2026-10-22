using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters;

// Every request that changes a character carries the Version the client last saw (optimistic concurrency):
// a stale write is refused with 409 instead of silently overwriting another tab's changes.

public sealed record CharacterSummaryDto(
    int Id, string Name, string? Handle, string? Role, int? RoleRank, CreationMethod Method, CharacterStatus Status,
    int ErrorCount, int WarningCount, int MaxHp, int CurrentHp, int Humanity, int HumanityMax, int Balance,
    bool HasPortrait, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record CharacterDetailDto(
    int Id, int Version, CharacterStatus Status, bool HasPortrait, DateTime CreatedAt, DateTime UpdatedAt,
    CharacterSheet Sheet, CharacterReport Report, CharacterValidationReport Validation,
    // What the last operation means for the player (e.g. "roll a Critical Injury"); empty on plain reads.
    IReadOnlyList<string> Notices);

public sealed record CharacterEventDto(
    long Id, CharacterEventKind Kind, string Action, int? Amount, string? Before, string? After, string Description,
    bool IsManual, DateTime CreatedAt);

public sealed record CreateCharacterRequest
{
    public string Name { get; init; } = "";
    public string? Handle { get; init; }
    public CreationMethod Method { get; init; }
    public string Role { get; init; } = "";
    public string? NativeLanguage { get; init; }
    /// <summary>Cultural Origin region of the Lifepath (CP:R p.45); its languages are the native-language suggestions.</summary>
    public string? CulturalRegion { get; init; }
    public string? Home { get; init; }
    public CampaignOverrides? Overrides { get; init; }
}

/// <summary>
/// Saves the editable parts of the sheet. The server keeps everything that only commands may change (inventory,
/// cyberware, money, Humanity, combat state, injuries); after creation, STATs, skills and Roles are kept too unless
/// <see cref="ManualReason"/> explains a GM-approved adjustment, which is logged.
/// </summary>
public sealed record SaveSheetRequest(int Version, CharacterSheet Sheet, string? ManualReason = null);

public sealed record VersionRequest(int Version);

public sealed record ResetCreationRequest(int Version, CreationMethod Method, string Role);

public sealed record TemplateRowRequest(int Version, int? Row);

/// <summary>Edgerunner STAT rolls; STATs left out are rolled by the server.</summary>
public sealed record StatRollsRequest(int Version, Dictionary<string, int>? Rolls);

public sealed record SkillChoiceRequest(int Version, string SkillId, string Specialization);

/// <summary>
/// Buys an item. Cyberware is installed on purchase during creation, or when <see cref="Install"/> is true;
/// weapons and armor are equipped only with <see cref="Equip"/> (armor also needs <see cref="Worn"/>).
/// With <see cref="Received"/> (Trade, Gift or Loot) the item comes in without paying, after creation only.
/// </summary>
public sealed record PurchaseRequest(
    int Version, int ItemId, int Quantity = 1, Caliber? Caliber = null, BudgetPool? Pool = null,
    bool? Install = null, bool MeatLimb = false, string? HostInstanceId = null, bool Acknowledge = false,
    bool Equip = false, ArmorSlot? Worn = null, ItemOrigin? Received = null, string? Note = null);

/// <summary>
/// Installs cyberware bought now (<see cref="ItemId"/>) or owned in the inventory (<see cref="FromInventory"/>).
/// <see cref="Acknowledge"/> confirms requirements the system can only show as text.
/// </summary>
public sealed record InstallCyberwareRequest(
    int Version, int? ItemId = null, string? FromInventory = null, bool MeatLimb = false, string? HostInstanceId = null,
    bool Acknowledge = false, BudgetPool? Pool = null,
    // Preview only: simulate that this installed piece (and its options) was removed first, as a replacement does.
    string? Replacing = null,
    // Side the player picked on the body (right eye, left arm): new pieces and hosts on that side come first.
    BodySide? Side = null);

public sealed record RemoveCyberwareRequest(int Version, string InstanceId, bool Keep = true);

public sealed record ReplaceCyberwareRequest(
    int Version, string InstanceId, int ItemId, bool MeatLimb = false, string? HostInstanceId = null, bool Acknowledge = false, bool Keep = true,
    BodySide? Side = null);

/// <summary>Therapy of a kind ("standard", "extreme"), paid at a clinic, by a Medtech (materials) or free.</summary>
public sealed record TherapyRequest(int Version, string Kind, string Payment = "clinic", int? Roll = null);

public sealed record AttachRequest(int Version, string WeaponInstanceId, string AttachmentInstanceId);

/// <summary>Option for a worn host: bought now (<see cref="ItemId"/>) or taken from the inventory (<see cref="OptionInstanceId"/>).</summary>
public sealed record WearableInstallRequest(int Version, string WearableInstanceId, int? ItemId = null, string? OptionInstanceId = null, BudgetPool? Pool = null);

public sealed record WearableOptionRequest(int Version, string WearableInstanceId, string OptionInstanceId);

public sealed record WearableSideRequest(int Version, string WearableInstanceId, BodySide? Side);

public sealed record PreviewPieceDto(
    string InstanceId, string Name, string Slug, int Price, int HumanityLoss, string? HumanityLossDice, string? Host,
    BodySide? Side, bool MeatLimb, string Region);

/// <summary>What an installation would change, computed by the same planner that installs it.</summary>
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

public sealed record DamageRequest(
    int Version, int Amount, InjuryLocation Location = InjuryLocation.Body, bool BypassArmor = false, string? Description = null);

public sealed record HealRequest(int Version, int Amount, string? Description = null);

/// <summary>Death Save; without <see cref="Roll"/> the server rolls the d10.</summary>
public sealed record DeathSaveRequest(int Version, int? Roll = null);

public sealed record AblateRequest(int Version, ArmorSlot Location, int Amount = 1, string? Description = null);

public sealed record RepairArmorRequest(int Version, string InstanceId, int? Sp = null);

public sealed record AddInjuryRequest(
    int Version, InjuryLocation Location, string Name, string? Detail = null, bool ApplyBonusDamage = true);

public sealed record UpdateInjuryRequest(int Version, InjuryStatus Status, string? Note = null);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum FireMode { Single, Autofire, Suppressive }

public sealed record FireRequest(int Version, string InstanceId, FireMode Mode = FireMode.Single, int? Rounds = null);

/// <summary>Reloads from an ammunition entry of the inventory (null = the compatible entry with most rounds).</summary>
public sealed record ReloadRequest(int Version, string InstanceId, string? AmmoInstanceId = null);

public sealed record AdjustBalanceRequest(int Version, int Amount, string Reason);

public sealed record LuckRequest(int Version, int Spent);

public sealed record PayMonthsRequest(int Version, int Months = 1);

/// <summary>Improvement Points awarded by the GM (or a negative correction), with the reason.</summary>
public sealed record AwardIpRequest(int Version, int Amount, string Reason);

public sealed record ImproveSkillRequest(int Version, string SkillKey);

public sealed record ImproveRoleRequest(int Version, string Role);

/// <summary>Multiclassing: a new Role at Rank 1 (CP:R p.142).</summary>
public sealed record AddRoleRequest(int Version, string Role);

/// <summary>Discards, loses or sells part or all of an inventory entry (quantity in units, or rounds for ammunition).</summary>
public sealed record RemoveItemRequest(int Version, string InstanceId, int? Quantity = null, int? SaleAmount = null, string? Reason = null);

/// <summary>Everything the frontend needs to build the forms; the values come from the rules files.</summary>
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

/// <summary>Role Ability explained for the player: a summary and the main mechanics, with the pages.</summary>
public sealed record RoleAbilityDto(string Name, string Source, string Summary, IReadOnlyList<string> Points);

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

/// <summary>A catalog item as the store shows it: price and what the purchase form needs.</summary>
public sealed record StoreItemDto(
    int Id, string Slug, string Name, ItemCategory Category, int? Price, string? Source, int? Page,
    bool Fashion, bool Cyberware, IReadOnlyList<Caliber> Calibers, int? PerPack, string? Summary,
    CyberwareType? CyberType = null, BodyLocation? BodyLocation = null, int? HumanityLoss = null, string? HumanityLossDice = null,
    bool Foundational = false, string? Requirements = null, string? Description = null);

/// <summary>One page of the store: the items, the total that matched and the page actually returned.</summary>
public sealed record StorePageDto(IReadOnlyList<StoreItemDto> Items, int Total, int Page, int PageSize);

/// <summary>Cyberware placement and therapy options the cyberware screen offers (values from the rule files).</summary>
public sealed record CyberwareRulesDto(
    IReadOnlyList<string> MeatArmOptions, IReadOnlyList<string> MeatLegOptions, IReadOnlyList<string> AnyLimbOptions,
    IReadOnlyList<string> Coverings, IReadOnlyList<TherapyKindDto> Therapies, IReadOnlyList<string> TherapyPayments, string TherapySource);

public sealed record TherapyKindDto(string Id, string Label, int Cost, string Recovery, int Dv, int Materials, string Source);

public sealed record HousingDto(string Id, string Label, int? Rent, int? Buy, bool Corporate);
