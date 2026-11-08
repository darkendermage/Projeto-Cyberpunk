import type { InventoryLine } from '../../api/characterTypes';

/** The panels of the Gear tab, in order. */
export type InventoryGroup = 'weapons' | 'armor' | 'misc' | 'clothing';

const WEAPON_CATEGORIES = new Set(['Weapon', 'WeaponAttachment', 'Ammunition']);

// Gear tab panels: clothing (clothes + Fashionware) first, then weapons with ammo and attachments, then anything
// with SP or shield HP, then misc.
export function groupInventory(items: readonly InventoryLine[]): Record<InventoryGroup, InventoryLine[]> {
  const groups: Record<InventoryGroup, InventoryLine[]> = { weapons: [], armor: [], misc: [], clothing: [] };
  for (const i of items) {
    if (i.fashion) groups.clothing.push(i);
    else if (WEAPON_CATEGORIES.has(i.category)) groups.weapons.push(i);
    else if (i.category === 'Armor' || i.sp != null || i.shieldHp != null) groups.armor.push(i);
    else groups.misc.push(i);
  }
  return groups;
}
