using System.Text.Json.Serialization;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Calculation;

// computed on every read, never saved

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WoundState { Unhurt, Lightly, Seriously, Mortally }

// so the sheet can explain where a number comes from
public sealed record ValuePart(string Label, int Value);

public sealed record StatLine(string Key, int Base, int Effective, IReadOnlyList<ValuePart> Parts);

public sealed record HealthReport(
    int MaxHp,
    int CurrentHp,
    // CP:R p.79
    int SeriouslyWoundedThreshold,
    WoundState WoundState,
    int WoundActionPenalty,
    int WoundMovePenalty,
    int? StabilizationDv,
    // Implanted Linear Frame replaces it (CP:R p.367)
    int BodyForDerived,
    string? BodySource,
    // roll under this on 1d10 + penalty (CP:R p.188)
    int DeathSave,
    // from active Critical Injuries
    int BaseDeathSavePenalty,
    // base + what piled up since Stabilization
    int DeathSavePenalty,
    bool MustRollDeathSave);

public sealed record HumanityReport(
    int EmpBase,
    // EMP x 10
    int Max,
    int Current,
    int Lost,
    int Recovered,
    // CP:R p.230
    int TherapyCap,
    int PiecesLoweringCap,
    int BorgwareLoweringCap,
    // tens of current Humanity
    int EmpEffective,
    bool Cyberpsychosis);

public sealed record ArmorLocationReport(
    ArmorSlot Location,
    int Sp,
    int? MaxSp,
    string? InstanceId,
    string? Name,
    bool FromCyberware);

public sealed record ArmorReport(
    ArmorLocationReport Head,
    ArmorLocationReport Body,
    // CP:R p.184
    ArmorPenalty Penalty,
    string? PenaltySource);

public sealed record CharacterSkillLine(
    string Key,
    string SkillId,
    string Name,
    string? NamePt,
    string? Specialization,
    string Stat,
    string Category,
    int Level,
    int StatValue,
    int Modifier,
    // STAT + level + modifier
    int Base,
    // wounds, injuries, general modifiers
    int ActionPenalty,
    int Total,
    bool DoubleCost,
    bool Combat,
    bool Basic,
    bool RoleSkill,
    int Page,
    // CP:R p.410, null at max
    int? IpToRaise,
    IReadOnlyList<ValuePart> ModifierParts,
    // "+2 for Checks which include sight" etc, not in Base
    IReadOnlyList<string> Situational);

public sealed record RoleLine(string Role, int Rank, string Ability, int AbilityPage, bool Current, int IpInvested, int? IpToRaise);

public sealed record ActiveInjuryLine(string Id, string Name, InjuryLocation Location, InjuryStatus Status, string? Effect, bool RaisesDeathSave);

public sealed record InventoryLine(
    string InstanceId,
    int ItemId,
    string Name,
    string Slug,
    ItemCategory Category,
    int Quantity,
    ItemState State,
    ArmorSlot? Worn,
    Caliber? Caliber,
    int? Rounds,
    int? Sp,
    int? CurrentSp,
    int? ShieldHp,
    int? ShieldHpMax,
    int? UnitPrice,
    int? PricePaid,
    ItemOrigin Origin,
    BudgetPool PaidFrom,
    bool Fashion,
    string? Notes,
    // weapons only
    WeaponQuality? Quality = null,
    // clothing only: where it is worn (top, jacket, footwear...)
    ClothingSlot? ClothingSlot = null);

public sealed record WeaponLine(
    string InstanceId,
    int ItemId,
    string Name,
    WeaponClass Class,
    string? SkillId,
    string? SkillName,
    // wound/injury penalties included
    int SkillTotal,
    string? Damage,
    int? Rof,
    int? Magazine,
    int LoadedRounds,
    int? LoadedAmmoItemId,
    string? LoadedAmmo,
    int? AutofireMultiplier,
    int? AutofireTotal,
    IReadOnlyList<Caliber> Calibers,
    bool Exotic,
    int AttachmentSlots,
    IReadOnlyList<string> Attachments,
    bool Equipped,
    // copy's own, else the catalog's
    WeaponQuality Quality = WeaponQuality.Standard,
    // CP:R p.344
    bool Jammed = false,
    // Excellent quality, working Smartgun...
    IReadOnlyList<ValuePart>? AttackBonuses = null,
    // for weapons with their own rules (weapon-rules.json)
    string? Slug = null,
    // mode switched on, extra magazines (id -> rounds), destroyed, BODY needed to fire it
    string? Mode = null,
    IReadOnlyDictionary<string, int>? Magazines = null,
    bool Destroyed = false,
    int? MinBody = null,
    // weapon-rules.json, spelled out for the sheet: extra magazines and the modes that stay on the weapon
    IReadOnlyList<SheetMagazine>? ExtraMagazines = null,
    IReadOnlyList<SheetWeaponMode>? SwitchModes = null)
{
    public int AttackTotal => SkillTotal + (AttackBonuses?.Sum(p => p.Value) ?? 0);
}

public sealed record SheetMagazine(string Id, string Label, int Rounds, int Capacity, bool Refill);

// Switch: "action", "move" or "minute"
public sealed record SheetWeaponMode(string Id, string Label, string Switch);

public sealed record CyberLine(
    string InstanceId,
    int ItemId,
    string Name,
    string Slug,
    CyberwareType? Type,
    BodyLocation? Location,
    string? ParentInstanceId,
    BodySide? Side,
    bool MeatLimb,
    bool Foundational,
    int? SlotsProvided,
    int SlotsUsed,
    int HumanityLossApplied,
    string? HumanityLossDice,
    int? CurrentSp,
    string Region,
    int Price,
    string? Requirements,
    // glove over the arm (CP:R p.347; BC p.37; IR4 p.67; IR5 p.99)
    string? CoveredBy = null,
    // tooltip
    string? Effect = null);

public sealed record HousingReport(
    string? HousingId,
    string? Housing,
    string? LifestyleId,
    string? Lifestyle,
    // table value (CP:R p.378) or the deal made in play
    int Rent,
    bool RentFromTable,
    int LifestyleCost,
    int MonthlyTotal,
    DateOnly? PaidUntil,
    int DaysOverdue,
    // Pending during the week of grace, Overdue after (CP:R p.377)
    PaymentStatus Status,
    string Source);

public sealed record CharacterReport(
    IReadOnlyList<StatLine> Stats,
    HealthReport Health,
    HumanityReport Humanity,
    ArmorReport Armor,
    IReadOnlyList<CharacterSkillLine> Skills,
    IReadOnlyList<RoleLine> Roles,
    IReadOnlyList<ActiveInjuryLine> ActiveInjuries,
    IReadOnlyList<ValuePart> ActionPenalties,
    int ActionPenalty,
    int Luck,
    int LuckAvailable,
    IReadOnlyList<InventoryLine> Inventory,
    IReadOnlyList<WeaponLine> Weapons,
    IReadOnlyList<CyberLine> Cyberware,
    HousingReport Housing,
    IReadOnlyList<CyberRegionReport> Regions,
    // Interface RED 3 p.93
    bool FullBodyConversion,
    IReadOnlyList<CyberEffectLine> CyberwareEffects,
    // Smart Glasses, Battleglove, Smart Ears...
    IReadOnlyList<WearableLine> Wearables);
