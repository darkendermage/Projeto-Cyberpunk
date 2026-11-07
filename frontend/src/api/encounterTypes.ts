/** Types of /api/encounters (Encounters/EncounterContracts.cs). */
import type { Ruled } from './nightMarketTypes';

export type CombatantKind = 'Pc' | 'Npc' | 'Generic';
export type CombatSide = 'Ally' | 'Enemy';
export type EncounterStatus = 'Active' | 'Ended';
export type WoundState = 'Unhurt' | 'Lightly' | 'Seriously' | 'Mortally';
export type InjuryLocation = 'Body' | 'Head';
export type AttackMode = 'Single' | 'Autofire' | 'AimedHead' | 'AimedHand' | 'AimedLeg' | 'ShotgunShell' | 'Explosive' | 'Suppressive';

export interface WeaponView {
  key: string;
  name: string;
  class: string | null;
  skill: string;
  attackTotal: number;
  autofireTotal: number | null;
  damage: string;
  rof: number;
  rounds: number | null;
  magazine: number | null;
  autofireMultiplier: number | null;
  /** The sheet prints "Autofire only": no normal or aimed shots. */
  autofireOnly?: boolean | null;
  melee: boolean;
  halvesArmor: boolean;
  /** CP:R p.344: Excellent +1 to attacks, Poor jams on a natural 1. */
  quality?: 'Poor' | 'Standard' | 'Excellent';
  /** Jammed by a natural 1: no attack until an Action unjams it. */
  jammed?: boolean;
  /** How attackTotal is made: the skill, then each bonus. */
  attackParts?: { label: string; value: number }[] | null;
  /** Attacks only by throwing (the Ranger boomerang). */
  thrownOnly?: boolean;
  /** Out of hand after a throw: back with a Targeting Scope, or picked up. */
  thrown?: boolean;
}

export interface InjuryView {
  name: string;
  location: InjuryLocation;
  effect: string | null;
  deathSave: boolean;
}

export interface FighterView {
  id: string;
  kind: CombatantKind;
  name: string;
  side: CombatSide;
  initiative: number | null;
  hp: number;
  maxHp: number;
  woundState: WoundState;
  actionPenalty: number;
  stabilizationDv: number | null;
  spBody: number;
  spHead: number;
  maxSpBody: number;
  maxSpHead: number;
  ref: number;
  dex: number;
  body: number;
  will: number;
  move: number;
  evasion: number;
  athletics: number;
  concentration: number;
  weapons: WeaponView[];
  injuries: InjuryView[];
  conditions: string[];
  deathSavePenalty: number;
  deathSaveTarget: number;
  mustDeathSave: boolean;
  dead: boolean;
  stabilized: boolean;
  coverId: string | null;
  characterId: number | null;
  characterVersion: number | null;
  source: string | null;
  /** NPCs and generic enemies: STATs and Skills of the snapshot (PCs read their own sheet). */
  stats?: NamedValue[] | null;
  skills?: NamedValue[] | null;
}

export interface NamedValue { id: string; label: string; value: number }

export interface CoverPiece {
  id: string;
  label: string;
  material: string;
  thick: boolean;
  maxHp: number;
  hp: number;
}

export interface CombatLogEntry {
  round: number;
  actorId: string | null;
  kind: string;
  text: string;
  at: string;
}

export interface TargetResult {
  targetId: string;
  targetName: string;
  hit: boolean;
  attack: number;
  dv: number | null;
  defenseRoll: number | null;
  defense: string;
  damageDice: number[];
  damage: number;
  location: string;
  coverAbsorbed: number | null;
  coverDestroyed: boolean;
  spBefore: number | null;
  through: number | null;
  critical: boolean;
  criticalInjury: string | null;
  woundState: string | null;
  notes: string[];
}

export interface AttackResult {
  attacker: string;
  weapon: string;
  mode: AttackMode;
  attackRoll: number | null;
  attackTotal: number;
  targets: TargetResult[];
  notes: string[];
  attackerId: string;
  /** The fight's attack number. */
  seq: number;
  melee: boolean;
}

export interface EncounterDetail {
  id: number;
  name: string;
  version: number;
  status: EncounterStatus;
  round: number;
  activeId: string | null;
  createdAt: string;
  updatedAt: string;
  queue: FighterView[];
  covers: CoverPiece[];
  log: CombatLogEntry[];
  notes: string | null;
  lastAttack: AttackResult | null;
  notices: string[];
  /** Set on the answer of an undo: the attack number taken back. */
  undoneAttack?: number | null;
  /** An attack waiting on the target's dodge or the attacker's Critical Injury roll. */
  pending?: PendingView | null;
}

export type PendingStep = 'Defense' | 'CriticalInjury';

/** A pending attack (no attack total: the target decides without it). */
export interface PendingView {
  seq: number;
  attackerId: string;
  attackerName: string;
  weapon: string;
  targetId: string;
  targetName: string;
  step: PendingStep;
  /** The range DV the target faces if they don't dodge. */
  rangeDv: number | null;
  /** The target's Evasion (DEX + skill) if they dodge. */
  evasion: number | null;
  attackerCharacterId: number | null;
  targetCharacterId: number | null;
}

export interface EncounterSummary {
  id: number;
  name: string;
  status: EncounterStatus;
  round: number;
  allies: number;
  enemies: number;
  down: number;
  updatedAt: string;
}

export interface CombatRulesDocument {
  about: string;
  initiative: Ruled<string>;
  initiativeTies: Ruled<string>;
  turn: Ruled<string>;
  actions: Ruled<{ name: string; label: string; text: string }[]>;
  rangeBands: Ruled<{ min: number; max: number }[]>;
  singleShot: Ruled<{ key: string; label: string; classes: string[]; dv: (number | null)[] }[]>;
  dodgeRef: Ruled<number>;
  aimedShot: Ruled<{ penalty: number; maxRof: number; headMultiplier: number; crackedSkullMultiplier: number }>;
  aimedEffects: Ruled<{ mode: AttackMode; label: string; effect: string }[]>;
  melee: Ruled<{ check: string; halveArmor: boolean; reachMeters: number; veryHeavyRof: number }>;
  grapple: Ruled<string>;
  thrown: Ruled<{ maxMeters: number; row: string; check: string }>;
  shotgunShell: Ruled<{ dv: number; damage: string; rangeMeters: number }>;
  explosives: Ruled<string>;
  suppressive: Ruled<{ check: string; rangeMeters: number; effect: string }>;
  coverMaterials: Ruled<{ id: string; label: string; thick: number; thin: number }[]>;
  coverExamples: Ruled<{ label: string; material: string; thick: boolean; hp: number }[]>;
  coverRules: Ruled<string>;
  shields: Ruled<string>;
  environment: Ruled<{ id: string; label: string; damage: string; bypassArmor: boolean; note: string }[]>;
  generic: Ruled<{ ref: number; dex: number; body: number; hp: number; spBody: number; spHead: number; attackBase: number; evasionBase: number; damage: string; weaponClass: string }>;
}

export interface EncounterRules {
  rules: CombatRulesDocument;
  characterCombat: unknown;
}
