/** Types of /api/table and /api/table/play (Application/Table). */
import type { CombatantKind, CombatSide, WeaponView, WoundState } from './encounterTypes';

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
}

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

export interface PlayerSeat { id: number; label: string; color: string; characterId: number | null }

export interface PlayerState { seat: PlayerSeat; scene: SceneView | null }

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
}

export interface PlayerEncounter { id: number; name: string; round: number; activeId: string | null; queue: PlayerFighter[] }

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
}

export interface RolledTerm { expression: string; sign: number; dice: number[]; extra: number | null; value: number }

export interface RollResult {
  formula: string;
  kind: RollKind;
  terms: RolledTerm[];
  total: number;
  critical: 'Success' | 'Failure' | null;
  criticalInjury: boolean;
}

export interface TableMessage {
  id: number;
  at: string;
  authorSeatId: number | null;
  authorName: string;
  authorColor: string | null;
  kind: 'Text' | 'Roll' | 'System';
  visibility: TableVisibility;
  targetSeatId: number | null;
  text: string;
  roll: RollResult | null;
  /** A secret roll seen by its author: they rolled, but not what came up. */
  concealed: boolean;
}

export interface SendMessage { text: string; visibility?: TableVisibility; targetSeatId?: number | null }
export interface RollRequest { formula: string; kind?: RollKind; label?: string | null; visibility?: TableVisibility; targetSeatId?: number | null }

/** Live previews and pings pushed by the hub. */
export interface TokenDrag { sceneId: number; tokenId: string; x: number; y: number }
export interface Ping { sceneId: number; x: number; y: number; color: string; label: string }
