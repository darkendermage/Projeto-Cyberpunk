import type { PortraitFocus } from '../lib/portraitFocus';
/** Types of /api/table and /api/table/play (Application/Table). */
import type { AttackMode, CombatantKind, CombatSide, PendingView, WeaponView, WoundState } from './encounterTypes';

export type GridType = 'Square' | 'None';
export type TokenVisibility = 'All' | 'Gm' | 'Seats';
export type TemplateShapeName = 'Circle' | 'Square' | 'Cone' | 'Line';
export type TableVisibility = 'Public' | 'Private' | 'Secret' | 'Whisper';
export type RollKind = 'Plain' | 'Check' | 'Damage';

/** Cell size and offset in image pixels; a cell is `metersPerCell` meters (2 m in Cyberpunk RED). */
export interface SceneGrid {
  type: GridType;
  sizePx: number;
  offsetX: number;
  offsetY: number;
  metersPerCell: number;
  color: string;
  opacity: number;
  snap: boolean;
}

export interface BackgroundView { mediaId: string | null; url: string | null; width: number; height: number }

/** Position (top-left) and size in cells, rotation in degrees. */
/** An image over the map (round 5), as this screen sees it. */
export interface SceneImageView {
  id: string; url: string; name: string; x: number; y: number; width: number; height: number; rotation: number; opacity: number; order: number;
  locked: boolean; lockedByGm: boolean; hidden: boolean; ownerSeatId: number | null;
  /** This viewer may move and resize it now. */
  editable: boolean;
}

/** An image placed or changed (mediaId only when adding). */
export interface ImageInput {
  mediaId?: string | null; name?: string | null; x: number; y: number; width?: number | null; height?: number | null;
  rotation?: number; opacity?: number; locked?: boolean; hidden?: boolean;
}

export interface TokenView {
  id: string;
  label: string;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
  locked: boolean;
  imageMediaId: string | null;
  imageUrl: string | null;
  color: string;
  ownerSeatId: number | null;
  visibility: TokenVisibility;
  visibleSeatIds: number[];
  characterId: number | null;
  combatantId: string | null;
  rangeRingMeters: number | null;
  controllable: boolean;
  /** A saved NPC of the generator the token stands for (its image shows on the token). */
  npcId: number | null;
  /** A named NPC of the books (compendium id) the token stands for. */
  compendiumNpcId: string | null;
  /** Scenery sits over the map and under the characters. */
  layer?: TokenLayer;
  barrier?: BarrierView | null;
  /** The framing of a PC's portrait when the token shows it. */
  imageFocus?: PortraitFocus | null;
}

export type TokenLayer = 'Token' | 'Scenery';

/** A barrier of the cover table (CP:R p.182); with a fight, its HP is the fight's cover HP. */
export interface BarrierView { material: string; thick: boolean; maxHp: number; hp: number; coverId: string | null }

export interface BarrierRequest { label: string | null; material: string; thick: boolean; hp: number | null; x: number; y: number; width?: number; height?: number }

/** An area of effect: origin in cells, size in meters, angle in degrees (0 = right, clockwise). */
export interface SceneTemplate {
  id: string;
  shape: TemplateShapeName;
  x: number;
  y: number;
  sizeMeters: number;
  angle: number;
  color: string;
  label: string | null;
  locked: boolean;
  ownerSeatId: number | null;
}

export interface SceneView {
  id: number;
  name: string;
  version: number;
  active: boolean;
  encounterId: number | null;
  grid: SceneGrid;
  background: BackgroundView;
  tokens: TokenView[];
  templates: SceneTemplate[];
  updatedAt: string;
  /** Images over the map, bottom first (round 5). */
  images?: SceneImageView[];
}

export interface SceneSummary {
  id: number;
  name: string;
  active: boolean;
  sortOrder: number;
  encounterId: number | null;
  backgroundUrl: string | null;
  tokens: number;
  updatedAt: string;
}

export interface TokenInput {
  label: string;
  x: number;
  y: number;
  width?: number;
  height?: number;
  rotation?: number;
  locked?: boolean;
  imageMediaId?: string | null;
  color?: string | null;
  ownerSeatId?: number | null;
  visibility?: TokenVisibility;
  visibleSeatIds?: number[];
  characterId?: number | null;
  combatantId?: string | null;
  rangeRingMeters?: number | null;
  npcId?: number | null;
  compendiumNpcId?: string | null;
  layer?: TokenLayer;
}

/** A selection changed at once (see the batch routes). */
export interface SceneBatch {
  moveTokens?: { id: string; x: number; y: number }[];
  moveTemplates?: { id: string; x: number; y: number }[];
  removeTokens?: string[];
  removeTemplates?: string[];
  hideTokens?: string[];
  showTokens?: string[];
}

export interface TokenMove { x: number; y: number; rotation?: number; width?: number; height?: number }

export interface TemplateInput {
  shape: TemplateShapeName;
  x: number;
  y: number;
  sizeMeters: number;
  angle?: number;
  color?: string | null;
  label?: string | null;
  locked?: boolean;
}

export interface Seat {
  id: number;
  label: string;
  color: string;
  characterId: number | null;
  token: string;
  createdAt: string;
  revoked: boolean;
  /** Someone holds the seat (picked it at the entrance or opened its link) since the app started. */
  claimed: boolean;
}

/** A free seat at the general link's entrance (never its key). */
export interface LobbySeat { id: number; label: string; color: string; characterName: string | null; hasPortrait: boolean }

/** The entrance: closed lists nothing; open lists the free seats. */
export interface Lobby { open: boolean; seats: LobbySeat[] }

export interface ClaimResult { token: string }

export interface PlayerSeat { id: number; label: string; color: string; characterId: number | null; diceColor?: string | null }

export interface PlayerState { seat: PlayerSeat; scene: SceneView | null; music?: TableMusic | null }

/** What the table is playing (YouTube), as of updatedAt. */
export interface TableMusic {
  videoId: string | null; playlistId: string | null; title: string | null; playing: boolean; positionSeconds: number; updatedAt: string;
  volume: number; index: number;
}

export interface TableMusicState { music: TableMusic | null; recent: { videoId: string | null; playlistId: string | null; title: string | null }[] }

export interface MediaInfo { id: string; url: string; contentType: string; length: number }

export type TunnelStateName = 'Missing' | 'Downloading' | 'Stopped' | 'Starting' | 'Running' | 'Failed';
export interface TunnelStatus { state: TunnelStateName; url: string | null; error: string | null; localUrl: string | null }

/** A fighter as a player sees it: numbers and weapons only for their own character. */
export interface PlayerFighter {
  id: string;
  name: string;
  side: CombatSide;
  kind: CombatantKind;
  initiative: number | null;
  woundState: WoundState;
  dead: boolean;
  stabilized: boolean;
  conditions: string[];
  injuries: string[];
  characterId: number | null;
  own: boolean;
  hp: number | null;
  maxHp: number | null;
  spBody: number | null;
  spHead: number | null;
  maxSpBody: number | null;
  maxSpHead: number | null;
  mustDeathSave: boolean | null;
  deathSaveTarget: number | null;
  weapons: WeaponView[] | null;
  /** The own fighter's once-a-Round effects already used (Combat Awareness). */
  roundFlags?: string[] | null;
}

export interface PlayerEncounter {
  id: number; name: string; round: number; activeId: string | null; queue: PlayerFighter[];
  /** An attack waiting on a dodge or a Critical Injury roll; pendingMine when this seat must answer it. */
  pending?: PendingView | null;
  pendingMine?: boolean;
}

export interface PlayerSkill { key: string; skillId: string; name: string; namePt: string | null; specialization: string | null; category: string; total: number }

export interface PlayerCharacter {
  id: number;
  name: string;
  handle: string | null;
  hp: number;
  maxHp: number;
  luck: number;
  luckAvailable: number;
  humanity: number;
  stats: { key: string; value: number }[];
  skills: PlayerSkill[];
  /** Equipped weapons, from the sheet (in a fight the fighter's own list is used). */
  weapons: PlayerWeapon[];
  armor: PlayerArmor;
}

/** An equipped weapon: attackTotal is the skill total (add 1d10). */
export interface PlayerWeapon { key: string; name: string; attackTotal: number; damage: string | null; magazine: number | null; rounds: number; autofireTotal: number | null }

/** SP worn on the head and the body; the max is null where nothing can be repaired. */
export interface PlayerArmor { spHead: number; maxSpHead: number | null; spBody: number; maxSpBody: number | null }

export type ArmorLocation = 'Head' | 'Body';

/** The seat's own sheet, read-only at the table. */
export interface PlayerSheet {
  name: string;
  handle: string | null;
  roles: string[];
  hp: number;
  maxHp: number;
  humanity: number;
  maxHumanity: number;
  stats: { key: string; value: number }[];
  armor: PlayerArmor;
  headArmor: string | null;
  bodyArmor: string | null;
  weapons: PlayerSheetWeapon[];
  cyberware: string[];
  gear: { name: string; quantity: number }[];
  skills: PlayerSheetSkill[];
}
export interface PlayerSheetWeapon { name: string; damage: string | null; rof: number | null; magazine: number | null; rounds: number; equipped: boolean }
export interface PlayerSheetSkill { name: string; namePt: string | null; specialization: string | null; category: string; level: number; total: number }

/** A PC on the scene, for the strip under the map. */
export interface PartyMember {
  characterId: number;
  name: string;
  hp: number;
  maxHp: number;
  woundState: WoundState;
  spHead: number;
  maxSpHead: number | null;
  spBody: number;
  maxSpBody: number | null;
  /** The seat that plays this PC. */
  seatId: number;
  /** Its player is connected now. */
  online: boolean;
  /** The PC has a token on the scene being looked at. */
  inScene: boolean;
  imageUrl: string | null;
}

export interface RolledTerm { expression: string; sign: number; dice: number[]; extra: number | null; value: number }

export interface RollResult {
  formula: string;
  kind: RollKind;
  terms: RolledTerm[];
  total: number;
  critical: 'Success' | 'Failure' | null;
  criticalInjury: boolean;
  /** LUCK points spent on it, already in the total. */
  luck?: number;
  /** The roll this one replaces (the 1-LUCK reroll). */
  rerollOf?: number | null;
  /** The roller's dice color (#rrggbb); null for the default dice. */
  diceColor?: string | null;
}

/** A session of the table (its chat and the fights used in it). */
export interface TableSession {
  id: number; name: string; startedAt: string; endedAt: string | null; open: boolean; messages: number;
  fights: { id: number; name: string; status: string }[];
}

export interface TableMessage {
  id: number;
  at: string;
  authorSeatId: number | null;
  authorName: string;
  authorColor: string | null;
  kind: 'Text' | 'Roll' | 'System' | 'Attack';
  visibility: TableVisibility;
  targetSeatId: number | null;
  text: string;
  roll: RollResult | null;
  /** A secret roll seen by its author: they rolled, but not what came up. */
  concealed: boolean;
  /** Attack lines: the attack of the fight on the live scene. */
  attack?: TableAttack | null;
}

/** One target of a table attack; players get null numbers on NPCs (a hit or a miss only). */
export interface TableAttackTarget {
  id: string;
  name: string;
  pc: boolean;
  hit: boolean;
  damage: number | null;
  through: number | null;
  location: string;
  dv: number | null;
  defenseRoll: number | null;
  damageDice: number[];
  criticalInjury: string | null;
  /** Combat Awareness lines that changed the damage (Spot Weakness, Damage Deflection). */
  notes?: string[] | null;
}

/** An attack of the fight on the live scene: the chat card, the effect on the map and the dice. */
export interface TableAttack {
  encounterId: number;
  seq: number;
  attackerId: string;
  attackerName: string;
  attackerPc: boolean;
  weapon: string;
  mode: AttackMode;
  melee: boolean;
  attackRoll: number | null;
  attackTotal: number;
  targets: TableAttackTarget[];
  undone: boolean;
  /** The extra d10 of a natural 10 (positive) or 1 (negative), CP:R p.130. */
  attackExtra?: number | null;
  /** The Critical Injury's 2d6, when it was rolled. */
  critDice?: number[] | null;
  /** The attacker's dice color. */
  diceColor?: string | null;
  /** Combat Awareness lines of the attack itself (Precision Attack). */
  notes?: string[] | null;
}

/** A quick attack's answer: the attack, or the step it waits on. */
export interface QuickAttackResult { attack: TableAttack | null; pending: PendingView | null }

export interface QuickAttack { weapon: string; mode: AttackMode; targetIds: string[] }

export interface SendMessage { text: string; visibility?: TableVisibility; targetSeatId?: number | null }
export interface RollRequest {
  /** LUCK points a player spends on their own Check (+1 each). */
  luck?: number; formula: string; kind?: RollKind; label?: string | null; visibility?: TableVisibility; targetSeatId?: number | null }

/** Live previews and pings pushed by the hub. */
export interface TokenDrag { sceneId: number; tokenId: string; x: number; y: number }
export interface Ping { sceneId: number; x: number; y: number; color: string; label: string }

/** The book's tables to look up at the table. */
export interface RuleNote { title: string; text: string; source: string }
export interface CoverMaterialRule { id: string; label: string; thick: number; thin: number }
export interface CoverExampleRule { label: string; material: string; thick: boolean; hp: number }
export interface TableRules {
  criticalInjuries: import('./characterTypes').CriticalInjuryRule[];
  coverMaterials: CoverMaterialRule[];
  coverMaterialsSource: string;
  coverExamples: CoverExampleRule[];
  coverExamplesSource: string;
  notes: RuleNote[];
}
