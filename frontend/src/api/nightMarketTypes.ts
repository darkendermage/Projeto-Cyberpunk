/** Types of /api/night-markets (NightMarkets/NightMarketContracts.cs). */

export type PriceCategory = 'Cheap' | 'Everyday' | 'Costly' | 'Premium' | 'Expensive' | 'VeryExpensive' | 'Luxury' | 'SuperLuxury';
export type WeaponQuality = 'Poor' | 'Standard' | 'Excellent';
export type ItemCondition = 'Normal' | 'Damaged' | 'Destroyed' | 'BeyondRepair';
export type ConditionDisclosure = 'Disclosed' | 'Silent' | 'Disguised';
export type MarketKind = 'NightMarket' | 'MidnightMarket' | 'Shop' | 'Armory';
export type GoodsMode = 'Roll' | 'Suggested' | 'Pick' | 'None';
export type MarketFeature = 'DoorPrize' | 'SecondHand' | 'Midnight' | 'MysteryBoxes';
export type NightMarketStatus = 'Open' | 'Closed';
export type ListingOrigin =
  | 'CoreTable' | 'Foundational' | 'BlackChrome' | 'Preset' | 'Related' | 'GmChoice' | 'Midnight' | 'Manual' | 'Index' | 'Admission';

/** A value of the rules file: its page or the complementary flag. */
export interface Ruled<T> {
  value: T;
  source?: string | null;
  complementary: boolean;
  note?: string | null;
  reference: string;
}

export interface ItemQuery {
  category?: string | null;
  types?: string[] | null;
  styles?: string[] | null;
  price?: { eq?: number | null; min?: number | null; max?: number | null } | null;
  exotic?: boolean | null;
  foundational?: boolean | null;
}

export interface TableEntry {
  label: string;
  slugs?: string[] | null;
  local?: { name: string; price: number } | null;
  query?: ItemQuery | null;
  gmChoice: boolean;
  note?: string | null;
}

export interface GoodsType {
  id: string;
  roll: number;
  name: string;
  description: string;
  table: TableEntry[];
}

export interface HaggleDeal {
  id: string;
  minRank: number;
  kind: 'Discount' | 'ExtraItem' | 'SplitPayment' | 'JobPay';
  percent?: number | null;
  label: string;
  text: string;
}

export interface PrizeEntry {
  roll?: number | null;
  label: string;
  slug?: string | null;
  quantity: number;
  weight: number;
}

export interface NightMarketRulesDocument {
  priceLadder: Ruled<{ category: PriceCategory; price: number }[]>;
  withoutFixer: Ruled<PriceCategory>;
  generator: {
    goodsRolls: Ruled<number>;
    linesPerType: Ruled<string>;
    bandSize: Ruled<number>;
    allQualities: Ruled<boolean>;
    notTotalInventory: Ruled<string>;
    foundational: Ruled<boolean>;
    cyberwareInstall: Ruled<string>;
    gmChoiceDraw: Ruled<boolean>;
    goods: Ruled<GoodsType[]>;
    relatedBooks: Ruled<boolean>;
  };
  operator: {
    reach: Ruled<{ minRank: number; maxRank: number; category: PriceCategory }[]>;
    nightMarketRank: Ruled<number>;
    nightMarketReach: Ruled<PriceCategory>;
    midnightMarketRank: Ruled<number>;
    haggleFormula: Ruled<string>;
    haggleTie: Ruled<string>;
    criticals: Ruled<string>;
    dealsPerTransaction: Ruled<number>;
    deals: Ruled<HaggleDeal[]>;
    rounding: Ruled<string>;
  };
  condition: {
    damagedSteps: Ruled<number>;
    destroyedSteps: Ruled<number>;
    detectRank: Ruled<number>;
    lowValueDamaged: Ruled<number>;
    beyondRepair: Ruled<number>;
    sellingIsGmCall: Ruled<string>;
    floor: Ruled<PriceCategory>;
    categoryPrice: Ruled<string>;
  };
  midnightMarket: { items: Ruled<string>; drawPool: Ruled<{ minCategory: PriceCategory; orExotic: boolean }> };
  mysteryBoxes: {
    redemption: Ruled<string>;
    pricePattern: Ruled<number[]>;
    boxes: Ruled<{ roll: number; name: string; contents: PrizeEntry[] }[]>;
  };
  doorPrize: {
    arrival: Ruled<number>;
    bonus: Ruled<PrizeEntry[]>;
    example: Ruled<PrizeEntry>;
    slices: Ruled<PrizeEntry[]>;
  };
  fixerAvailability: Ruled<string>;
  blackChrome: { booths: Ruled<string>; listingFee: Ruled<string>; buyNow: Ruled<string> };
}

export interface PresetOrganizer {
  name: string;
  role: string;
  operatorRank?: number | null;
  trading?: number | null;
}

export interface MarketPreset {
  id: string;
  name: string;
  kind: MarketKind;
  letter: string;
  organizer: PresetOrganizer;
  summary?: string | null;
  frequency?: string | null;
  location?: string | null;
  sells?: string | null;
  invite?: string | null;
  rules?: string | null;
  goods: GoodsMode;
  suggestedGoods: string[];
  features: MarketFeature[];
  admission?: { label: string; price: number; source: string } | null;
  forSale: boolean;
  source: string;
}

export interface NightMarketRules {
  rules: NightMarketRulesDocument;
  presets: MarketPreset[];
  books: { name: string; items: number }[];
  indexSections: { id: string; label: string; rows: number }[];
}

export interface MarketOrganizer {
  name: string;
  role?: string | null;
  operatorRank?: number | null;
  trading?: number | null;
}

export interface MarketOptions {
  damagedPercent: number;
  scam: boolean;
  includeRelated: boolean;
  blackChromeLetter?: string | null;
}

export interface GenerateMarketRequest {
  name?: string;
  presetId: string;
  seed?: number | null;
  books?: string[] | null;
  goods?: string[] | null;
  rollGoods?: boolean;
  lines?: Record<string, number> | null;
  organizer?: MarketOrganizer | null;
  options?: MarketOptions | null;
  midnight?: boolean;
}

export interface RolledGoods {
  id: string;
  name: string;
  roll?: number | null;
  lines: number;
  rolls: { roll: number; band: number; label: string }[];
}

export interface Stall {
  code: string;
  number: number;
  label: string;
  listings: number;
}

export interface Listing {
  id: number;
  stall: string;
  itemId?: number | null;
  slug?: string | null;
  name: string;
  categoryKey?: string | null;
  manufacturer?: string | null;
  description?: string | null;
  book?: string | null;
  page?: number | null;
  basePrice: number;
  baseCategory: PriceCategory;
  price: number;
  priceCategory: PriceCategory;
  value: number;
  valueCategory: PriceCategory;
  quality?: WeaponQuality | null;
  condition: ItemCondition;
  conditionHidden: boolean;
  stock?: number | null;
  forSale: boolean;
  origin: ListingOrigin;
  group?: string | null;
  goodsId?: string | null;
  source?: string | null;
  note?: string | null;
  warnings: string[];
}

export interface OpenSlot {
  id: number;
  stall: string;
  label: string;
  origin: ListingOrigin;
  goodsId?: string | null;
}

export interface MarketSale {
  id: number;
  at: string;
  listingId?: number | null;
  name: string;
  buyer?: string | null;
  quantity: number;
  delivered: number;
  unitPrice: number;
  total: number;
  paidNow: number;
  dueLater: number;
  dealId?: string | null;
  haggle?: string | null;
  kind: 'sale' | 'prize' | 'box';
  undone: boolean;
}

export interface MarketEvent {
  at: string;
  kind: string;
  title: string;
  detail?: string | null;
}

export interface NightMarketDetail {
  id: number;
  name: string;
  version: number;
  status: NightMarketStatus;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
  preset: MarketPreset;
  organizer: MarketOrganizer;
  organizerReach?: PriceCategory | null;
  seed: number;
  books: string[];
  options: MarketOptions;
  goods: RolledGoods[];
  stalls: Stall[];
  listings: Listing[];
  openSlots: OpenSlot[];
  sales: MarketSale[];
  events: MarketEvent[];
  visitors: number;
  notices: string[];
}

export interface NightMarketSummary {
  id: number;
  name: string;
  presetId: string;
  presetName: string;
  letter: string;
  organizer: string;
  operatorRank?: number | null;
  status: NightMarketStatus;
  listings: number;
  sales: number;
  revenue: number;
  updatedAt: string;
}

export interface HaggleSide {
  trading: number;
  rank: number;
}

export interface HaggleRequest {
  buyer: HaggleSide;
  seller: HaggleSide;
  buyerRolls?: number[] | null;
  sellerRolls?: number[] | null;
}

export interface DealOption {
  id: string;
  label: string;
  text: string;
  minRank: number;
  applies: boolean;
  reason?: string | null;
}

export interface HaggleResult {
  buyerTotal: number;
  sellerTotal: number;
  buyerRolls: number[];
  sellerRolls: number[];
  success: boolean;
  summary: string;
  deals: DealOption[];
}

export interface PrizeResult {
  roll?: number | null;
  label: string;
  quantity: number;
  itemId?: number | null;
  itemName?: string | null;
  extraItemId?: number | null;
  extraItemName?: string | null;
  loaded: boolean;
  quality?: WeaponQuality | null;
}

export interface MysteryBoxResult {
  boxRoll: number;
  box: string;
  contentRoll: number;
  prize: PrizeResult;
  source: string;
}

export interface DoorPrizeResult {
  roll: number;
  of: number;
  prize: PrizeResult;
  bonus: PrizeResult[];
}

export interface MarketEventResult<T> {
  market: NightMarketDetail;
  result: T;
}

export interface SellQuoteRequest {
  itemId?: number | null;
  price?: number | null;
  condition: ItemCondition;
  disclosure: ConditionDisclosure;
  fixerRank?: number | null;
  haggle?: HaggleRequest | null;
  dealId?: string | null;
}

export interface SellQuote {
  name?: string | null;
  marketPrice: number;
  marketCategory: PriceCategory;
  offer: number;
  offerCategory: PriceCategory;
  fixerNoticesDamage: boolean;
  notes: string[];
  haggle?: HaggleResult | null;
}

export interface IndexSource {
  book: string;
  page?: number | null;
}

export interface IndexRow {
  row: number;
  section: string;
  group?: string | null;
  name: string;
  cost: string;
  priceEb?: number | null;
  priceCategory?: PriceCategory | null;
  sources: IndexSource[];
  itemId?: number | null;
  slug?: string | null;
}

export interface IndexPage {
  total: number;
  page: number;
  pageSize: number;
  rows: IndexRow[];
}

export interface IndexQuery {
  q?: string;
  section?: string | null;
  category?: PriceCategory | null;
  catalogued?: boolean | null;
  page: number;
  pageSize: number;
}
