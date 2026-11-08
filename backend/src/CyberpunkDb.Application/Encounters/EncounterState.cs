using System.Text.Json.Serialization;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Encounters;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CombatantKind { Pc, Npc, Generic }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CombatSide { Ally, Enemy }

// stored as JSON
public sealed record EncounterState
{
    public List<Combatant> Combatants { get; init; } = [];
    public int Round { get; init; } = 1;
    // null until Initiative is rolled
    public string? ActiveId { get; init; }
    public List<CoverPiece> Covers { get; init; } = [];
    public List<CombatLogEntry> Log { get; init; } = [];
    public string? Notes { get; init; }
    // lets the table tell a new attack from one it already showed
    public int AttackSeq { get; init; }
    // cleared as soon as anything else happens
    public AttackUndo? Undo { get; init; }
    // Attack waiting on the target's dodge (REF 8+) or the attacker's crit roll. Blocks the fight until resolved or
    // cancelled by the GM.
    public PendingAttack? Pending { get; init; }

    public Combatant? Find(string? id) => Combatants.FirstOrDefault(c => c.Id == id);

    // highest first, ties by the re-roll (CP:R p.168)
    [JsonIgnore]
    public IEnumerable<Combatant> Queue => Combatants
        .OrderByDescending(c => c.Initiative ?? int.MinValue)
        .ThenByDescending(c => c.TieBreak)
        .ThenBy(c => c.Id, StringComparer.Ordinal);
}

// A PC is just a link to the sheet (HP, armor, injuries and ammo live there). NPCs carry a snapshot plus their own
// running state.
public sealed record Combatant
{
    public string Id { get; init; } = "";
    public CombatantKind Kind { get; init; }
    public string Name { get; init; } = "";
    public CombatSide Side { get; init; }
    public int? Initiative { get; init; }
    // only used to order ties
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
    // Prone, Grappled, Unconscious, "OnFire:fire-strong"...
    public List<string> Conditions { get; init; } = [];
    public string? CoverId { get; init; }
    // NPCs only, PCs use the sheet
    public Dictionary<string, int> Ammo { get; init; } = [];
    // Poor Quality natural 1 (CP:R p.344). PCs keep this on the sheet.
    public List<string> Jammed { get; init; } = [];
    // Ranger boomerang etc, until it comes back or gets picked up
    public List<string> Thrown { get; init; } = [];
    // "spotWeakness", "damageDeflection"; cleared each Round
    public List<string> RoundFlags { get; init; } = [];
}

// taken when the NPC joins
public sealed record CombatSnapshot
{
    public int? NpcId { get; init; }
    // named NPC from the books (for the token image)
    public string? CompendiumId { get; init; }
    public string Source { get; init; } = "";
    public int MaxHp { get; init; }
    // Death Save and unarmed damage
    public int Body { get; init; }
    public Dictionary<string, int> Stats { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    // STAT + level, armor penalty included
    public Dictionary<string, int> Skills { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public int MaxSpBody { get; init; }
    public int MaxSpHead { get; init; }
    // sheet prints full SP vs melee: halving attacks don't halve it
    public bool? FullSpVsMeleeBody { get; init; }
    public bool? FullSpVsMeleeHead { get; init; }
    public bool Cyberarm { get; init; }
    public List<CombatWeapon> Weapons { get; init; } = [];
}

public sealed record CombatWeapon
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public WeaponClass? Class { get; init; }
    public string Skill { get; init; } = "";
    public string Damage { get; init; } = "";
    public int Rof { get; init; } = 1;
    public int? Magazine { get; init; }
    public int? AutofireMultiplier { get; init; }
    // "Autofire only" on the sheet
    public bool? AutofireOnly { get; init; }
    // printed DV per range band, overrides the general tables
    public int?[]? RangeDv { get; init; }
    public bool Melee { get; init; }
    // null -> same as Melee
    public bool? HalvesArmor { get; init; }
    // generic enemies: STAT + skill, without the d10
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

// Valid while the fight is still at EncounterVersion and each sheet at the version the attack left it.
public sealed record AttackUndo(int Seq, int EncounterVersion, string State, List<SheetSnapshot> Sheets);

public sealed record SheetSnapshot(int CharacterId, int Version, string SheetJson);

public sealed record CombatLogEntry(int Round, string? ActorId, string Kind, string Text, DateTime At);
