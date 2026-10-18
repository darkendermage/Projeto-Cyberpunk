using System.Text.Json.Serialization;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Npcs.Validation;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NpcIssueSeverity { Error, Warning, Info }

/// <summary>
/// One finding. <see cref="Key"/> identifies the violation (code plus target) and is what a GM exception
/// references. Only <see cref="Overridable"/> errors accept an exception; structural errors never do.
/// </summary>
public sealed record NpcIssue(
    string Code,
    string Key,
    NpcIssueSeverity Severity,
    string Message,
    string? Suggestion,
    string? Source,
    bool Overridable,
    string? Field)
{
    public bool Overridden { get; init; }
    public string? Justification { get; init; }
}

/// <summary>Budget usage. <see cref="Total"/> null means unlimited.</summary>
public sealed record BudgetLine(int Used, int? Total, string Source)
{
    public int? Remaining => Total - Used;
    public bool Exceeded => Total is { } t && Used > t;
}

public sealed record ArmorPenalty(int Ref, int Dex, int Move);

public sealed record NpcDerived(
    int Hp,
    int SeriouslyWounded,
    int DeathSave,
    int BodyForDerived,
    int StrengthBody,
    int SpBody,
    int SpHead,
    ArmorPenalty ArmorPenalty,
    IReadOnlyDictionary<string, int> EffectiveStats,
    int CyberwarePieces,
    int? SuggestedEmp);

public sealed record SkillLine(
    string Key, string SkillId, string Name, string? Specialization, string Stat, string Category,
    int Level, int FreeLevel, int Cost, int Base, bool DoubleCost, bool Combat, bool IsKey);

public sealed record GearLine(
    int Index, int ItemId, string Name, string Slug, ItemCategory Category, int Quantity,
    WeaponQuality? Quality, int? UnitPrice, int Total, ArmorSlot? Worn, string? Summary, bool Locked);

public sealed record CyberLine(
    string InstanceId, int ItemId, string Name, string Slug, ItemCategory Category, string? ParentInstanceId,
    int? Price, int SlotsUsed, bool SlotsAssumed, int? SlotsProvided, int? SlotsFree, string? Pool, bool Foundational,
    int? RequiresItemId, bool RequiresPair, string? FreeTextRequirement, bool Locked);

public sealed record TemplateSummary(
    NpcLevel Level, bool Hardened, string Name, int StatPoints, int MaxStat, int SkillPoints, int MaxSkill,
    string SuggestedHp, string SuggestedSp, string CombatSkillBase, string NonCombatSkillBase,
    int GearBudget, int? CyberwareBudget, string WeaponQuality, int RoleRank, string RolePresence);

public sealed record NpcValidationReport(
    bool IsValid,
    NpcStatus Status,
    TemplateSummary Template,
    BudgetLine Stats,
    BudgetLine Skills,
    BudgetLine Gear,
    BudgetLine Cyberware,
    NpcDerived Derived,
    IReadOnlyList<SkillLine> SkillLines,
    IReadOnlyList<GearLine> GearLines,
    IReadOnlyList<CyberLine> CyberLines,
    IReadOnlyList<NpcIssue> Issues)
{
    public int ErrorCount => Issues.Count(i => i.Severity == NpcIssueSeverity.Error && !i.Overridden);
    public int WarningCount => Issues.Count(i => i.Severity == NpcIssueSeverity.Warning);
}
