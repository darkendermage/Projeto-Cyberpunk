import type {
  HaggleRequest, ItemCondition, Listing, ListingOrigin, MarketFeature, MarketKind, NightMarketDetail, PrizeEntry, Stall, WeaponQuality,
} from '../../api/nightMarketTypes';

export const KIND_LABEL: Record<MarketKind, string> = {
  NightMarket: 'Night Market',
  MidnightMarket: 'Midnight Market',
  Shop: 'Loja',
  Armory: 'Arsenal',
};

export const FEATURE_LABEL: Record<MarketFeature, string> = {
  DoorPrize: 'Door Prize',
  SecondHand: 'Segunda mão',
  Midnight: 'Midnight Market',
  MysteryBoxes: 'Mystery Boxes',
};

export const CONDITION_LABEL: Record<ItemCondition, string> = {
  Normal: 'Normal',
  Damaged: 'Damaged',
  Destroyed: 'Destroyed',
  BeyondRepair: 'Destroyed Beyond Repair',
};

export const ORIGIN_LABEL: Record<ListingOrigin, string> = {
  CoreTable: 'Tabela do Core',
  Foundational: 'Peça-base',
  BlackChrome: 'Black Chrome',
  Preset: 'Estoque do mercado',
  Related: 'Outros livros',
  GmChoice: 'Escolha do Mestre',
  Midnight: 'Midnight Market',
  Manual: 'Adicionado pelo Mestre',
  Index: 'Night Market Index',
  Admission: 'Entrada',
};

/** Tabs of the CBK app, as on the Black Chrome pages, by catalog category. */
export const CBK_CATEGORIES: { key: string; label: string; categories: string[] }[] = [
  { key: 'weapons', label: 'weapons', categories: ['weapons', 'ammunition', 'weapon-attachments'] },
  { key: 'cyberware', label: 'cyberware', categories: ['cyberware'] },
  { key: 'fashion', label: 'fashion & armor', categories: ['armor'] },
  { key: 'gear', label: 'general gear', categories: ['gear', 'drugs'] },
  { key: 'net', label: 'net stuff', categories: ['netrunning'] },
  { key: 'vehicles', label: 'vehicles', categories: ['vehicles', 'vehicle-upgrades'] },
  { key: 'other', label: 'other categories', categories: [] },
];

export function cbkCategoryOf(listing: Pick<Listing, 'categoryKey'>): string {
  const key = listing.categoryKey ?? '';
  return CBK_CATEGORIES.find((c) => c.categories.includes(key))?.key ?? 'other';
}

export function qualityLabel(quality: WeaponQuality | null | undefined): string | null {
  if (!quality || quality === 'Standard') return null;
  return quality === 'Poor' ? 'Poor Quality' : 'Excellent Quality';
}

export interface ListingFilter {
  stall: string | null;
  category: string | null;
  q: string;
  /** Players see a hidden condition as "Normal"; the GM view shows it. */
  gm: boolean;
  onlyForSale: boolean;
}

const normalize = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

export function filterListings(listings: Listing[], f: ListingFilter): Listing[] {
  const q = normalize(f.q.trim());
  return listings.filter((l) =>
    (f.stall === null || l.stall === f.stall)
    && (f.category === null || cbkCategoryOf(l) === f.category)
    && (!f.onlyForSale || (l.forSale && (l.stock == null || l.stock > 0)))
    && (q === '' || normalize(`${l.name} ${l.manufacturer ?? ''} ${l.group ?? ''}`).includes(q)));
}

/** The condition a buyer sees: a scam shows the item as new unless it is the GM looking. */
export function shownCondition(listing: Listing, gm: boolean): ItemCondition {
  return listing.conditionHidden && !gm ? 'Normal' : listing.condition;
}

/** Stalls in map order (number), the entrance (0) first. */
export function sortStalls(stalls: Stall[]): Stall[] {
  return [...stalls].sort((a, b) => a.number - b.number);
}

export interface WheelSlice {
  label: string;
  start: number;
  end: number;
}

/** Splits the 360° wheel by weight, in the order of the rules file (the first slice starts at the top). */
export function wheelSlices(entries: Pick<PrizeEntry, 'label' | 'weight'>[]): WheelSlice[] {
  const total = entries.reduce((s, e) => s + e.weight, 0);
  let angle = 0;
  return entries.map((e) => {
    const start = angle;
    angle += (e.weight / total) * 360;
    return { label: e.label, start, end: angle };
  });
}

/**
 * Rotation that leaves the pointer (top) on the middle of the slice the roll landed in. `roll` is 1-based over the
 * total weight, as the server rolls it. `turns` full spins come first so the wheel visibly spins.
 */
export function wheelRotation(entries: Pick<PrizeEntry, 'weight'>[], roll: number, turns = 4): number {
  let acc = 0;
  let index = entries.length - 1;
  for (let i = 0; i < entries.length; i++) {
    acc += entries[i].weight;
    if (roll <= acc) { index = i; break; }
  }
  const slices = wheelSlices(entries.map((e, i) => ({ label: String(i), weight: e.weight })));
  const middle = (slices[index].start + slices[index].end) / 2;
  return turns * 360 + (360 - middle);
}

// ------------------------------------------------------------------ Haggle form

export interface HaggleState {
  buyer: { trading: number; rank: number };
  seller: { trading: number; rank: number };
  buyerDice: string;
  sellerDice: string;
}

/** Seller side starts from the market organizer (COOL + Trading and Operator Rank of the book's stat block). */
export function initialHaggle(market?: Pick<NightMarketDetail, 'organizer'>): HaggleState {
  return {
    buyer: { trading: 10, rank: 1 },
    seller: { trading: market?.organizer.trading ?? 10, rank: market?.organizer.operatorRank ?? 0 },
    buyerDice: '',
    sellerDice: '',
  };
}

/** "7" or "10 4": the d10 and, after a natural 10 or 1, the extra d10 (CP:R p.130). Empty: the server rolls. */
export function parseDice(text: string): number[] | null {
  const parts = text.split(/[\s,+]+/).filter(Boolean).map(Number);
  return parts.length === 0 || parts.some((n) => !Number.isInteger(n)) ? null : parts.slice(0, 2);
}

export function toHaggleRequest(h: HaggleState): HaggleRequest {
  return { buyer: h.buyer, seller: h.seller, buyerRolls: parseDice(h.buyerDice), sellerRolls: parseDice(h.sellerDice) };
}
