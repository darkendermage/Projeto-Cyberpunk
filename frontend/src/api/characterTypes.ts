// Types of the player-character API (Application/Characters). Derived values always come from the server report.
import type { ArmorSlot, RuleFact } from './types';

export type CreationMethod = 'Streetrat' | 'Edgerunner' | 'CompletePackage';
export type CharacterStatus = 'Creating' | 'Complete' | 'NeedsAttention';
export type CharacterEventKind = 'Ledger' | 'Humanity' | 'Combat' | 'Injury' | 'Progression' | 'Edit';
export type ItemState = 'Carried' | 'Equipped' | 'Stored';
export type ItemOrigin = 'Starting' | 'Purchase' | 'Manual' | 'Loot' | 'Trade' | 'Gift';
export type BudgetPool = 'Balance' | 'CreationGear' | 'CreationFashion';
export type InjuryLocation = 'Body' | 'Head';
export type InjuryStatus = 'Active' | 'QuickFixed' | 'Healed';
export type ModifierTarget = 'Stat' | 'Skill' | 'AllActions';
export type WoundState = 'Unhurt' | 'Lightly' | 'Seriously' | 'Mortally';
export type FireMode = 'Single' | 'Autofire' | 'Suppressive';
export type IssueSeverity = 'Error' | 'Warning' | 'Info';
export type RelationshipKind = 'Friend' | 'Enemy' | 'Ally' | 'Lover' | 'Contact';
export type PaymentStatus = 'Paid' | 'Pending' | 'Overdue' | 'NotApplicable';
export type Caliber = string;

// ---------------------------------------------------------------- sheet (stored source data)

export interface CharacterIdentity {
  name: string;
  handle?: string | null;
  pronouns?: string | null;
  age?: number | null;
  concept?: string | null;
  nativeLanguage?: string | null;
  home: string;
  reputation: number;
}

export interface CharacterRole {
  role: string;
  rank: number;
  ipInvested: number;
  acquiredAt?: string | null;
  notes?: string | null;
}

export interface CampaignOverrides {
  statPoints?: number | null;
  statMin?: number | null;
  statMax?: number | null;
  skillPoints?: number | null;
  skillMax?: number | null;
  gearBudget?: number | null;
  fashionBudget?: number | null;
  note?: string | null;
}

export interface CreationState {
  method: CreationMethod;
  finalized: boolean;
  finalizedAt?: string | null;
  templateRow?: number | null;
  statRolls: Record<string, number>;
  startingChoices: Record<string, number>;
  skillChoices: Record<string, string>;
  overrides: CampaignOverrides;
}

export interface StatModifier {
  id: string;
  target: ModifierTarget;
  key: string;
  value: number;
  source: string;
  active: boolean;
}

export interface WeaponState {
  loadedRounds: number;
  loadedAmmoItemId?: number | null;
  attachments: string[];
}

export interface InventoryEntry {
  instanceId: string;
  itemId: number;
  quantity: number;
  state: ItemState;
  worn?: ArmorSlot | null;
  currentSp?: number | null;
  currentHp?: number | null;
  caliber?: Caliber | null;
  rounds?: number | null;
  weapon?: WeaponState | null;
  /** Cyberware options held by a worn host (Smart Glasses, Battleglove...). */
  wearable?: { options: string[]; side?: 'Left' | 'Right' | null } | null;
  origin: ItemOrigin;
  paidFrom: BudgetPool;
  pricePaid?: number | null;
  notes?: string | null;
}

export interface CharacterCyberware {
  instanceId: string;
  itemId: number;
  parentInstanceId?: string | null;
  side?: 'Left' | 'Right' | null;
  meatLimb: boolean;
  humanityLossApplied: number;
  humanityLossRolled: boolean;
  currentSp?: number | null;
  origin: ItemOrigin;
  paidFrom: BudgetPool;
  pricePaid?: number | null;
  installedAt?: string | null;
}

export interface CombatState {
  currentHp?: number | null;
  deathSavePenalty: number;
  stabilized: boolean;
  luckSpent: number;
  conditions: string[];
}

export interface CriticalInjuryRecord {
  id: string;
  name: string;
  location: InjuryLocation;
  status: InjuryStatus;
  detail?: string | null;
  receivedAt: string;
  combatEventId?: number | null;
  quickFixedAt?: string | null;
  quickFixNote?: string | null;
  treatmentNote?: string | null;
  resolvedAt?: string | null;
}

export interface Relationship {
  id: string;
  kind: RelationshipKind;
  name: string;
  relation?: string | null;
  description?: string | null;
  history?: string | null;
  cause?: string | null;
  wronged?: string | null;
  resources?: string | null;
  intent?: string | null;
  outcome?: string | null;
  notes?: string | null;
}

export interface LifepathState {
  culturalRegion?: string | null;
  answers: Record<string, string>;
  relationships: Relationship[];
  background?: string | null;
}

export interface StyleState {
  clothingStyle?: string | null;
  hairstyle?: string | null;
  affectation?: string | null;
  appearance?: string | null;
  notes?: string | null;
  outfitItems: string[];
}

export interface HousingState {
  housingId?: string | null;
  location?: string | null;
  lifestyleId?: string | null;
  rentOverride?: number | null;
  paidUntil?: string | null;
  notes?: string | null;
}

export interface CharacterSheet {
  identity: CharacterIdentity;
  roles: CharacterRole[];
  creation: CreationState;
  stats: Record<string, number>;
  modifiers: StatModifier[];
  skills: Record<string, number>;
  inventory: InventoryEntry[];
  cyberware: CharacterCyberware[];
  combat: CombatState;
  injuries: CriticalInjuryRecord[];
  humanity: { lost: number; recovered: number };
  lifepath: LifepathState;
  roleLifepath: Record<string, Record<string, string>>;
  style: StyleState;
  housing: HousingState;
  wallet: { balance: number };
  progress: { ip: number; ipEarned: number };
  roleData: Record<string, unknown>;
  notes?: string | null;
}

// ---------------------------------------------------------------- report (derived on the server)

export interface ValuePart { label: string; value: number }
export interface StatLine { key: string; base: number; effective: number; parts: ValuePart[] }

export interface HealthReport {
  maxHp: number;
  currentHp: number;
  seriouslyWoundedThreshold: number;
  woundState: WoundState;
  woundActionPenalty: number;
  woundMovePenalty: number;
  stabilizationDv: number | null;
  bodyForDerived: number;
  bodySource: string | null;
  deathSave: number;
  baseDeathSavePenalty: number;
  deathSavePenalty: number;
  mustRollDeathSave: boolean;
}

export interface HumanityReport {
  empBase: number;
  max: number;
  current: number;
  lost: number;
  recovered: number;
  therapyCap: number;
  piecesLoweringCap: number;
  borgwareLoweringCap: number;
  empEffective: number;
  cyberpsychosis: boolean;
}

export interface ArmorLocationReport {
  location: ArmorSlot;
  sp: number;
  maxSp: number | null;
  instanceId: string | null;
  name: string | null;
  fromCyberware: boolean;
}

export interface ArmorReport {
  head: ArmorLocationReport;
  body: ArmorLocationReport;
  penalty: { ref: number; dex: number; move: number };
  penaltySource: string | null;
}

export interface CharacterSkillLine {
  key: string;
  skillId: string;
  name: string;
  namePt: string | null;
  specialization: string | null;
  stat: string;
  category: string;
  level: number;
  statValue: number;
  modifier: number;
  base: number;
  actionPenalty: number;
  total: number;
  doubleCost: boolean;
  combat: boolean;
  basic: boolean;
  roleSkill: boolean;
  page: number;
  ipToRaise: number | null;
  /** What makes up the modifier: temporary modifiers and cyberware bonuses. */
  modifierParts: ValuePart[];
  /** Cyberware bonuses that only apply in a situation (not in the base). */
  situational: string[];
}

/** What an installed piece of cyberware does to the sheet. */
export interface CyberEffectLine { item: string; description: string; source: string }

export interface RoleLine { role: string; rank: number; ability: string; abilityPage: number; current: boolean; ipInvested: number; ipToRaise: number | null }

export interface ActiveInjuryLine { id: string; name: string; location: InjuryLocation; status: InjuryStatus; effect: string | null; raisesDeathSave: boolean }

export interface InventoryLine {
  instanceId: string;
  itemId: number;
  name: string;
  slug: string;
  category: string;
  quantity: number;
  state: ItemState;
  worn: ArmorSlot | null;
  caliber: Caliber | null;
  rounds: number | null;
  sp: number | null;
  currentSp: number | null;
  shieldHp: number | null;
  shieldHpMax: number | null;
  unitPrice: number | null;
  pricePaid: number | null;
  origin: ItemOrigin;
  paidFrom: BudgetPool;
  fashion: boolean;
  notes: string | null;
}

export interface WeaponLine {
  instanceId: string;
  itemId: number;
  name: string;
  class: string;
  skillId: string | null;
  skillName: string | null;
  skillTotal: number;
  damage: string | null;
  rof: number | null;
  magazine: number | null;
  loadedRounds: number;
  loadedAmmoItemId: number | null;
  loadedAmmo: string | null;
  autofireMultiplier: number | null;
  autofireTotal: number | null;
  calibers: Caliber[];
  exotic: boolean;
  attachmentSlots: number;
  attachments: string[];
  equipped: boolean;
}

export interface CyberLine {
  instanceId: string;
  itemId: number;
  name: string;
  slug: string;
  type: string | null;
  location: string | null;
  parentInstanceId: string | null;
  side: 'Left' | 'Right' | null;
  meatLimb: boolean;
  foundational: boolean;
  slotsProvided: number | null;
  slotsUsed: number;
  humanityLossApplied: number;
  humanityLossDice: string | null;
  currentSp: number | null;
  region: string;
  price: number;
  requirements: string | null;
  /** A glove worn over this arm makes the option inaccessible. */
  coveredBy?: string | null;
}

export type WearableAccepts = 'Cybereye' | 'Cyberarm' | 'Cyberaudio';

/** A worn item with Option Slots (Smart Glasses, Smart Lens, Battleglove, Smart Gloves, Smart Ears). */
export interface WearableLine {
  instanceId: string;
  itemId: number;
  name: string;
  accepts: WearableAccepts;
  slots: number;
  slotsUsed: number;
  cyberfingers: number;
  cyberfingersUsed: number;
  worn: boolean;
  side: 'Left' | 'Right' | null;
  coversArm: boolean;
  options: { instanceId: string; itemId: number; name: string; slots: number; finger: boolean }[];
  builtIn: string[];
  covered: string[];
  requirement: string | null;
  warnings: string[];
  source: string;
}

export interface CyberRegionReport {
  key: string;
  label: string;
  side: 'Left' | 'Right' | null;
  foundationInstanceId: string | null;
  foundationName: string | null;
  slotsTotal: number | null;
  slotsUsed: number;
  meatLimb: boolean;
  pieces: string[];
  source: string | null;
}

export interface PreviewPiece {
  instanceId: string;
  name: string;
  slug: string;
  price: number;
  humanityLoss: number;
  humanityLossDice: string | null;
  host: string | null;
  side: 'Left' | 'Right' | null;
  meatLimb: boolean;
  region: string;
}

export interface InstallPreview {
  canInstall: boolean;
  error: string | null;
  pieces: PreviewPiece[];
  price: number;
  pool: BudgetPool;
  available: number;
  availableAfter: number;
  humanityNow: number;
  humanityLossMin: number;
  humanityLossMax: number;
  humanityLossRolled: boolean;
  empNow: number;
  empAfterWorst: number;
  therapyCapAfter: number;
  textRequirements: string[];
  warnings: string[];
}

export interface InstallRequestBody {
  itemId?: number | null;
  fromInventory?: string | null;
  meatLimb?: boolean;
  hostInstanceId?: string | null;
  acknowledge?: boolean;
  pool?: BudgetPool | null;
  replacing?: string | null;
  side?: 'Left' | 'Right' | null;
}

export interface HousingReport {
  housingId: string | null;
  housing: string | null;
  lifestyleId: string | null;
  lifestyle: string | null;
  rent: number;
  rentFromTable: boolean;
  lifestyleCost: number;
  monthlyTotal: number;
  paidUntil: string | null;
  daysOverdue: number;
  status: PaymentStatus;
  source: string;
}

export interface CharacterReport {
  stats: StatLine[];
  health: HealthReport;
  humanity: HumanityReport;
  armor: ArmorReport;
  skills: CharacterSkillLine[];
  roles: RoleLine[];
  activeInjuries: ActiveInjuryLine[];
  actionPenalties: ValuePart[];
  actionPenalty: number;
  luck: number;
  luckAvailable: number;
  inventory: InventoryLine[];
  weapons: WeaponLine[];
  cyberware: CyberLine[];
  housing: HousingReport;
  regions: CyberRegionReport[];
  fullBodyConversion: boolean;
  cyberwareEffects: CyberEffectLine[];
  wearables: WearableLine[];
}

export interface CharacterIssue {
  code: string;
  severity: IssueSeverity;
  message: string;
  suggestion: string | null;
  source: string | null;
  field: string | null;
}

export interface CreationBudget { used: number; total: number; source: string; campaignVariant: boolean; remaining: number; exceeded: boolean }

export interface CharacterValidationReport {
  status: CharacterStatus;
  canFinalize: boolean;
  stats: CreationBudget | null;
  skills: CreationBudget | null;
  gear: CreationBudget | null;
  fashion: CreationBudget | null;
  issues: CharacterIssue[];
  errorCount: number;
  warningCount: number;
}

export interface CharacterDetail {
  id: number;
  version: number;
  status: CharacterStatus;
  hasPortrait: boolean;
  createdAt: string;
  updatedAt: string;
  sheet: CharacterSheet;
  report: CharacterReport;
  validation: CharacterValidationReport;
  notices: string[];
}

export interface CharacterSummary {
  id: number;
  name: string;
  handle: string | null;
  role: string | null;
  roleRank: number | null;
  method: CreationMethod;
  status: CharacterStatus;
  errorCount: number;
  warningCount: number;
  maxHp: number;
  currentHp: number;
  humanity: number;
  humanityMax: number;
  balance: number;
  hasPortrait: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CharacterEvent {
  id: number;
  kind: CharacterEventKind;
  action: string;
  amount: number | null;
  before: string | null;
  after: string | null;
  description: string;
  isManual: boolean;
  createdAt: string;
}

// ---------------------------------------------------------------- rules

export interface StartingItem { slug: string; itemId: number | null; name: string; qty: number; caliber: Caliber | null; rounds: number | null; worn: ArmorSlot | null; meatArm: boolean }
export interface StartingOption { index: number; label: string; items: StartingItem[] }
export interface StartingGroup { index: number; options: StartingOption[] }
export interface StartingSection { key: string; label: string; source: string; humanityLoss: number | null; groups: StartingGroup[] }
export interface LifepathQuestion { id: string; label: string; labelPt: string; dice: string; options: string[]; showIfQuestion: string | null; showIfValue: string | null }

export interface RoleRules {
  id: string;
  ability: string;
  abilityPage: number;
  statTable: number[][];
  statTableSource: string;
  skills: Record<string, number>;
  skillsSource: string;
  choiceSkills: string[];
  starting: StartingSection[];
  lifepath: LifepathQuestion[];
  lifepathSource: string;
  abilityInfo: RoleAbilityInfo;
}

/** Role Ability explained for the player, summarized from the book pages in `source`. */
export interface RoleAbilityInfo { name: string; source: string; summary: string; points: string[] }

export interface CreationMethodRules {
  method: CreationMethod;
  label: string;
  summary: string;
  source: string;
  statPoints: number | null;
  statMin: number | null;
  statMax: number | null;
  skillPoints: number | null;
  skillMin: number | null;
  skillMax: number | null;
  gearBudget: number | null;
  fashionBudget: number | null;
  startingCash: number | null;
  statPresets: { label: string; points: number }[];
}

export interface SkillRule {
  id: string;
  name: string;
  namePt: string | null;
  stat: string;
  category: string;
  combat: boolean;
  doubleCost: boolean;
  specialized: boolean;
  page: number;
}

export interface CriticalInjuryRule {
  location: InjuryLocation;
  roll: number;
  name: string;
  effect: string;
  quickFix: string | null;
  treatment: string;
  deathSave: boolean;
  quickFixHeals: boolean;
}

export interface CharacterRules {
  about: string;
  books: Record<string, string>;
  statOrder: string[];
  methods: CreationMethodRules[];
  roles: RoleRules[];
  skills: SkillRule[];
  basicSkills: string[];
  originLanguageLevel: number;
  culturalOrigins: { region: string; languages: string[] }[];
  homes: string[];
  lifepathTables: { id: string; label: string; labelPt: string; dice: string; page: number; options: string[] }[];
  relations: {
    friendRelationship: string[]; enemyWho: string[]; enemyCause: string[]; enemyResources: string[];
    enemyRevenge: string[]; loveOutcome: string[]; source: string;
  };
  criticalInjuries: CriticalInjuryRule[];
  lifestyles: { id: string; label: string; value: number }[];
  housing: { id: string; label: string; rent: number | null; buy: number | null; corporate: boolean }[];
  facts: RuleFact[];
  cyberware: {
    meatArmOptions: string[];
    meatLegOptions: string[];
    anyLimbOptions: string[];
    coverings: string[];
    therapies: { id: string; label: string; cost: number; recovery: string; dv: number; materials: number; source: string }[];
    therapyPayments: string[];
    therapySource: string;
  };
}

export interface StoreItem {
  id: number;
  slug: string;
  name: string;
  category: string;
  price: number | null;
  source: string | null;
  page: number | null;
  fashion: boolean;
  cyberware: boolean;
  calibers: Caliber[];
  perPack: number | null;
  summary: string | null;
  cyberType: string | null;
  bodyLocation: string | null;
  humanityLoss: number | null;
  humanityLossDice: string | null;
  foundational: boolean;
  requirements: string | null;
  /** Catalog description (book text or summary), shown when the item is opened. */
  description: string | null;
}

/** One page of the store. */
export interface StorePage { items: StoreItem[]; total: number; page: number; pageSize: number }

export interface CreateCharacterRequest {
  name: string;
  handle?: string | null;
  method: CreationMethod;
  role: string;
  nativeLanguage?: string | null;
  culturalRegion?: string | null;
  home?: string | null;
  overrides?: CampaignOverrides | null;
}
