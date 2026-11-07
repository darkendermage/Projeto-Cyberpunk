using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Encounters;

public sealed record CreateEncounterRequest(string? Name);
public sealed record EncounterVersionRequest(int Version);
public sealed record UpdateEncounterRequest(int Version, string Name, string? Notes);

/// <summary>A quick enemy without a sheet; anything left empty takes the defaults of combat-rules.json.</summary>
public sealed record GenericRequest(
    string Name, int? Ref = null, int? Dex = null, int? Body = null, int? Will = null, int? Move = null, int? Hp = null,
    int? SpBody = null, int? SpHead = null, int? EvasionBase = null, int? AttackBase = null, string? WeaponName = null,
    WeaponClass? WeaponClass = null, string? Damage = null, int? Rof = null);

/// <summary>
/// Adds combatants: a saved character (<see cref="CharacterId"/>), a saved NPC (<see cref="NpcId"/>), freshly generated
/// enemies (<see cref="Generate"/>), a named NPC of the books (<see cref="CompendiumNpcId"/>) or quick generic ones (<see cref="Generic"/>). <see cref="Count"/> copies NPCs.
/// </summary>
public sealed record AddCombatantsRequest(
    int Version, CombatSide Side, int? CharacterId = null, int? NpcId = null, Npcs.Generation.NpcGenerationRequest? Generate = null,
    GenericRequest? Generic = null, int Count = 1, string? Name = null, string? CompendiumNpcId = null);

/// <summary>Initiative for everyone (or the listed combatants); typed d10s win over rolled ones.</summary>
public sealed record InitiativeRequest(int Version, Dictionary<string, int>? Rolls = null, List<string>? Only = null);
public sealed record SetInitiativeRequest(int Version, int Initiative);
/// <summary>Drag in the Initiative Queue: the combatant goes right before <see cref="BeforeId"/> (null: to the bottom).</summary>
public sealed record MoveInQueueRequest(int Version, string? BeforeId = null);

/// <summary>
/// One attack (CP:R p.170-178). <see cref="Weapon"/> is a weapon key of the attacker, "brawling", a Martial Arts key or
/// "thrown". Any die left empty is rolled by the server.
/// </summary>
public sealed record AttackRequest(
    int Version, string AttackerId, List<string> TargetIds, string Weapon, AttackMode Mode = AttackMode.Single, int? Range = null,
    bool Dodge = false, int Modifier = 0, int? AttackRoll = null, Dictionary<string, int>? DefenseRolls = null,
    List<int>? DamageDice = null, string? ThrownDamage = null, bool IgnoreCover = false, int? CritRoll = null,
    // The extra d10 of a natural 10 or 1 (CP:R p.130), typed like the other dice; rolled when missing.
    int? AttackExtra = null,
    // Targets that chose to dodge (REF 8+): their Evasion roll is the DV (CP:R p.172).
    List<string>? DodgeTargets = null,
    // The Critical Injury's 2d6, when rolled step by step (CritRoll is their sum).
    List<int>? CritDice = null,
    // Stop for the target's dodge and the attacker's Critical Injury roll instead of rolling them at once.
    bool Interactive = false);

public sealed record ManualDamageRequest(
    int Version, int Amount, InjuryLocation Location = InjuryLocation.Body, bool BypassArmor = false, bool HalveArmor = false,
    bool Critical = false, string? Description = null);
public sealed record EnvironmentDamageRequest(int Version, string EnvironmentId, List<int>? Dice = null);
public sealed record HealCombatantRequest(int Version, int Amount);
/// <summary>Clears a jammed weapon (an Action, CP:R p.344).</summary>
public sealed record UnjamRequest(int Version, string Weapon);
/// <summary>Reloads one of the fighter's weapons (an Action, CP:R p.171).</summary>
public sealed record ReloadCombatantRequest(int Version, string Weapon);
/// <summary>SP of one location up or down (ablation or repair), within the armor's full SP.</summary>
public sealed record CombatantArmorRequest(int Version, Npcs.ArmorSlot Location, int Delta);
public sealed record DeathSaveCombatantRequest(int Version, int? Roll = null);
public sealed record ConditionRequest(int Version, string Condition, bool On);
public sealed record CoverRequest(int Version, string Label, string Material, bool Thick, int? Hp = null);
public sealed record TakeCoverRequest(int Version, string? CoverId);
public sealed record CoverDamageRequest(int Version, int Amount);

/// <summary>What one attack did to one target, step by step, for the log and the result panel.</summary>
public sealed record TargetResult(
    string TargetId, string TargetName, bool Hit, int Attack, int? Dv, int? DefenseRoll, string Defense,
    IReadOnlyList<int> DamageDice, int Damage, string Location, int? CoverAbsorbed, bool CoverDestroyed,
    int? SpBefore, int? Through, bool Critical, string? CriticalInjury, string? WoundState, IReadOnlyList<string> Notes);

/// <summary>An attack's outcome; <see cref="Seq"/> numbers the fight's attacks, <see cref="Melee"/> tells a blade from a gun.</summary>
public sealed record AttackResult(string Attacker, string Weapon, AttackMode Mode, int? AttackRoll, int AttackTotal, IReadOnlyList<TargetResult> Targets, IReadOnlyList<string> Notes,
    string AttackerId = "", int Seq = 0, bool Melee = false, int? AttackExtra = null, IReadOnlyList<int>? CritDice = null);

public sealed record CombatantDto(FighterView View, CombatantKind Kind, bool Active);

public sealed record EncounterDetailDto(
    int Id, string Name, int Version, EncounterStatus Status, int Round, string? ActiveId, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<FighterView> Queue, IReadOnlyList<CoverPiece> Covers, IReadOnlyList<CombatLogEntry> Log, string? Notes,
    AttackResult? LastAttack, IReadOnlyList<string> Notices, int? UndoneAttack = null, PendingView? Pending = null);

public sealed record EncounterSummaryDto(int Id, string Name, EncounterStatus Status, int Round, int Allies, int Enemies, int Down, DateTime UpdatedAt);

public sealed record EncounterRulesDto(CombatRulesDocument Rules, object CharacterCombat);

/// <summary>The target's choice on a pending attack: dodge (roll Evasion as the DV) or take the range DV.</summary>
public sealed record PendingDefenseRequest(int Version, string TargetId, bool Dodge);

/// <summary>The attacker's Critical Injury roll on a pending attack.</summary>
public sealed record PendingCriticalRequest(int Version, string TargetId);
