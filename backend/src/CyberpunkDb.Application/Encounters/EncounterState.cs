using System.Text.Json.Serialization;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Encounters;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CombatantKind { Pc, Npc, Generic }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CombatSide { Ally, Enemy }

/// <summary>Everything about a saved encounter, stored as JSON.</summary>
public sealed record EncounterState
{
    public List<Combatant> Combatants { get; init; } = [];
    public int Round { get; init; } = 1;
    /// <summary>Id of the combatant whose Turn it is; null before Initiative is rolled.</summary>
    public string? ActiveId { get; init; }
    public List<CoverPiece> Covers { get; init; } = [];
    public List<CombatLogEntry> Log { get; init; } = [];
    public string? Notes { get; init; }

    public Combatant? Find(string? id) => Combatants.FirstOrDefault(c => c.Id == id);

    /// <summary>The Initiative Queue: highest Initiative first, ties broken by the re-roll (CP:R p.168).</summary>
    [JsonIgnore]
    public IEnumerable<Combatant> Queue => Combatants
        .OrderByDescending(c => c.Initiative ?? int.MinValue)
        .ThenByDescending(c => c.TieBreak)
        .ThenBy(c => c.Id, StringComparer.Ordinal);
}

/// <summary>
/// One participant. A PC is only a link to the character sheet, which holds its HP, armor, injuries and ammo. NPCs and
/// generic enemies carry a snapshot of their sheet plus their own running state.
/// </summary>
public sealed record Combatant
{
    public string Id { get; init; } = "";
    public CombatantKind Kind { get; init; }
    public string Name { get; init; } = "";
    public CombatSide Side { get; init; }
    public int? Initiative { get; init; }
    /// <summary>Re-roll used only to order tied Initiatives.</summary>
    public int TieBreak { get; init; }
    public int? CharacterId { get; init; }
    public CombatSnapshot? Snapshot { get; init; }
    public int Hp { get; init; }
    public int SpBody { get; init; }
    public int SpHead { get; init; }
    public List<CombatInjury> Injuries { get; init; } = [];
    public int DeathSavePenalty { get; init; }
    public bool Dead { get; init; }
    public bool Stabilized { get; init; }
    /// <summary>Prone, Grappled, Unconscious, "OnFire:fire-strong"…</summary>
    public List<string> Conditions { get; init; } = [];
    public string? CoverId { get; init; }
    /// <summary>Rounds left in each weapon's magazine (NPCs and generic enemies).</summary>
    public Dictionary<string, int> Ammo { get; init; } = [];
}

/// <summary>What the combat table needs from an NPC sheet, taken when it joins the encounter.</summary>
public sealed record CombatSnapshot
{
    public int? NpcId { get; init; }
    public string Source { get; init; } = "";
    public int MaxHp { get; init; }
    /// <summary>BODY used for the Death Save and unarmed damage.</summary>
    public int Body { get; init; }
    public Dictionary<string, int> Stats { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Skill Base (STAT + level, armor penalty included) by skill key ("handgun", "evasion", "martial-arts:Karate").</summary>
    public Dictionary<string, int> Skills { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public int MaxSpBody { get; init; }
    public int MaxSpHead { get; init; }
    public bool Cyberarm { get; init; }
    public List<CombatWeapon> Weapons { get; init; } = [];
}

public sealed record CombatWeapon
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public WeaponClass? Class { get; init; }
    /// <summary>Skill id used to attack ("handgun", "melee-weapon").</summary>
    public string Skill { get; init; } = "";
    public string Damage { get; init; } = "";
    public int Rof { get; init; } = 1;
    public int? Magazine { get; init; }
    public int? AutofireMultiplier { get; init; }
    public bool Melee { get; init; }
    /// <summary>Generic enemies: attack total without the d10 (STAT + skill).</summary>
    public int? AttackBase { get; init; }
}

public sealed record CombatInjury(string Name, InjuryLocation Location, string Effect, bool DeathSave, int? ActionPenalty, int? MovePenalty);

public sealed record CoverPiece
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Material { get; init; } = "";
    public bool Thick { get; init; }
    public int MaxHp { get; init; }
    public int Hp { get; init; }
}

public sealed record CombatLogEntry(int Round, string? ActorId, string Kind, string Text, DateTime At);
