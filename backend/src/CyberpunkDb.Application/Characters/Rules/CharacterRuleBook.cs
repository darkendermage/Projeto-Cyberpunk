using System.Text.Json;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Rules;

namespace CyberpunkDb.Application.Characters.Rules;

// character-rules.json plus the skill/cyberware rules shared with NpcRuleBook. Singleton.
public sealed class CharacterRuleBook
{
    private const string ResourceName = "CyberpunkDb.Application.Characters.Rules.character-rules.json";

    // Local Expert home placeholder
    public const string HomeToken = "@home";
    // Play Instrument, Science...
    public const string ChooseToken = "@choose";

    private readonly Dictionary<string, RoleRules> _roles;
    private readonly HashSet<string> _meatArmOptions;

    public CharacterRuleBook(CharacterRulesDocument document, NpcRuleBook shared)
    {
        Document = document;
        Shared = shared;
        _roles = document.Roles.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
        _meatArmOptions = document.Cyberware.MeatArmOptions.Value.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public CharacterRulesDocument Document { get; }

    // shared with the NPC generator
    public NpcRuleBook Shared { get; }

    public IReadOnlyList<string> StatOrder => Document.StatOrder;
    public IReadOnlyList<RoleRules> Roles => Document.Roles;
    public IReadOnlyList<SkillDefinition> Skills => Shared.Skills;

    public static CharacterRuleBook LoadEmbedded(NpcRuleBook shared)
    {
        using var stream = typeof(CharacterRuleBook).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"Recurso {ResourceName} não encontrado.");
        var document = JsonSerializer.Deserialize<CharacterRulesDocument>(stream, CharacterRulesDocument.JsonOptions)
                       ?? throw new InvalidOperationException("character-rules.json vazio.");
        return new CharacterRuleBook(document, shared);
    }

    public RoleRules? FindRole(string? id) => id is null ? null : _roles.GetValueOrDefault(id);

    public RoleRules Role(string id) =>
        FindRole(id) ?? throw new InvalidOperationException($"Role {id} ausente em character-rules.json.");

    public SkillDefinition? FindSkill(string id) => Shared.FindSkill(id);

    // 2 for (x2) skills (CP:R p.81)
    public int SkillCostMultiplier(SkillDefinition skill) =>
        skill.DoubleCost ? Shared.Document.SkillCost.DoubleCost.Value : 1;

    public bool IsMeatArmOption(string slug) => _meatArmOptions.Contains(slug);

    public bool IsMeatLegOption(string slug) =>
        Document.Cyberware.MeatLegOptions.Value.Contains(slug, StringComparer.OrdinalIgnoreCase);

    public WearableHost? FindWearable(string slug) =>
        Document.Cyberware.Wearables.Value.FirstOrDefault(w => w.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    public AnyLimbOption? FindAnyLimbOption(string slug) =>
        Document.Cyberware.AnyLimbOptions.Value.FirstOrDefault(o => o.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    // Borgware that requires the Biosystem (Interface RED 3 p.93)
    public bool IsFbcBody(CatalogItem item) =>
        item.Cyber is { Type: Domain.Enums.CyberwareType.Borgware, Requirements: { } req }
        && req.Contains(Document.Cyberware.Fbc.Value.BodyRequirement, StringComparison.OrdinalIgnoreCase);

    public bool IsFbcExempt(string slug) =>
        Document.Cyberware.Fbc.Value.ExemptSlugs.Contains(slug, StringComparer.OrdinalIgnoreCase);

    public bool InFbc(CharacterSheet sheet, NpcCatalog catalog) =>
        sheet.Cyberware.Any(c => catalog.Find(c.ItemId) is { } item && IsFbcBody(item));

    // @home -> home district, @choose -> the chosen specialization (or stays as is if not chosen yet)
    public static string ResolveKey(string ruleKey, string home, IReadOnlyDictionary<string, string>? choices = null)
    {
        var (id, spec) = SkillKey.Parse(ruleKey);
        spec = spec switch
        {
            HomeToken => home,
            ChooseToken => choices?.GetValueOrDefault(id) ?? ChooseToken,
            _ => spec,
        };
        return SkillKey.Make(id, spec);
    }

    // the 13 every method needs at 2+ (CP:R p.88, p.90)
    public IReadOnlyList<string> BasicSkillKeys(string home) =>
        Document.Creation.BasicSkills.Value.Select(k => ResolveKey(k, home)).ToList();

    // Edgerunner list (CP:R p.88-89) with the Streetrat levels (p.86-87)
    public IReadOnlyDictionary<string, int> RoleSkillLevels(string role, string home, IReadOnlyDictionary<string, string>? choices = null) =>
        Role(role).Skills.Levels.ToDictionary(kv => ResolveKey(kv.Key, home, choices), kv => kv.Value, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> RoleChoiceSkills(string role) =>
        Role(role).Skills.Levels.Keys
            .Select(SkillKey.Parse)
            .Where(p => p.Specialization == ChooseToken)
            .Select(p => p.Id)
            .ToList();

    public CriticalInjuryDefinition? FindCriticalInjury(InjuryLocation location, string name)
    {
        var table = location == InjuryLocation.Head ? Document.Combat.CriticalInjuries.Head : Document.Combat.CriticalInjuries.Body;
        return table.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    // CP:R p.410
    public int SkillImprovementCost(SkillDefinition skill, int nextLevel) =>
        nextLevel * (skill.DoubleCost ? Document.Progression.IpDoubleSkillPerLevel.Value : Document.Progression.IpSkillPerLevel.Value);

    // CP:R p.410
    public int RoleImprovementCost(int nextRank) => nextRank * Document.Progression.IpRolePerRank.Value;

    public LifestyleOption? FindLifestyle(string? id) =>
        Document.Economy.Lifestyles.Options.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public HousingOption? FindHousing(string? id) =>
        Document.Economy.Housing.Options.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    // "CP:R p.105 (quoted text...)" -> "CP:R p.105"
    public static string Page(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "";
        var cut = source.IndexOf(" (", StringComparison.Ordinal);
        return cut > 0 ? source[..cut] : source;
    }

    public StartingHousing StartingHousingFor(string role) =>
        role.Equals("Exec", StringComparison.OrdinalIgnoreCase)
            ? Document.Creation.StartingHousing.Exec
            : Document.Creation.StartingHousing.Default;
}
