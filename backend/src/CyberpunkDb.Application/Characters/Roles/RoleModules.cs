using System.Text.Json;
using CyberpunkDb.Application.Characters.Validation;

namespace CyberpunkDb.Application.Characters.Roles;

// Hook for Role-specific systems (Media Credibility, Nomad garage, Netrunner deck, Tech fabrication...). Each
// module owns its data under CharacterSheet.RoleData. Roles without a module just keep their data untouched.
public interface IRoleModule
{
    // as in character-rules.json
    string Role { get; }

    // issues go into the character's validation report
    IEnumerable<CharacterIssue> Validate(CharacterSheet sheet, JsonElement data);
}

public sealed class RoleModuleRegistry(IEnumerable<IRoleModule> modules)
{
    private readonly Dictionary<string, IRoleModule> _modules =
        modules.ToDictionary(m => m.Role, StringComparer.OrdinalIgnoreCase);

    public IRoleModule? Find(string role) => _modules.GetValueOrDefault(role);

    public IReadOnlyCollection<string> Roles => _modules.Keys;

    public IEnumerable<CharacterIssue> Validate(CharacterSheet sheet) =>
        sheet.Roles
            .Select(r => (Module: Find(r.Role), Data: sheet.RoleData.TryGetValue(r.Role, out var d) ? d : default))
            .Where(x => x.Module is not null && x.Data.ValueKind != JsonValueKind.Undefined)
            .SelectMany(x => x.Module!.Validate(sheet, x.Data));
}
