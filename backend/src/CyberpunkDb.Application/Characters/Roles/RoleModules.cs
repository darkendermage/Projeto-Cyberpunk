using System.Text.Json;
using CyberpunkDb.Application.Characters.Validation;

namespace CyberpunkDb.Application.Characters.Roles;

/// <summary>
/// Extension point for the Role-specific systems (Media Credibility and stories, Nomad garage, Netrunner cyberdeck,
/// Tech fabrication, Exec team, Fixer contacts...). Each Role works differently, so each module owns its own data under
/// <see cref="CharacterSheet.RoleData"/> and its own rules, instead of one generic form for every Role. No module is
/// required for the core sheet; a Role without a module simply keeps its data untouched.
/// </summary>
public interface IRoleModule
{
    /// <summary>Role id as in character-rules.json ("Media", "Nomad"...).</summary>
    string Role { get; }

    /// <summary>Checks the module's own data; issues join the character's validation report.</summary>
    IEnumerable<CharacterIssue> Validate(CharacterSheet sheet, JsonElement data);
}

public sealed class RoleModuleRegistry(IEnumerable<IRoleModule> modules)
{
    private readonly Dictionary<string, IRoleModule> _modules =
        modules.ToDictionary(m => m.Role, StringComparer.OrdinalIgnoreCase);

    public IRoleModule? Find(string role) => _modules.GetValueOrDefault(role);

    public IReadOnlyCollection<string> Roles => _modules.Keys;

    /// <summary>Issues from the modules of every Role the character has.</summary>
    public IEnumerable<CharacterIssue> Validate(CharacterSheet sheet) =>
        sheet.Roles
            .Select(r => (Module: Find(r.Role), Data: sheet.RoleData.TryGetValue(r.Role, out var d) ? d : default))
            .Where(x => x.Module is not null && x.Data.ValueKind != JsonValueKind.Undefined)
            .SelectMany(x => x.Module!.Validate(sheet, x.Data));
}
