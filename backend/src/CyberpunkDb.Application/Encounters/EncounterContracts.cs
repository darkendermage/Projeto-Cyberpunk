using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Encounters;

public sealed record CreateEncounterRequest(string? Name);
public sealed record EncounterVersionRequest(int Version);
public sealed record UpdateEncounterRequest(int Version, string Name, string? Notes);

// empty fields take the combat-rules.json defaults
public sealed record GenericRequest(
    string Name, int? Ref = null, int? Dex = null, int? Body = null, int? Will = null, int? Move = null, int? Hp = null,
    int? SpBody = null, int? SpHead = null, int? EvasionBase = null, int? AttackBase = null, string? WeaponName = null,
    WeaponClass? WeaponClass = null, string? Damage = null, int? Rof = null);

// One of CharacterId, NpcId, Generate, CompendiumNpcId or Generic. Count copies NPCs.
public sealed record AddCombatantsRequest(
    int Version, CombatSide Side, int? CharacterId = null, int? NpcId = null, Npcs.Generation.NpcGenerationRequest? Generate = null,
    GenericRequest? Generic = null, int Count = 1, string? Name = null, string? CompendiumNpcId = null);

// typed d10s win over rolled ones
public sealed record InitiativeRequest(int Version, Dictionary<string, int>? Rolls = null, List<string>? Only = null);
public sealed record SetInitiativeRequest(int Version, int Initiative);
// null BeforeId = bottom
public sealed record MoveInQueueRequest(int Version, string? BeforeId = null);

// CP:R p.170-178. Weapon is a weapon key, "brawling", a Martial Arts key or "thrown". Empty dice get rolled.
public sealed record AttackRequest(
    int Version, string AttackerId, List<string> TargetIds, string Weapon, AttackMode Mode = AttackMode.Single, int? Range = null,
    bool Dodge = false, int Modifier = 0, int? AttackRoll = null, Dictionary<string, int>? DefenseRolls = null,
    List<int>? DamageDice = null, string? ThrownDamage = null, bool IgnoreCover = false, int? CritRoll = null,
    // extra d10 on a natural 10 or 1 (CP:R p.130)
    int? AttackExtra = null,
    // REF 8+: their Evasion roll is the DV (CP:R p.172)
    List<string>? DodgeTargets = null,
    // the crit 2d6 when rolled step by step (CritRoll is the sum)
    List<int>? CritDice = null,
    // wait for dodge / crit roll instead of rolling them right away
    bool Interactive = false,
    // weapon-rules.json mode (Slug/Shell, Burst, Throw...); null = the weapon's current one
    string? WeaponMode = null);

public sealed record ManualDamageRequest(
    int Version, int Amount, InjuryLocation Location = InjuryLocation.Body, bool BypassArmor = false, bool HalveArmor = false,
    bool Critical = false, string? Description = null);
public sealed record EnvironmentDamageRequest(int Version, string EnvironmentId, List<int>? Dice = null);
public sealed record HealCombatantRequest(int Version, int Amount);
// an Action (CP:R p.344)
public sealed record UnjamRequest(int Version, string Weapon);
// an Action (CP:R p.171). Magazine: an extra one from weapon-rules.json, null = the main one.
public sealed record ReloadCombatantRequest(int Version, string Weapon, string? Magazine = null);
// modes that stay on the weapon (Hypurrburst, AVAR railgun...): an Action, a Move Action or a minute
public sealed record WeaponModeSwitchRequest(int Version, string Weapon, string Mode);
// ablate or repair, within full SP
public sealed record CombatantArmorRequest(int Version, Npcs.ArmorSlot Location, int Delta);
public sealed record DeathSaveCombatantRequest(int Version, int? Roll = null);
public sealed record ConditionRequest(int Version, string Condition, bool On);
public sealed record CoverRequest(int Version, string Label, string Material, bool Thick, int? Hp = null);
public sealed record TakeCoverRequest(int Version, string? CoverId);
public sealed record CoverDamageRequest(int Version, int Amount);

// for the log and the result panel
public sealed record TargetResult(
    string TargetId, string TargetName, bool Hit, int Attack, int? Dv, int? DefenseRoll, string Defense,
    IReadOnlyList<int> DamageDice, int Damage, string Location, int? CoverAbsorbed, bool CoverDestroyed,
    int? SpBefore, int? Through, bool Critical, string? CriticalInjury, string? WoundState, IReadOnlyList<string> Notes);

public sealed record AttackResult(string Attacker, string Weapon, AttackMode Mode, int? AttackRoll, int AttackTotal, IReadOnlyList<TargetResult> Targets, IReadOnlyList<string> Notes,
    string AttackerId = "", int Seq = 0, bool Melee = false, int? AttackExtra = null, IReadOnlyList<int>? CritDice = null);

public sealed record CombatantDto(FighterView View, CombatantKind Kind, bool Active);

public sealed record EncounterDetailDto(
    int Id, string Name, int Version, EncounterStatus Status, int Round, string? ActiveId, DateTime CreatedAt, DateTime UpdatedAt,
    IReadOnlyList<FighterView> Queue, IReadOnlyList<CoverPiece> Covers, IReadOnlyList<CombatLogEntry> Log, string? Notes,
    AttackResult? LastAttack, IReadOnlyList<string> Notices, int? UndoneAttack = null, PendingView? Pending = null);

public sealed record EncounterSummaryDto(int Id, string Name, EncounterStatus Status, int Round, int Allies, int Enemies, int Down, DateTime UpdatedAt);

public sealed record EncounterRulesDto(CombatRulesDocument Rules, object CharacterCombat);

// dodge (Evasion roll as DV) or take the range DV
public sealed record PendingDefenseRequest(int Version, string TargetId, bool Dodge);

public sealed record PendingCriticalRequest(int Version, string TargetId);
