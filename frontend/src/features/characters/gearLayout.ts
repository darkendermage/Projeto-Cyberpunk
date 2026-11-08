import type { InventoryLine, WearableLine } from '../../api/characterTypes';
import { t, tv } from '../../i18n';
import { groupInventory } from './inventoryGroups';

/** The chips over the stored items. */
export type GearFilter = 'all' | 'weapons' | 'clothing' | 'other';

/** Items per page of the inventory list. */
export const PAGE_SIZE = 10;

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

export interface BodySlot { key: 'eyes' | 'ears' | 'hands' | 'body'; label: string; filled: boolean; names: string[] }

/** The strip of worn places: eyes, ears and hands by the wearables with slots, the body by armor worn on it. */
export function bodySlots(wearables: readonly WearableLine[], items: readonly InventoryLine[]): BodySlot[] {
  const worn = (accepts: WearableLine['accepts']) => wearables.filter((w) => w.worn && w.accepts === accepts).map((w) => tv(w.name));
  const body = items.filter((i) => i.state === 'Equipped' && i.worn === 'Body').map((i) => tv(i.name));
  const slot = (key: BodySlot['key'], label: string, names: string[]): BodySlot => ({ key, label, filled: names.length > 0, names });
  return [
    slot('eyes', t('Óculos', 'Eyes'), worn('Cybereye')),
    slot('ears', t('Orelhas', 'Ears'), worn('Cyberaudio')),
    slot('hands', t('Mãos', 'Hands'), worn('Cyberarm')),
    slot('body', t('Corpo', 'Body'), body),
  ];
}
