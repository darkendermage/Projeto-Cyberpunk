using System.Text.Json.Serialization;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Npcs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArmorSlot { Body, Head }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CyberwareRuleState { Allowed, Required, Forbidden }

// Skill keys are "id" or "id:Specialization". Levels are totals; the free levels (DGD p.151) are discounted
// when computing the cost.
public sealed record NpcSheet
{
    public string Name { get; init; } = "";
    public string? Concept { get; init; }
    public NpcLevel Level { get; init; }
    public bool Hardened { get; init; }
    public string? ProfileId { get; init; }
    public string? GangId { get; init; }
    public string? Role { get; init; }
    public int RoleRank { get; init; }
    public string NativeLanguage { get; init; } = "English";
    public string Home { get; init; } = "Watson";
    public Dictionary<string, int> Stats { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    // NPCs rarely have LUCK (DGD p.152), not part of the STAT budget
    public int? Luck { get; init; }
    public Dictionary<string, int> Skills { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    // checked against the template ranges
    public List<string> KeySkills { get; init; } = [];
    public List<NpcGearEntry> Gear { get; init; } = [];
    public List<NpcCyberwareEntry> Cyberware { get; init; } = [];
    public List<NpcCyberwareRule> CyberwareRules { get; init; } = [];
    public List<NpcRuleException> Exceptions { get; init; } = [];
    public NpcLocks Locks { get; init; } = new();
    public string? Notes { get; init; }

    // case-insensitive keys again, and fill nulls from deserialization
    public NpcSheet Normalize() => this with
    {
        Name = Name?.Trim() ?? "",
        Concept = string.IsNullOrWhiteSpace(Concept) ? null : Concept.Trim(),
        GangId = string.IsNullOrWhiteSpace(GangId) ? null : GangId.Trim(),
        Role = string.IsNullOrWhiteSpace(Role) ? null : Role.Trim(),
        NativeLanguage = string.IsNullOrWhiteSpace(NativeLanguage) ? "English" : NativeLanguage.Trim(),
        Home = string.IsNullOrWhiteSpace(Home) ? "Watson" : Home.Trim(),
        Stats = new Dictionary<string, int>(
            (Stats ?? []).ToDictionary(kv => kv.Key.Trim().ToUpperInvariant(), kv => kv.Value), StringComparer.OrdinalIgnoreCase),
        Skills = new Dictionary<string, int>(
            (Skills ?? []).GroupBy(kv => SkillKey.Normalize(kv.Key), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Max(kv => kv.Value)), StringComparer.OrdinalIgnoreCase),
        KeySkills = (KeySkills ?? []).Select(SkillKey.Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        Gear = Gear ?? [],
        Cyberware = Cyberware ?? [],
        CyberwareRules = CyberwareRules ?? [],
        Exceptions = Exceptions ?? [],
        Locks = Locks ?? new NpcLocks(),
    };
}

public sealed record NpcGearEntry
{
    public int ItemId { get; init; }
    public int Quantity { get; init; } = 1;
    // null = as cataloged (other qualities priced by CP:R p.342)
    public WeaponQuality? Quality { get; init; }
    // null = carried, not worn
    public ArmorSlot? Worn { get; init; }
    // semi-auto: the generator leaves it alone
    public bool Locked { get; init; }
}

public sealed record NpcCyberwareEntry
{
    public string InstanceId { get; init; } = "";
    public int ItemId { get; init; }
    public string? ParentInstanceId { get; init; }
    public bool Locked { get; init; }
}

public sealed record NpcCyberwareRule(int ItemId, CyberwareRuleState State);

// matched by issue key
public sealed record NpcRuleException(string Key, string Justification);

// what the generator can't touch in semi-auto
public sealed record NpcLocks
{
    public List<string> Stats { get; init; } = [];
    public List<string> Skills { get; init; } = [];
    // name, concept, Role/rank, language, home
    public bool Identity { get; init; }
}

public static class SkillKey
{
    public static string Make(string id, string? specialization) =>
        string.IsNullOrWhiteSpace(specialization) ? id : $"{id}:{specialization.Trim()}";

    public static (string Id, string? Specialization) Parse(string key)
    {
        var i = key.IndexOf(':');
        return i < 0
            ? (key.Trim().ToLowerInvariant(), null)
            : (key[..i].Trim().ToLowerInvariant(), key[(i + 1)..].Trim() is { Length: > 0 } s ? s : null);
    }

    public static string Normalize(string key)
    {
        var (id, spec) = Parse(key ?? "");
        return Make(id, spec);
    }
}
