import { describe, expect, it } from 'vitest';
import { groupOf, isActive, NAV } from './navigation';

describe('NAV', () => {
  it('keeps Items, Characters and the Virtual Table on their own, the rest in groups', () => {
    expect(NAV.map((e) => (e.kind === 'link' ? e.to : `[${e.id}]`))).toEqual([
      '/catalog/weapons', '[collection]', '/characters', '[gm]', '/mesa', '[other]',
    ]);
  });

  it('puts the GM tools and the collection where the user asked', () => {
    const items = (id: string) => NAV.flatMap((e) => (e.kind === 'group' && e.id === id ? e.items.map((i) => i.to) : []));
    expect(items('collection')).toEqual(['/lists', '/favorites', '/compare']);
    expect(items('gm')).toEqual(['/enemies', '/npcs', '/combat', '/night-market', '/net-architecture']);
    expect(items('other')).toEqual(['/weather', '/rules', '/tables', '/martial-arts', '/glossary']);
  });
});

describe('active route', () => {
  it('matches a page and the pages under it', () => {
    expect(isActive('/catalog/armor', '/catalog/weapons', '/catalog')).toBe(true);
    expect(isActive('/combat/12', '/combat')).toBe(true);
    expect(isActive('/compare', '/combat')).toBe(false);
  });

  it('finds the group of the open page', () => {
    expect(groupOf('/night-market/abc')).toBe('gm');
    expect(groupOf('/favorites')).toBe('collection');
    expect(groupOf('/glossary')).toBe('other');
    expect(groupOf('/characters/3/ficha')).toBeNull();
  });
});
