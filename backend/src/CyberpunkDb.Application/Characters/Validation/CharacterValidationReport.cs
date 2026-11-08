using System.Text.Json.Serialization;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Validation;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CharacterIssueSeverity { Error, Warning, Info }

// only errors block finalizing
public sealed record CharacterIssue(
    string Code,
    CharacterIssueSeverity Severity,
    string Message,
    string? Suggestion,
    string? Source,
    string? Field);

public sealed record CreationBudget(int Used, int Total, string Source, bool CampaignVariant)
{
    public int Remaining => Total - Used;
    public bool Exceeded => Used > Total;
}

public sealed record CharacterValidationReport(
    CharacterStatus Status,
    bool CanFinalize,
    CreationBudget? Stats,
    CreationBudget? Skills,
    CreationBudget? Gear,
    CreationBudget? Fashion,
    IReadOnlyList<CharacterIssue> Issues)
{
    public int ErrorCount => Issues.Count(i => i.Severity == CharacterIssueSeverity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == CharacterIssueSeverity.Warning);
}
