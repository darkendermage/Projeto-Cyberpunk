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
