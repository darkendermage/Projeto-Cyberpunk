import { describe, expect, it } from 'vitest';
import type { TokenView } from '../../api/tableTypes';
import { buildSources, matchSources, sourceFields, sourceOf, tokenInputOf } from './sources';

const list = buildSources(
  [{ id: 1, name: 'Valerie', handle: 'V' }, { id: 2, name: 'Jackie Welles', handle: null }],
  [{ id: 7, name: 'José Capanga' }, { id: 8, name: 'Guarda Arasaka' }],
  [{ id: 'adam-smasher', name: 'Adam Smasher' }, { id: 'rogue', name: 'Rogue Amendiares' }],
);

describe('buildSources', () => {
  it('names characters by handle and keeps every group', () => {
    expect(list.map((s) => `${s.kind}:${s.id}:${s.name}`)).toEqual([
      'character:1:V', 'character:2:Jackie Welles', 'npc:7:José Capanga', 'npc:8:Guarda Arasaka',
      'compendium:adam-smasher:Adam Smasher', 'compendium:rogue:Rogue Amendiares',
    ]);
  });
});

describe('matchSources', () => {
  it('ignores case and accents', () => {
    expect(matchSources(list, 'jose').map((s) => s.id)).toEqual(['7']);
  });

  it('puts names that start with the query first', () => {
    expect(matchSources(list, 'a')[0].name).toBe('Adam Smasher');
    expect(matchSources(list, 'ro')[0].name).toBe('Rogue Amendiares');
  });

  it('caps the results', () => {
    expect(matchSources(list, '', 3)).toHaveLength(3);
  });
});

describe('sourceFields', () => {
  it('sets exactly one link', () => {
    expect(sourceFields({ kind: 'npc', id: '7', name: 'José' })).toEqual({ characterId: null, npcId: 7, compendiumNpcId: null });
    expect(sourceFields({ kind: 'compendium', id: 'adam-smasher', name: 'Adam' })).toEqual({ characterId: null, npcId: null, compendiumNpcId: 'adam-smasher' });
    expect(sourceFields(null)).toEqual({ characterId: null, npcId: null, compendiumNpcId: null });
  });
});

describe('sourceOf', () => {
  it('finds what the token stands for', () => {
    expect(sourceOf({ characterId: null, npcId: null, compendiumNpcId: 'rogue' }, list)?.name).toBe('Rogue Amendiares');
    expect(sourceOf({ characterId: 2, npcId: null, compendiumNpcId: null }, list)?.kind).toBe('character');
    expect(sourceOf({ characterId: null, npcId: null, compendiumNpcId: null }, list)).toBeNull();
  });
});

describe('tokenInputOf', () => {
  it('keeps the NPC links when the token is saved again', () => {
    const token: TokenView = {
      id: 'T1', label: 'Royce', x: 1, y: 2, width: 1, height: 1, rotation: 0, locked: false, imageMediaId: null, imageUrl: null,
      color: '#ff2a6d', ownerSeatId: null, visibility: 'All', visibleSeatIds: [], characterId: null, combatantId: 'C3',
      rangeRingMeters: null, controllable: true, npcId: 7, compendiumNpcId: null,
    };
    expect(tokenInputOf(token)).toMatchObject({ npcId: 7, compendiumNpcId: null, combatantId: 'C3', label: 'Royce' });
    expect(tokenInputOf({ ...token, npcId: null, compendiumNpcId: 'rogue' }).compendiumNpcId).toBe('rogue');
  });
});
