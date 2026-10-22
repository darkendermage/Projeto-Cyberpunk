using System.Text.Json.Serialization;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Calculation;

// Everything here is derived from the sheet on every read; none of it is persisted.

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WoundState { Unhurt, Lightly, Seriously, Mortally }

/// <summary>One part of an effective value, so the sheet can explain where a number comes from.</summary>
public sealed record ValuePart(string Label, int Value);

/// <summary>A STAT: base value as bought or rolled, and the effective value after modifiers and penalties.</summary>
public sealed record StatLine(string Key, int Base, int Effective, IReadOnlyList<ValuePart> Parts);

public sealed record HealthReport(
    int MaxHp,
    int CurrentHp,
    // Half of the max HP, rounded up (CP:R p.79).
    int SeriouslyWoundedThreshold,
    WoundState WoundState,
    int WoundActionPenalty,
    int WoundMovePenalty,
    int? StabilizationDv,
    // BODY used for HP and Death Save (an Implanted Linear Frame replaces it, CP:R p.367).
    int BodyForDerived,
    string? BodySource,
    // Death Save target: roll under this on 1d10, plus the penalty (CP:R p.188).
    int DeathSave,
    // Base Death Save Penalty from active Critical Injuries.
    int BaseDeathSavePenalty,
    // Penalty applied to the next Death Save: base plus what accumulated since Stabilization.
    int DeathSavePenalty,
    bool MustRollDeathSave);

public sealed record HumanityReport(
    int EmpBase,
    // EMP × 10 (CP:R p.80).
    int Max,
    int Current,
    int Lost,
    int Recovered,
    // Highest Humanity therapy can restore while the current cyberware stays installed (CP:R p.230).
    int TherapyCap,
    int PiecesLoweringCap,
    int BorgwareLoweringCap,
    // The tens place of the current Humanity (CP:R p.80).
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
    // Highest penalty among worn armor, applied once (CP:R p.184).
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
    // Skill-targeted modifiers.
    int Modifier,
    // STAT + level + modifier.
    int Base,
    // Penalty to all Actions (wound state, injuries, general modifiers).
    int ActionPenalty,
    int Total,
    bool DoubleCost,
    bool Combat,
    bool Basic,
    bool RoleSkill,
    int Page,
    // Improvement Points to raise it one level (CP:R p.410); null at the maximum.
    int? IpToRaise,
    // What makes up Modifier (temporary modifiers and cyberware bonuses).
    IReadOnlyList<ValuePart> ModifierParts,
    // Cyberware bonuses that only apply in a situation ("+2 for Checks which include sight"); not in Base.
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
    string? Notes);

public sealed record WeaponLine(
    string InstanceId,
    int ItemId,
    string Name,
    WeaponClass Class,
    string? SkillId,
    string? SkillName,
    // Skill Base of the weapon skill, wound and injury penalties included.
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
    bool Equipped);

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
    // A glove worn over this arm makes the option inaccessible (CP:R p.347; BC p.37; IR4 p.67; IR5 p.99).
    string? CoveredBy = null);

public sealed record HousingReport(
    string? HousingId,
    string? Housing,
    string? LifestyleId,
    string? Lifestyle,
    // Monthly rent: the table value (CP:R p.378) or the amount agreed in play.
    int Rent,
    bool RentFromTable,
    int LifestyleCost,
    int MonthlyTotal,
    DateOnly? PaidUntil,
    int DaysOverdue,
    // Paid until today; Pending inside the one-week grace; Overdue after it (CP:R p.377).
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
    // Inhabiting a Full Body Conversion body (Interface RED 3 p.93).
    bool FullBodyConversion,
    // What the installed cyberware does to the sheet (STAT and skill bonuses, game effects).
    IReadOnlyList<CyberEffectLine> CyberwareEffects,
    // Worn items with Option Slots (Smart Glasses, Battleglove, Smart Ears...).
    IReadOnlyList<WearableLine> Wearables);
