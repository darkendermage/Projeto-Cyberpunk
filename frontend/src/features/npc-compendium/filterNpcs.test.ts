import { describe, expect, it } from 'vitest';
import type { NpcSummary } from '../../api/npcCompendium';
import { filterNpcs, npcBooks, npcRoles, type NpcFilters } from './filterNpcs';

const npc = (over: Partial<NpcSummary>): NpcSummary => ({
  id: 'x', name: 'X', aliases: [], kind: 'Sheet', role: 'Solo', roleRank: 4, rep: null, affiliation: null,
  sources: [{ book: 'Core', page: 1 }], summary: '', ...over,
});

const list: NpcSummary[] = [
  npc({ id: 'con', name: 'Kinneth "Con" de Léon', aliases: ['Malarkey'], role: 'Fixer', kind: 'Profile', sources: [{ book: 'Tales', page: 5 }] }),
  npc({ id: 'rogue', name: 'Rogue', sources: [{ book: 'Core', page: 9 }, { book: 'Tales', page: 7 }] }),
  npc({ id: 'bozo', name: 'Bozo', role: 'Nomad', sources: [{ book: 'Core', page: 2 }] }),
];
const none: NpcFilters = { q: '', book: '', role: '', sheet: 'all' };
const ids = (r: NpcSummary[]) => r.map((n) => n.id);

describe('filterNpcs', () => {
  it('finds by name ignoring accents and case', () => {
    expect(ids(filterNpcs(list, { ...none, q: 'leon' }))).toEqual(['con']);
  });
  it('finds by alias', () => {
    expect(ids(filterNpcs(list, { ...none, q: 'malarkey' }))).toEqual(['con']);
  });
  it('filters by book in any source', () => {
    expect(ids(filterNpcs(list, { ...none, book: 'Tales' }))).toEqual(['con', 'rogue']);
    expect(ids(filterNpcs(list, { ...none, book: 'Core' }))).toEqual(['rogue', 'bozo']);
  });
  it('filters by role and by sheet kind', () => {
    expect(ids(filterNpcs(list, { ...none, role: 'Nomad' }))).toEqual(['bozo']);
    expect(ids(filterNpcs(list, { ...none, sheet: 'profile' }))).toEqual(['con']);
    expect(ids(filterNpcs(list, { ...none, sheet: 'sheet' }))).toEqual(['rogue', 'bozo']);
  });
  it('empty filters keep the list', () => {
    expect(filterNpcs(list, none)).toEqual(list);
  });
  it('lists unique sorted books and roles', () => {
    expect(npcBooks(list)).toEqual(['Core', 'Tales']);
    expect(npcRoles(list)).toEqual(['Fixer', 'Nomad', 'Solo']);
  });
});
