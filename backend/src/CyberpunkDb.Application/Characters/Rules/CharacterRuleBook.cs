using System.Text.Json;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Rules;

namespace CyberpunkDb.Application.Characters.Rules;

/// <summary>
/// The loaded <c>character-rules.json</c>, plus the shared skill and cyberware rules of <see cref="NpcRuleBook"/>.
/// Singleton; everything the character engine decides comes from here.
/// </summary>
public sealed class CharacterRuleBook
{
    private const string ResourceName = "CyberpunkDb.Application.Characters.Rules.character-rules.json";

    /// <summary>Placeholder in rule skill keys for the character's home (Local Expert).</summary>
    public const string HomeToken = "@home";
    /// <summary>Placeholder for a specialization the player chooses (Play Instrument, Science).</summary>
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

    /// <summary>Skills, STAT keys, cyberware pools, capacity and overrides shared with the NPC generator.</summary>
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

    /// <summary>Cost multiplier per level: 2 for skills marked (x2) (CP:R p.81), 1 otherwise.</summary>
    public int SkillCostMultiplier(SkillDefinition skill) =>
        skill.DoubleCost ? Shared.Document.SkillCost.DoubleCost.Value : 1;

    public bool IsMeatArmOption(string slug) => _meatArmOptions.Contains(slug);

    public bool IsMeatLegOption(string slug) =>
        Document.Cyberware.MeatLegOptions.Value.Contains(slug, StringComparer.OrdinalIgnoreCase);

    /// <summary>Coverings and Hardened Shielding: options of "a Cyberarm or Cyberleg" (CP:R p.116).</summary>
    /// <summary>The wearable host rules of a worn item (Smart Glasses, Battleglove...), or null.</summary>
    public WearableHost? FindWearable(string slug) =>
        Document.Cyberware.Wearables.Value.FirstOrDefault(w => w.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    public AnyLimbOption? FindAnyLimbOption(string slug) =>
        Document.Cyberware.AnyLimbOptions.Value.FirstOrDefault(o => o.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    /// <summary>An FBC body: Borgware whose requirement is the Biosystem (Interface RED 3 p.93).</summary>
    public bool IsFbcBody(CatalogItem item) =>
        item.Cyber is { Type: Domain.Enums.CyberwareType.Borgware, Requirements: { } req }
        && req.Contains(Document.Cyberware.Fbc.Value.BodyRequirement, StringComparison.OrdinalIgnoreCase);

    public bool IsFbcExempt(string slug) =>
        Document.Cyberware.Fbc.Value.ExemptSlugs.Contains(slug, StringComparer.OrdinalIgnoreCase);

    /// <summary>True while the character inhabits a Full Body Conversion body.</summary>
    public bool InFbc(CharacterSheet sheet, NpcCatalog catalog) =>
        sheet.Cyberware.Any(c => catalog.Find(c.ItemId) is { } item && IsFbcBody(item));

    /// <summary>
    /// Resolves a rule skill key for one character: <c>@home</c> becomes the home district; <c>@choose</c> becomes
    /// the specialization already chosen, or stays as the token when the player has not chosen yet.
    /// </summary>
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

    /// <summary>The 13 Basic Skills that every method requires at Level 2 or more (CP:R p.88 and p.90).</summary>
    public IReadOnlyList<string> BasicSkillKeys(string home) =>
        Document.Creation.BasicSkills.Value.Select(k => ResolveKey(k, home)).ToList();

    /// <summary>The 20 skills of a Role (Edgerunner list, CP:R p.88–89) with the Streetrat levels (p.86–87).</summary>
    public IReadOnlyDictionary<string, int> RoleSkillLevels(string role, string home, IReadOnlyDictionary<string, string>? choices = null) =>
        Role(role).Skills.Levels.ToDictionary(kv => ResolveKey(kv.Key, home, choices), kv => kv.Value, StringComparer.OrdinalIgnoreCase);

    /// <summary>Skill ids whose specialization the Role template leaves to the player (Play Instrument, Science).</summary>
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

    /// <summary>IP to raise a skill to <paramref name="nextLevel"/> (CP:R p.410).</summary>
    public int SkillImprovementCost(SkillDefinition skill, int nextLevel) =>
        nextLevel * (skill.DoubleCost ? Document.Progression.IpDoubleSkillPerLevel.Value : Document.Progression.IpSkillPerLevel.Value);

    /// <summary>IP to raise a Role Ability to <paramref name="nextRank"/> (CP:R p.410).</summary>
    public int RoleImprovementCost(int nextRank) => nextRank * Document.Progression.IpRolePerRank.Value;

    public LifestyleOption? FindLifestyle(string? id) =>
        Document.Economy.Lifestyles.Options.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public HousingOption? FindHousing(string? id) =>
        Document.Economy.Housing.Options.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>"CP:R p.105 (quoted rule text…)" → "CP:R p.105": the page alone, for short history entries.</summary>
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
