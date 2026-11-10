import { describe, expect, it } from 'vitest';
import type { InventoryLine, WearableLine } from '../../api/characterTypes';
import { filterStored, gearTag, paginate, splitGear, wornSlots } from './gearLayout';

const line = (instanceId: string, category: string, change: Partial<InventoryLine> = {}): InventoryLine => ({
  instanceId, itemId: 1, name: instanceId, slug: instanceId, category, quantity: 1, state: 'Stored', worn: null, caliber: null, rounds: null,
  sp: null, currentSp: null, shieldHp: null, shieldHpMax: null, unitPrice: null, pricePaid: null, origin: 'Purchase', paidFrom: 'Balance',
  fashion: false, notes: null, ...change,
});

const wearable = (accepts: WearableLine['accepts'], worn: boolean): WearableLine => ({
  instanceId: accepts, itemId: 1, name: accepts, accepts, slots: 1, slotsUsed: 0, cyberfingers: 0, cyberfingersUsed: 0, worn, side: null,
  coversArm: accepts === 'Cyberarm', options: [], builtIn: [], covered: [], requirement: null, warnings: [], source: '',
});

describe('splitGear', () => {
  it('keeps equipped and carried items with the character, stored ones in the inventory', () => {
    const { onPerson, stored } = splitGear([line('pistol', 'Weapon', { state: 'Equipped' }), line('ammo', 'Ammunition', { state: 'Carried' }), line('agent', 'Gear')]);

    expect(onPerson.map((i) => i.instanceId)).toEqual(['pistol', 'ammo']);
    expect(stored.map((i) => i.instanceId)).toEqual(['agent']);
  });
});

describe('filterStored', () => {
  const items = [line('Pistol', 'Weapon'), line('Jacket', 'Armor', { fashion: true }), line('Armorjack', 'Armor', { sp: 11 }), line('Flashlight', 'Gear')];
  const ids = (filter: Parameters<typeof filterStored>[1], query = '') => filterStored(items, filter, query).map((i) => i.instanceId);

  it('filters by the chips', () => {
    expect(ids('weapons')).toEqual(['Pistol']);
    expect(ids('clothing')).toEqual(['Jacket']);
    expect(ids('other')).toEqual(['Armorjack', 'Flashlight']);
    expect(ids('all')).toHaveLength(4);
  });
  it('searches the name, ignoring case and accents', () => expect(ids('all', 'flash')).toEqual(['Flashlight']));
});

describe('paginate', () => {
  const rows = Array.from({ length: 23 }, (_, i) => i);

  it('cuts pages of 10', () => {
    expect(paginate(rows, 0)).toEqual({ rows: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9], page: 0, pages: 3 });
    expect(paginate(rows, 2).rows).toEqual([20, 21, 22]);
  });
  it('keeps the page inside the list when it shrinks', () => expect(paginate(rows.slice(0, 5), 2)).toEqual({ rows: [0, 1, 2, 3, 4], page: 0, pages: 1 }));
  it('has one empty page for an empty list', () => expect(paginate([], 0)).toEqual({ rows: [], page: 0, pages: 1 }));
});

describe('gearTag', () => {
  it('says where armor is worn, that clothes are worn, and the state of the rest', () => {
    expect(gearTag(line('a', 'Armor', { sp: 11, state: 'Equipped', worn: 'Body' })).label).toBe('Body');
    expect(gearTag(line('a', 'Armor', { sp: 11, state: 'Equipped', worn: 'Head' })).label).toBe('Head');
    expect(gearTag(line('t', 'Armor', { fashion: true, state: 'Equipped' })).label).toBe('Worn');
    expect(gearTag(line('p', 'Weapon', { state: 'Equipped' })).label).toBe('Equipped');
    expect(gearTag(line('m', 'Ammunition', { state: 'Carried' })).label).toBe('Carried');
  });
});

describe('wornSlots', () => {
  const filled = (slots: ReturnType<typeof wornSlots>) => Object.fromEntries(slots.filter((s) => s.filled).map((s) => [s.key, s.names]));

  it('shows the twelve places in grid order', () => {
    expect(wornSlots([], []).map((s) => s.key)).toEqual(['head', 'hat', 'eyes', 'ears', 'jewelry', 'top', 'jacket', 'body', 'hands', 'bottom', 'feet', 'outfit']);
  });

  it('fills clothes by catalog slot, armor by where it is worn and eyes / ears / hands by worn wearables', () => {
    const slots = wornSlots([wearable('Cybereye', true), wearable('Cyberaudio', false), wearable('Cyberarm', true)], [
      line('jack', 'Armor', { sp: 11, state: 'Equipped', worn: 'Body' }),
      line('helmet', 'Armor', { sp: 11, state: 'Equipped', worn: 'Head' }),
      line('jacket', 'Armor', { fashion: true, state: 'Equipped', clothingSlot: 'Jacket' }),
      line('shades', 'Armor', { fashion: true, state: 'Equipped', clothingSlot: 'Eyewear' }),
    ]);
    expect(filled(slots)).toEqual({ head: ['helmet'], eyes: ['shades', 'Cybereye'], jacket: ['jacket'], body: ['jack'], hands: ['Cyberarm'] });
  });

  it('leaves carried and stored clothes out', () => {
    const slots = wornSlots([], [
      line('boots', 'Armor', { fashion: true, state: 'Carried', clothingSlot: 'Footwear' }),
      line('top', 'Armor', { fashion: true, clothingSlot: 'Top' }),
    ]);
    expect(filled(slots)).toEqual({});
  });
});
