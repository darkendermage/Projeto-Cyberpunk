using System.Text.Json.Serialization;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Npcs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArmorSlot { Body, Head }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CyberwareRuleState { Allowed, Required, Forbidden }

/// <summary>
/// The mechanical sheet of an NPC, as edited by the user and produced by the generator.
/// Skills are keyed by skill id, with the specialization after a colon ("handgun", "language:Streetslang").
/// Skill levels are the total level; the free levels from DGD p.151 are discounted when computing cost.
/// </summary>
public sealed record NpcSheet
{
    public string Name { get; init; } = "";
    public string? Concept { get; init; }
    public NpcLevel Level { get; init; }
    public bool Hardened { get; init; }
    public string? ProfileId { get; init; }
    public string? Role { get; init; }
    public int RoleRank { get; init; }
    public string NativeLanguage { get; init; } = "English";
    public string Home { get; init; } = "Watson";
    public Dictionary<string, int> Stats { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>NPCs rarely have LUCK (DGD p.152); kept outside the STAT budget.</summary>
    public int? Luck { get; init; }
    public Dictionary<string, int> Skills { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>"Important" skills whose Skill Base is checked against the template ranges.</summary>
    public List<string> KeySkills { get; init; } = [];
    public List<NpcGearEntry> Gear { get; init; } = [];
    public List<NpcCyberwareEntry> Cyberware { get; init; } = [];
    public List<NpcCyberwareRule> CyberwareRules { get; init; } = [];
    public List<NpcRuleException> Exceptions { get; init; } = [];
    public NpcLocks Locks { get; init; } = new();
    public string? Notes { get; init; }

    /// <summary>Rebuilds dictionaries with case-insensitive keys and fills nulls left by deserialization.</summary>
    public NpcSheet Normalize() => this with
    {
        Name = Name?.Trim() ?? "",
        Concept = string.IsNullOrWhiteSpace(Concept) ? null : Concept.Trim(),
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
    /// <summary>Weapon quality when it differs from the catalog item (priced by CP:R p.342). Null = as cataloged.</summary>
    public WeaponQuality? Quality { get; init; }
    /// <summary>Armor worn on this location. Null = carried, not worn.</summary>
    public ArmorSlot? Worn { get; init; }
    /// <summary>Semi-automatic mode: kept as-is by the generator.</summary>
    public bool Locked { get; init; }
}

public sealed record NpcCyberwareEntry
{
    public string InstanceId { get; init; } = "";
    public int ItemId { get; init; }
    /// <summary>Instance of the foundational cyberware this option is installed in.</summary>
    public string? ParentInstanceId { get; init; }
    public bool Locked { get; init; }
}

public sealed record NpcCyberwareRule(int ItemId, CyberwareRuleState State);

/// <summary>A GM exception that accepts one overridable violation (matched by issue key) with a justification.</summary>
public sealed record NpcRuleException(string Key, string Justification);

/// <summary>Fields the generator must not touch in semi-automatic mode.</summary>
public sealed record NpcLocks
{
    public List<string> Stats { get; init; } = [];
    public List<string> Skills { get; init; } = [];
    /// <summary>Name, concept, Role and rank, native language and home.</summary>
    public bool Identity { get; init; }
}

/// <summary>Skill keys: "id" or "id:Specialization".</summary>
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
