import { describe, expect, it } from 'vitest';
import type { TokenView } from '../../api/tableTypes';
import { fightEntries, fightRequest } from './fight';

const token = (id: string, change: Partial<TokenView>): TokenView => ({
  id, label: id, x: 0, y: 0, width: 1, height: 1, rotation: 0, locked: false, imageMediaId: null, imageUrl: null, color: '#fff',
  ownerSeatId: null, visibility: 'All', visibleSeatIds: [], characterId: null, combatantId: null, rangeRingMeters: null,
  controllable: true, npcId: null, compendiumNpcId: null, ...change,
});

describe('fightEntries', () => {
  const entries = fightEntries([
    token('V', { characterId: 1 }),
    token('Drone', { ownerSeatId: 3 }),
    token('Capanga', { npcId: 7 }),
    token('Smasher', { compendiumNpcId: 'adam-smasher' }),
    token('Barril', {}),
  ]);

  it('brings in the characters and NPCs, and leaves plain tokens out', () => {
    expect(entries.map((e) => [e.tokenId, e.include])).toEqual([['V', true], ['Drone', false], ['Capanga', true], ['Smasher', true], ['Barril', false]]);
  });

  it('puts PCs and the players\' tokens on the allies\' side, the rest against them', () => {
    expect(entries.map((e) => e.side)).toEqual(['Ally', 'Ally', 'Enemy', 'Enemy', 'Enemy']);
  });

  it('tells where each fighter comes from', () => {
    expect(entries.map((e) => e.kind)).toEqual(['pc', 'generic', 'npc', 'compendium', 'generic']);
  });
});

describe('fightRequest', () => {
  it('sends only the chosen tokens and a trimmed name', () => {
    const entries = fightEntries([token('V', { characterId: 1 }), token('Barril', {})]);
    expect(fightRequest('  Tiroteio  ', entries)).toEqual({ name: 'Tiroteio', tokens: [{ tokenId: 'V', side: 'Ally' }] });
    expect(fightRequest('', entries).name).toBeNull();
  });
});
