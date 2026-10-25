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
/// enemies (<see cref="Generate"/>) or quick generic ones (<see cref="Generic"/>). <see cref="Count"/> copies NPCs.
/// </summary>
public sealed record AddCombatantsRequest(
    int Version, CombatSide Side, int? CharacterId = null, int? NpcId = null, Npcs.Generation.NpcGenerationRequest? Generate = null,
    GenericRequest? Generic = null, int Count = 1, string? Name = null);

/// <summary>Initiative for everyone (or the listed combatants); typed d10s win over rolled ones.</summary>
public sealed record InitiativeRequest(int Version, Dictionary<string, int>? Rolls = null, List<string>? Only = null);
public sealed record SetInitiativeRequest(int Version, int Initiative);

/// <summary>
/// One attack (CP:R p.170-178). <see cref="Weapon"/> is a weapon key of the attacker, "brawling", a Martial Arts key or
/// "thrown". Any die left empty is rolled by the server.
/// </summary>
public sealed record AttackRequest(
    int Version, string AttackerId, List<string> TargetIds, string Weapon, AttackMode Mode = AttackMode.Single, int? Range = null,
    bool Dodge = false, int Modifier = 0, int? AttackRoll = null, Dictionary<string, int>? DefenseRolls = null,
    List<int>? DamageDice = null, string? ThrownDamage = null, bool IgnoreCover = false, int? CritRoll = null);

public sealed record ManualDamageRequest(
    int Version, int Amount, InjuryLocation Location = InjuryLocation.Body, bool BypassArmor = false, bool HalveArmor = false,
    bool Critical = false, string? Description = null);
public sealed record EnvironmentDamageRequest(int Version, string EnvironmentId, List<int>? Dice = null);
public sealed record HealCombatantRequest(int Version, int Amount);
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

public sealed record AttackResult(string Attacker, string Weapon, AttackMode Mode, int? AttackRoll, int AttackTotal, IReadOnlyList<TargetResult> Targets, IReadOnlyList<string> Notes);

public sealed record CombatantDto(FighterView View, CombatantKind Kind, bool Active);

public sealed record EncounterDetailDto(
    int Id, string Name, int Version, EncounterStatus Status, int Round, string? ActiveId, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<FighterView> Queue, IReadOnlyList<CoverPiece> Covers, IReadOnlyList<CombatLogEntry> Log, string? Notes,
    AttackResult? LastAttack, IReadOnlyList<string> Notices);

public sealed record EncounterSummaryDto(int Id, string Name, EncounterStatus Status, int Round, int Allies, int Enemies, int Down, DateTime UpdatedAt);

public sealed record EncounterRulesDto(CombatRulesDocument Rules, object CharacterCombat);
