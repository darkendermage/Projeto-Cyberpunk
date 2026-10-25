/** Types of /api/net-architectures (NetArchitectures/NetArchitectureContracts.cs). */
import type { PriceCategory, Ruled } from './nightMarketTypes';

export type NetDifficulty = 'Basic' | 'Standard' | 'Uncommon' | 'Advanced';
export type NetContentKind = 'BlackIce' | 'Password' | 'File' | 'ControlNode';
export type DefenseGroup = 'Active' | 'Emplaced' | 'Environmental';

export interface DifficultyRule {
  id: NetDifficulty;
  label: string;
  dv: number;
  fightingChance: number;
  mightDie: number | null;
  description: string;
}

export interface TableContent {
  kind: NetContentKind;
  slug?: string | null;
  dv?: number | null;
}

export interface TableRow {
  roll: number;
  contents: TableContent[];
}

export interface DemonRule {
  id: string;
  name: string;
  rez: number;
  interface: number;
  netActions: number;
  combatNumber: number;
  cost: number;
  priceCategory: PriceCategory;
  icon: string;
}

export interface DefenseRule {
  id: string;
  name: string;
  label: string;
  group: DefenseGroup;
  page: number;
  counterDv: number;
  counterTime: string;
  trigger: string;
  data: string;
  description: string;
  loadoutOptions: string[];
  loadoutPicks: number;
  requiresDemon: boolean;
}

export interface DeviceRule {
  id: string;
  label: string;
}

export interface FloorBand {
  min: number;
  max: number;
  maxControlNodes: number | null;
  portable: boolean;
  costPerFloor: number;
  priceCategory: PriceCategory;
}

export interface DvCost {
  dv: number;
  cost: number;
  priceCategory: PriceCategory;
}

export interface LocationRule {
  id: string;
  label: string;
  description: string;
  defenseShare: number;
  demonChance: number;
  defenses: Record<string, number>;
  devices: Record<string, number>;
  fileIdeas: string[];
}

export interface NetArchitectureRulesDocument {
  about: string;
  books: Record<string, string>;
  difficulties: Ruled<DifficultyRule[]>;
  shape: {
    floorDice: Ruled<{ count: number; sides: number }>;
    branchDie: Ruled<number>;
    branchOn: Ruled<number>;
    lobbyFloors: Ruled<number>;
    firstBranchFloor: Ruled<number>;
    longestBranch: Ruled<boolean>;
    split: Ruled<string>;
  };
  lobbyTable: Ruled<TableRow[]>;
  bodyTables: Ruled<{ difficulty: NetDifficulty; rows: TableRow[] }[]>;
  noRepeat: Ruled<NetContentKind[]>;
  bossFloor: Ruled<boolean>;
  sameCostSwap: Ruled<boolean>;
  activeNeedsDemon: Ruled<boolean>;
  demons: Ruled<DemonRule[]>;
  demonAbilities: Ruled<string>;
  floorsPerDemon: Ruled<number>;
  demonsShareFloor: Ruled<boolean>;
  defenses: Ruled<DefenseRule[]>;
  defenseControl: Ruled<string>;
  devices: Ruled<DeviceRule[]>;
  pricing: {
    floors: Ruled<FloorBand[]>;
    features: Ruled<DvCost[]>;
    iceMultiplier: Ruled<number[]>;
    iceSameAsDeck: Ruled<boolean>;
    defenses: Ruled<DvCost[]>;
    devicesFree: Ruled<boolean>;
    availability: Ruled<string>;
  };
  locations: Ruled<LocationRule[]>;
  stealth: {
    watchers: Ruled<string>;
    checks: Ruled<{ label: string; roll: string }[]>;
    placement: Ruled<string>;
    enemyNetrunnerInterface: Ruled<string>;
  };
  generator: {
    difficultyWeights: Ruled<Record<NetDifficulty, number>>;
    respectNodeLimit: Ruled<boolean>;
    nodesPerDemon: Ruled<number>;
    demonWeights: Ruled<Record<NetDifficulty, Record<string, number>>>;
    enemyNetrunnerChance: Ruled<Record<NetDifficulty, number>>;
  };
}

export interface IceInfo {
  id: number;
  slug: string;
  name: string;
  price: number | null;
  priceCategory: PriceCategory | null;
  class: 'AntiPersonnel' | 'AntiProgram' | string | null;
  per: number | null;
  spd: number | null;
  atk: number | null;
  def: number | null;
  rez: number | null;
  effect: string | null;
  book: string | null;
  page: number | null;
}

export interface NetArchitectureRules {
  rules: NetArchitectureRulesDocument;
  ice: IceInfo[];
}

export interface NodeFunction {
  defenseId?: string | null;
  loadout: string[];
  deviceId?: string | null;
  label?: string | null;
  demonId?: string | null;
}

export interface NetContent {
  id: string;
  kind: NetContentKind;
  slug?: string | null;
  dv?: number | null;
  fileIdea?: string | null;
  function?: NodeFunction | null;
}

export interface NetFloor {
  id: string;
  branchId: string;
  depth: number;
  table: string;
  roll: number | null;
  locked: boolean;
  contents: NetContent[];
}

export interface NetBranch {
  id: string;
  fromFloor: number | null;
  floors: NetFloor[];
}

export interface NetDemon {
  id: string;
  type: string;
  floorId: string | null;
}

export interface NetWatcher {
  id: string;
  name: string;
  interface: number;
  pathfinderBonus: number;
  floorId: string | null;
}

export interface ArchitectureState {
  seed: number;
  difficulty: NetDifficulty;
  locationId: string;
  respectNodeLimit: boolean;
  branches: NetBranch[];
  demons: NetDemon[];
  netrunners: NetWatcher[];
  notes: string | null;
}

export interface CostLine {
  group: string;
  label: string;
  quantity: number;
  unit: number;
  total: number;
  source: string | null;
}

export interface ArchitectureQuote {
  lines: CostLine[];
  total: number;
  portable: boolean;
  maxControlNodes: number | null;
  controlNodes: number;
  maxDemons: number;
  warnings: string[];
}

export interface WatcherInfo {
  id: string;
  kind: 'Demon' | 'Netrunner';
  name: string;
  interface: number;
  pathfinderBonus: number;
  floorId: string | null;
}

export interface LocationSummary {
  id: string;
  label: string;
}

export interface NetArchitectureDetail {
  id: number;
  name: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  state: ArchitectureState;
  difficulty: DifficultyRule;
  location: LocationSummary;
  bottomFloorId: string;
  quote: ArchitectureQuote;
  ice: IceInfo[];
  watchers: WatcherInfo[];
}

export interface NetArchitectureSummary {
  id: number;
  name: string;
  difficulty: NetDifficulty;
  floors: number;
  branches: number;
  location: LocationSummary;
  total: number;
  portable: boolean;
  updatedAt: string;
}

export interface GenerateArchitectureRequest {
  name?: string | null;
  difficulty?: NetDifficulty | null;
  floors?: number | null;
  branches?: number | null;
  locationId?: string | null;
  seed?: number | null;
  respectNodeLimit?: boolean | null;
  enemyNetrunner?: boolean | null;
}
