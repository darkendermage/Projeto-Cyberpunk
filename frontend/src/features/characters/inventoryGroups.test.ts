import { describe, expect, it } from 'vitest';
import type { InventoryLine } from '../../api/characterTypes';
import { groupInventory } from './inventoryGroups';

const line = (instanceId: string, category: string, change: Partial<InventoryLine> = {}): InventoryLine => ({
  instanceId, itemId: 1, name: instanceId, slug: instanceId, category, quantity: 1, state: 'Carried', worn: null, caliber: null, rounds: null,
  sp: null, currentSp: null, shieldHp: null, shieldHpMax: null, unitPrice: null, pricePaid: null, origin: 'Purchase', paidFrom: 'Balance',
  fashion: false, notes: null, ...change,
});

describe('groupInventory', () => {
  const groups = groupInventory([
    line('pistol', 'Weapon'),
    line('ammo', 'Ammunition', { rounds: 30 }),
    line('scope', 'WeaponAttachment'),
    line('jacket', 'Armor', { sp: 11 }),
    line('shield', 'Gear', { shieldHp: 10, shieldHpMax: 10 }),
    line('top', 'Armor', { fashion: true }),
    line('techhair', 'Cyberware', { fashion: true }),
    line('agent', 'Gear'),
    line('pills', 'Drug'),
  ]);
  const ids = (key: keyof typeof groups) => groups[key].map((l) => l.instanceId);

  it('puts weapons, ammunition and attachments together', () => expect(ids('weapons')).toEqual(['pistol', 'ammo', 'scope']));
  it('puts armor and shields in armor', () => expect(ids('armor')).toEqual(['jacket', 'shield']));
  it('puts clothes and Fashionware in clothing, even when the category is Armor', () => expect(ids('clothing')).toEqual(['top', 'techhair']));
  it('leaves everything else in misc', () => expect(ids('misc')).toEqual(['agent', 'pills']));
});
