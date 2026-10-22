// Mirrors backend/src/CyberpunkDb.Application/Catalog/Contracts and Collections contracts.

export type FieldKind = 'number' | 'enum' | 'boolean' | 'text' | 'longtext' | 'dice' | 'set';

export type FilterOp =
  | 'eq' | 'neq' | 'gt' | 'gte' | 'lt' | 'lte' | 'between'
  | 'in' | 'nin' | 'all' | 'contains' | 'isnull' | 'notnull';

export type AttributeValue = string | number | boolean | string[] | null;

export interface Option {
  value: string;
  label: string;
}

export interface FieldDescriptor {
  key: string;
  label: string;
  kind: FieldKind;
  unit?: string | null;
  group?: string | null;
  description?: string | null;
  filterable: boolean;
  sortable: boolean;
  facet: boolean;
  editable: boolean;
  required: boolean;
  base: boolean;
  defaultOp?: FilterOp | null;
  operators: FilterOp[];
  appliesTo?: string[] | null;
  options?: Option[] | null;
  min?: number | null;
  max?: number | null;
  /** Distinct values of small numeric domains, used by the min/max pickers. */
  values?: number[] | null;
}

export interface SortOption {
  key: string;
  label: string;
}

export interface CategorySchema {
  key: string;
  category: string;
  label: string;
  sublabel?: string | null;
  description: string;
  icon: string;
  subtypeField?: string | null;
  cardFields: string[];
  tableFields: string[];
  filterOrder: string[];
  sortOptions: SortOption[];
  defaultSort: string;
  fields: FieldDescriptor[];
  itemCount: number;
}

export interface CategorySummary {
  key: string;
  category: string;
  label: string;
  sublabel?: string | null;
  icon: string;
  itemCount: number;
}

export interface NamedRef {
  slug: string;
  name: string;
}

export type DataOrigin = 'Compendium' | 'Demo' | 'Custom' | 'Verified';

export interface Item {
  id: number;
  slug: string;
  name: string;
  category: string;
  subtype?: Option | null;
  description?: string | null;
  notes?: string | null;
  price?: number | null;
  priceNote?: string | null;
  priceCategory?: string | null;
  manufacturer?: NamedRef | null;
  source?: NamedRef | null;
  page?: number | null;
  imageUrl?: string | null;
  ruleSystem: string;
  dataOrigin: DataOrigin;
  tags: string[];
  attributes: Record<string, AttributeValue>;
  extra?: Record<string, unknown> | null;
}

export interface ItemSummary {
  id: number;
  slug: string;
  name: string;
  category: string;
  price?: number | null;
  subtype?: string | null;
}

export interface RelationGroup {
  key: string;
  label: string;
  items: ItemSummary[];
}

export interface ItemDetail {
  item: Item;
  relations: RelationGroup[];
}

export interface FacetValue {
  value: string;
  label: string;
  count: number;
}

export interface FilterCriterionDto {
  field: string;
  op: FilterOp;
  value?: string | number | boolean | null;
  values?: (string | number)[];
  min?: number | null;
  max?: number | null;
}

export interface SearchRequest {
  q?: string;
  filters: FilterCriterionDto[];
  sort?: string;
  dir?: 'asc' | 'desc';
  page: number;
  pageSize: number;
  includeFacets?: boolean;
}

export interface SearchResult {
  items: Item[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  facets: Record<string, FacetValue[]>;
}

export interface GlobalSearchGroup {
  category: string;
  label: string;
  total: number;
  items: Item[];
}

export interface GearListSummary {
  id: number;
  name: string;
  description?: string | null;
  entryCount: number;
  totalQuantity: number;
  totalPrice: number;
  updatedAt: string;
}

export interface GearListEntry {
  id: number;
  quantity: number;
  note?: string | null;
  addedAt: string;
  item: Item;
}

export interface GearListDetail {
  id: number;
  name: string;
  description?: string | null;
  createdAt: string;
  updatedAt: string;
  entries: GearListEntry[];
  totalPrice: number;
  unpricedEntries: number;
}

export interface ItemUpsert {
  slug?: string | null;
  name: string;
  category: string;
  description?: string | null;
  notes?: string | null;
  price?: number | null;
  priceNote?: string | null;
  priceCategory?: string | null;
  manufacturer?: string | null;
  source?: string | null;
  page?: number | null;
  imageUrl?: string | null;
  ruleSystem?: string | null;
  dataOrigin?: string | null;
  tags?: string[];
  attributes?: Record<string, AttributeValue>;
}

export interface ImportResult {
  created: number;
  updated: number;
  failed: number;
  errors: { index: number; slug?: string | null; name?: string | null; errors: Record<string, string[]> }[];
}

/** GET /api/app/update — newer GitHub release for the desktop executable. */
export interface UpdateInfo {
  current: string;
  latest: string | null;
  available: boolean;
  notes: string | null;
  releaseUrl: string | null;
  canApply: boolean;
}

export interface HealthInfo {
  status: string;
  version: string;
}

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

// ---------------------------------------------------------------- enemy generator (Application/Npcs)

export type NpcLevel = 'Mook' | 'Lieutenant' | 'MiniBoss' | 'Boss';
export type NpcStatus = 'Draft' | 'Valid' | 'ValidWithExceptions';
export type WeaponQuality = 'Poor' | 'Standard' | 'Excellent';
export type ArmorSlot = 'Body' | 'Head';
export type CyberwareRuleState = 'Allowed' | 'Required' | 'Forbidden';
export type NpcIssueSeverity = 'Error' | 'Warning' | 'Info';

export interface NpcGearEntry {
  itemId: number;
  quantity: number;
  quality?: WeaponQuality | null;
  worn?: ArmorSlot | null;
  locked?: boolean;
}

export interface NpcCyberwareEntry {
  instanceId: string;
  itemId: number;
  parentInstanceId?: string | null;
  locked?: boolean;
}

export interface NpcCyberwareRule {
  itemId: number;
  state: CyberwareRuleState;
}

export interface NpcRuleException {
  key: string;
  justification: string;
}

export interface NpcLocks {
  stats: string[];
  skills: string[];
  identity: boolean;
}

export interface NpcSheet {
  name: string;
  concept?: string | null;
  level: NpcLevel;
  hardened: boolean;
  profileId?: string | null;
  role?: string | null;
  roleRank: number;
  nativeLanguage: string;
  home: string;
  stats: Record<string, number>;
  luck?: number | null;
  skills: Record<string, number>;
  keySkills: string[];
  gear: NpcGearEntry[];
  cyberware: NpcCyberwareEntry[];
  cyberwareRules: NpcCyberwareRule[];
  exceptions: NpcRuleException[];
  locks: NpcLocks;
  notes?: string | null;
}

export interface NpcIssue {
  code: string;
  key: string;
  severity: NpcIssueSeverity;
  message: string;
  suggestion?: string | null;
  source?: string | null;
  overridable: boolean;
  field?: string | null;
  overridden: boolean;
  justification?: string | null;
}

export interface BudgetLine {
  used: number;
  total: number | null;
  remaining: number | null;
  exceeded: boolean;
  source: string;
}

export interface NpcDerived {
  hp: number;
  seriouslyWounded: number;
  deathSave: number;
  bodyForDerived: number;
  strengthBody: number;
  spBody: number;
  spHead: number;
  armorPenalty: { ref: number; dex: number; move: number };
  effectiveStats: Record<string, number>;
  cyberwarePieces: number;
  suggestedEmp: number | null;
}

export interface SkillLine {
  key: string;
  skillId: string;
  name: string;
  specialization: string | null;
  stat: string;
  category: string;
  level: number;
  freeLevel: number;
  cost: number;
  base: number;
  doubleCost: boolean;
  combat: boolean;
  isKey: boolean;
}

export interface GearLine {
  index: number;
  itemId: number;
  name: string;
  slug: string;
  category: string;
  quantity: number;
  quality: WeaponQuality | null;
  unitPrice: number | null;
  total: number;
  worn: ArmorSlot | null;
  summary: string | null;
  locked: boolean;
}

export interface CyberLine {
  instanceId: string;
  itemId: number;
  name: string;
  slug: string;
  category: string;
  parentInstanceId: string | null;
  price: number | null;
  slotsUsed: number;
  slotsAssumed: boolean;
  slotsProvided: number | null;
  slotsFree: number | null;
  pool: string | null;
  foundational: boolean;
  requiresItemId: number | null;
  requiresPair: boolean;
  freeTextRequirement: string | null;
  locked: boolean;
}

export interface TemplateSummary {
  level: NpcLevel;
  hardened: boolean;
  name: string;
  statPoints: number;
  maxStat: number;
  skillPoints: number;
  maxSkill: number;
  suggestedHp: string;
  suggestedSp: string;
  combatSkillBase: string;
  nonCombatSkillBase: string;
  gearBudget: number;
  cyberwareBudget: number | null;
  weaponQuality: string;
  roleRank: number;
  rolePresence: string;
}

export interface NpcValidationReport {
  isValid: boolean;
  status: NpcStatus;
  template: TemplateSummary;
  stats: BudgetLine;
  skills: BudgetLine;
  gear: BudgetLine;
  cyberware: BudgetLine;
  derived: NpcDerived;
  skillLines: SkillLine[];
  gearLines: GearLine[];
  cyberLines: CyberLine[];
  issues: NpcIssue[];
  errorCount: number;
  warningCount: number;
}

export interface NpcGenerationRequest {
  level: NpcLevel;
  hardened: boolean;
  profileId?: string | null;
  seed?: number | null;
  base?: NpcSheet | null;
  cyberwareRules: NpcCyberwareRule[];
  applyEmpGuideline: boolean;
}

export interface GenerationConflict {
  code: string;
  message: string;
  suggestion?: string | null;
  source?: string | null;
}

export interface NpcGenerationResult {
  success: boolean;
  sheet: NpcSheet | null;
  report: NpcValidationReport | null;
  seed: number;
  attempts: number;
  profileId: string | null;
  conflicts: GenerationConflict[];
}

export interface NpcSummary {
  id: number;
  name: string;
  concept?: string | null;
  level: NpcLevel;
  hardened: boolean;
  profileId?: string | null;
  status: NpcStatus;
  createdAt: string;
  updatedAt: string;
}

export interface NpcDetail {
  id: number;
  status: NpcStatus;
  createdAt: string;
  updatedAt: string;
  sheet: NpcSheet;
  report: NpcValidationReport;
}

export interface RuleFact {
  key: string;
  label: string;
  value: string;
  source: string | null;
  complementary: boolean;
  note: string | null;
}

export interface NpcTemplateInfo {
  level: NpcLevel;
  hardened: boolean;
  name: string;
  summary: string;
  values: TemplateSummary;
  facts: RuleFact[];
}

export interface SkillInfo {
  id: string;
  name: string;
  stat: string;
  category: string;
  combat: boolean;
  doubleCost: boolean;
  specialized: boolean;
  page: number;
}

export interface ProfileInfo {
  id: string;
  label: string;
  description: string;
  role: string | null;
  keyCombatSkills: string[];
  keyNonCombatSkills: string[];
}

export interface NpcRules {
  books: Record<string, string>;
  about: string;
  statKeys: string[];
  templates: NpcTemplateInfo[];
  skills: SkillInfo[];
  freeSkills: { skill: string; specialization: string | null; level: number }[];
  freeSkillsSource: string;
  profiles: ProfileInfo[];
  roles: string[];
  languages: string[];
  homes: string[];
  generalRules: RuleFact[];
}
