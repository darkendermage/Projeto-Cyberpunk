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
  melee: boolean;
  halvesArmor: boolean;
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
}

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
