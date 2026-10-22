import { describe, expect, it } from 'vitest';
import type { Listing } from '../../api/nightMarketTypes';
import { cbkCategoryOf, filterListings, parseDice, shownCondition, sortStalls, wheelRotation, wheelSlices } from './nightMarket';

const listing = (over: Partial<Listing>): Listing => ({
  id: 1, stall: 'C3', name: 'Neo-Soviet Cyberarm', categoryKey: 'cyberware', basePrice: 100, baseCategory: 'Premium',
  price: 100, priceCategory: 'Premium', value: 100, valueCategory: 'Premium', condition: 'Normal', conditionHidden: false,
  forSale: true, origin: 'BlackChrome', warnings: [], ...over,
});

describe('filterListings', () => {
  const all = [
    listing({ id: 1 }),
    listing({ id: 2, stall: 'C4', name: 'Heavy Pistol', categoryKey: 'weapons', manufacturer: 'Militech' }),
    listing({ id: 3, stall: 'C4', name: 'Basic Ammo', categoryKey: 'ammunition', stock: 0 }),
    listing({ id: 4, stall: 'C6', name: 'Piranha Smash', categoryKey: 'drugs', forSale: false }),
  ];
  const none = { stall: null, category: null, q: '', gm: false, onlyForSale: false };

  it('filters by booth, CBK tab and text (accents and maker included)', () => {
    expect(filterListings(all, { ...none, stall: 'C4' }).map((l) => l.id)).toEqual([2, 3]);
    expect(filterListings(all, { ...none, category: 'weapons' }).map((l) => l.id)).toEqual([2, 3]);
    expect(filterListings(all, { ...none, q: 'militéch' }).map((l) => l.id)).toEqual([2]);
  });

  it('hides sold-out and lent items when only for sale', () => {
    expect(filterListings(all, { ...none, onlyForSale: true }).map((l) => l.id)).toEqual([1, 2]);
  });
});

describe('CBK tabs and conditions', () => {
  it('maps catalog categories to the tabs of the app', () => {
    expect(cbkCategoryOf({ categoryKey: 'weapon-attachments' })).toBe('weapons');
    expect(cbkCategoryOf({ categoryKey: 'drugs' })).toBe('gear');
    expect(cbkCategoryOf({ categoryKey: null })).toBe('other');
  });

  it('a scam looks new to the players but not to the GM', () => {
    const scam = listing({ condition: 'Damaged', conditionHidden: true });
    expect(shownCondition(scam, false)).toBe('Normal');
    expect(shownCondition(scam, true)).toBe('Damaged');
  });

  it('sorts stalls by their map number', () => {
    const stalls = [{ code: 'C6', number: 6, label: 'x', listings: 1 }, { code: 'C0', number: 0, label: 'Entrada', listings: 1 }];
    expect(sortStalls(stalls).map((s) => s.code)).toEqual(['C0', 'C6']);
  });
});

describe('door prize wheel', () => {
  const entries = [{ label: 'Junk', weight: 3 }, { label: 'Smash', weight: 1 }];

  it('splits the circle by weight', () => {
    expect(wheelSlices(entries)).toEqual([
      { label: 'Junk', start: 0, end: 270 },
      { label: 'Smash', start: 270, end: 360 },
    ]);
  });

  it('stops with the pointer on the middle of the rolled slice', () => {
    expect(wheelRotation(entries, 1, 0)).toBe(360 - 135);
    expect(wheelRotation(entries, 3, 0)).toBe(360 - 135);
    expect(wheelRotation(entries, 4, 2)).toBe(720 + 360 - 315);
  });
});

describe('typed dice', () => {
  it('reads the d10 and the extra die of a critical, or leaves it to the server', () => {
    expect(parseDice('7')).toEqual([7]);
    expect(parseDice('10 4')).toEqual([10, 4]);
    expect(parseDice('1, 3')).toEqual([1, 3]);
    expect(parseDice('')).toBeNull();
    expect(parseDice('x')).toBeNull();
  });
});
