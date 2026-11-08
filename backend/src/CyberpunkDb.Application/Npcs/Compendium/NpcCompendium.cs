using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Application.Npcs.Rules;

namespace CyberpunkDb.Application.Npcs.Compendium;

// npc-compendium.json, indexed by id. Singleton.
public sealed class NpcCompendium
{
    private const string ResourceName = "CyberpunkDb.Application.Npcs.npc-compendium.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, CompendiumNpc> _byId;

    public NpcCompendium(IReadOnlyList<CompendiumNpc> all)
    {
        All = all;
        _byId = all.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<CompendiumNpc> All { get; }

    public CompendiumNpc? Find(string id) => _byId.GetValueOrDefault(id);

    public CompendiumNpc Require(string id) => Find(id)
        ?? throw new NotFoundException(L.T($"NPC '{id}' não está no compêndio.", $"NPC '{id}' isn't in the compendium."));

    public IReadOnlyList<CompendiumNpcSummary> ListSummaries() => All
        .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
        .Select(n => new CompendiumNpcSummary(n.Id, n.Name, n.Aliases, n.Kind, n.Role, n.RoleRank, n.Rep, n.Affiliation,
            n.Sources, n.Summary.Current))
        .ToList();

    public CompendiumNpcDetail GetDetail(string id)
    {
        var n = Require(id);
        var s = n.Sheet;
        var sheet = s is null ? null : new CompendiumSheetDto(s.Hp, s.SeriouslyWounded, s.DeathSave, s.Stats, s.ArmoredStats,
            s.Weapons, s.Armor, s.Skills, s.ArmoredSkills, s.Gear, s.Cyberware,
            s.Specials.Select(x => new CompendiumSpecialDto(x.Name, x.Text.Current)).ToList());
        return new CompendiumNpcDetail(n.Id, n.Name, n.Aliases, n.Kind, n.Role, n.RoleRank, n.Rep, n.Affiliation, n.Sources,
            n.Summary.Current, n.SheetNote?.Current, sheet);
    }

    // "Handgun" -> "handgun", "Language (English)" -> "language:English". Null if the rule book doesn't have it.
    public static string? SkillKey(string name, NpcRuleBook rules)
    {
        name = name.Trim();
        var exact = rules.Skills.FirstOrDefault(k => k.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || k.Id.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact.Id;
        var open = name.IndexOf('(');
        if (open <= 0 || !name.EndsWith(')')) return null;
        var baseName = name[..open].Trim();
        var skill = rules.Skills.FirstOrDefault(k => k.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase));
        return skill is null ? null : $"{skill.Id}:{name[(open + 1)..^1].Trim()}";
    }

    public static NpcCompendium LoadEmbedded()
    {
        using var stream = typeof(NpcCompendium).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"Recurso {ResourceName} não encontrado.");
        return new NpcCompendium(JsonSerializer.Deserialize<List<CompendiumNpc>>(stream, JsonOptions)
                                 ?? throw new InvalidOperationException("npc-compendium.json vazio."));
    }
}
