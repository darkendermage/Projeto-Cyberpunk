import { describe, expect, it } from 'vitest';
import { nextSort, pageOf, sortRows } from './sorting';

const rows = [
  { name: 'Kevlar', price: 50, category: 'Armor' },
  { name: 'agent', price: 100, category: 'Gear' },
  { name: 'Heavy Pistol', price: null, category: 'Weapon' },
  { name: 'Basic Ammo', price: 10, category: 'Ammunition' },
];
const value = (r: (typeof rows)[number], k: 'name' | 'price' | 'category') => r[k];

describe('sortRows', () => {
  it('sorts by price both ways, leaving missing prices last', () => {
    expect(sortRows(rows, { key: 'price', dir: 'asc' }, value).map((r) => r.price)).toEqual([10, 50, 100, null]);
    expect(sortRows(rows, { key: 'price', dir: 'desc' }, value).map((r) => r.price)).toEqual([100, 50, 10, null]);
  });

  it('sorts text ignoring case and keeps the original order without a sort', () => {
    expect(sortRows(rows, { key: 'name', dir: 'asc' }, value).map((r) => r.name)).toEqual(['agent', 'Basic Ammo', 'Heavy Pistol', 'Kevlar']);
    expect(sortRows(rows, null, value)).toEqual(rows);
  });
});

describe('nextSort', () => {
  it('starts ascending and flips on the same column', () => {
    const first = nextSort(null, 'price');
    expect(first).toEqual({ key: 'price', dir: 'asc' });
    expect(nextSort(first, 'price')).toEqual({ key: 'price', dir: 'desc' });
    expect(nextSort({ key: 'price', dir: 'desc' }, 'name')).toEqual({ key: 'name', dir: 'asc' });
  });
});

describe('pageOf', () => {
  it('cuts pages of 12 and clamps a page past the end', () => {
    const many = Array.from({ length: 30 }, (_, i) => i);
    expect(pageOf(many, 1, 12).items).toHaveLength(12);
    expect(pageOf(many, 3, 12)).toEqual({ items: [24, 25, 26, 27, 28, 29], page: 3, totalPages: 3 });
    expect(pageOf(many, 9, 12).page).toBe(3);
    expect(pageOf([], 1, 12).totalPages).toBe(1);
  });
});
