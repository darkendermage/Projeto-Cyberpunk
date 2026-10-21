using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Npcs;

public sealed record NpcSaveRequest
{
    public NpcSheet Sheet { get; init; } = new();
    /// <summary>Saves even with blocking errors, as a draft pending validation.</summary>
    public bool SaveAsDraft { get; init; }
}

/// <summary>Manual editor: install an implant (and any foundational piece it needs) into the sheet.</summary>
public sealed record NpcInstallCyberwareRequest(NpcSheet Sheet, int ItemId);

public sealed record NpcSummaryDto(
    int Id, string Name, string? Concept, NpcLevel Level, bool Hardened, string? ProfileId, NpcStatus Status,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record NpcDetailDto(
    int Id, NpcStatus Status, DateTime CreatedAt, DateTime UpdatedAt, NpcSheet Sheet, NpcValidationReport Report);

/// <summary>One documented parameter: the value as shown to the user and where it comes from.</summary>
public sealed record RuleFactDto(string Key, string Label, string Value, string? Source, bool Complementary, string? Note);

public sealed record NpcTemplateDto(
    NpcLevel Level, bool Hardened, string Name, string Summary, TemplateSummary Values, IReadOnlyList<RuleFactDto> Facts);

public sealed record SkillDto(
    string Id, string Name, string Stat, string Category, bool Combat, bool DoubleCost, bool Specialized, int Page, string? NamePt = null);

public sealed record FreeSkillDto(string Skill, string? Specialization, int Level);

public sealed record ProfileDto(
    string Id, string Label, string Description, string? Role, IReadOnlyList<string> KeyCombatSkills, IReadOnlyList<string> KeyNonCombatSkills);

public sealed record NpcRulesDto(
    IReadOnlyDictionary<string, string> Books,
    string About,
    IReadOnlyList<string> StatKeys,
    IReadOnlyList<NpcTemplateDto> Templates,
    IReadOnlyList<SkillDto> Skills,
    IReadOnlyList<FreeSkillDto> FreeSkills,
    string FreeSkillsSource,
    IReadOnlyList<ProfileDto> Profiles,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Homes,
    IReadOnlyList<RuleFactDto> GeneralRules);
