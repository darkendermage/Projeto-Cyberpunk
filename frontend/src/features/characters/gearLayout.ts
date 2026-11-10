import type { ClothingSlot, InventoryLine, WearableLine } from '../../api/characterTypes';
import { t, tv } from '../../i18n';
import { groupInventory } from './inventoryGroups';

/** The chips over the stored items. */
export type GearFilter = 'all' | 'weapons' | 'clothing' | 'other';

/** Items per page of the inventory list. */
export const PAGE_SIZE = 10;

/** Items per page of each group under "Equipados" (weapons, armor, clothing, misc). */
export const GROUP_PAGE_SIZE = 4;

/** With the character (equipped or carried) for "Equipped", stored for "Inventory". */
export function splitGear(items: readonly InventoryLine[]): { onPerson: InventoryLine[]; stored: InventoryLine[] } {
  return { onPerson: items.filter((i) => i.state !== 'Stored'), stored: items.filter((i) => i.state === 'Stored') };
}

const plain = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

/** The stored items a chip and the search show: "other" is armor and the rest; the search reads the name. */
export function filterStored(items: readonly InventoryLine[], filter: GearFilter, query: string): InventoryLine[] {
  const g = groupInventory(items);
  const base = filter === 'weapons' ? g.weapons : filter === 'clothing' ? g.clothing : filter === 'other' ? [...g.armor, ...g.misc] : [...items];
  const q = plain(query.trim());
  return q ? base.filter((i) => plain(tv(i.name)).includes(q) || plain(i.name).includes(q)) : base;
}

/** One page of rows; the page stays inside the list when it shrinks (a sale, a filter). */
export function paginate<T>(rows: readonly T[], page: number, size = PAGE_SIZE): { rows: T[]; page: number; pages: number } {
  const pages = Math.max(1, Math.ceil(rows.length / size));
  const at = Math.min(Math.max(0, page), pages - 1);
  return { rows: rows.slice(at * size, at * size + size), page: at, pages };
}

/** The tag on an item: where armor is worn, clothes worn, else its state. */
export function gearTag(i: InventoryLine): { label: string; tone: 'ok' | 'info' | 'muted' } {
  if (i.state === 'Equipped' && i.worn === 'Body') return { label: t('Corpo', 'Body'), tone: 'ok' };
  if (i.state === 'Equipped' && i.worn === 'Head') return { label: t('Cabeça', 'Head'), tone: 'ok' };
  if (i.state === 'Equipped' && i.fashion) return { label: t('Vestida', 'Worn'), tone: 'ok' };
  if (i.state === 'Equipped') return { label: t('Equipado', 'Equipped'), tone: 'ok' };
  if (i.state === 'Carried') return { label: t('Carregado', 'Carried'), tone: 'info' };
  return { label: t('Guardado', 'Stored'), tone: 'muted' };
}

export type WornSlotKey = 'head' | 'hat' | 'eyes' | 'ears' | 'jewelry' | 'top' | 'jacket' | 'body' | 'hands' | 'bottom' | 'feet' | 'outfit';

export interface WornSlot { key: WornSlotKey; label: string; hint: string; filled: boolean; names: string[] }

// What the character is wearing, place by place. Clothes by their catalog slot, armor by where it was put on,
// eyes / ears / hands also by the wearables with Option Slots (Smart Glasses, Smart Ears, gloves).
// Only equipped items count: carried clothes are in the bag, not on the body.
export function wornSlots(wearables: readonly WearableLine[], items: readonly InventoryLine[]): WornSlot[] {
  const equipped = items.filter((i) => i.state === 'Equipped');
  const clothes = (slot: ClothingSlot) => equipped.filter((i) => i.clothingSlot === slot).map((i) => tv(i.name));
  const armor = (where: 'Body' | 'Head') => equipped.filter((i) => i.worn === where).map((i) => tv(i.name));
  const wearable = (accepts: WearableLine['accepts']) => wearables.filter((w) => w.worn && w.accepts === accepts).map((w) => tv(w.name));
  const slot = (key: WornSlotKey, label: string, hint: string, names: string[]): WornSlot => {
    const unique = [...new Set(names)];
    return { key, label, hint, filled: unique.length > 0, names: unique };
  };
  const armorHint = t('Armadura', 'Armor');
  const clothingHint = t('Roupa', 'Clothing');
  return [
    slot('head', t('Cabeça', 'Head'), armorHint, armor('Head')),
    slot('hat', t('Chapéu', 'Hat'), clothingHint, clothes('Headwear')),
    slot('eyes', t('Óculos', 'Eyewear'), t('Roupa ou vestível', 'Clothing or wearable'), [...clothes('Eyewear'), ...wearable('Cybereye')]),
    slot('ears', t('Orelhas', 'Ears'), t('Vestível', 'Wearable'), wearable('Cyberaudio')),
    slot('jewelry', t('Acessórios', 'Accessories'), t('Joias e acessórios', 'Jewelry and accessories'), clothes('Jewelry')),
    slot('top', t('Parte de cima', 'Top'), clothingHint, clothes('Top')),
    slot('jacket', t('Jaqueta', 'Jacket'), clothingHint, clothes('Jacket')),
    slot('body', t('Corpo', 'Body'), armorHint, armor('Body')),
    slot('hands', t('Mãos', 'Hands'), t('Vestível', 'Wearable'), wearable('Cyberarm')),
    slot('bottom', t('Parte de baixo', 'Bottom'), clothingHint, clothes('Bottom')),
    slot('feet', t('Calçado', 'Footwear'), clothingHint, clothes('Footwear')),
    slot('outfit', t('Traje completo', 'Full outfit'), clothingHint, clothes('FullOutfit')),
  ];
}
