import { describe, expect, it } from 'vitest';
import type { PartyMember, TokenView } from '../../api/tableTypes';
import { buildStrip } from './strip';

const token = (id: string, change: Partial<TokenView>): TokenView => ({
  id, label: id, x: 0, y: 0, width: 1, height: 1, rotation: 0, locked: false, imageMediaId: null, imageUrl: null, color: '#fff',
  ownerSeatId: null, visibility: 'All', visibleSeatIds: [], characterId: null, combatantId: null, rangeRingMeters: null,
  controllable: false, npcId: null, compendiumNpcId: null, ...change,
});
const member = (characterId: number, name: string): PartyMember => ({
  characterId, name, hp: 30, maxHp: 40, woundState: 'Lightly', spHead: 7, maxSpHead: 11, spBody: 11, maxSpBody: 11,
});

describe('buildStrip', () => {
  const tokens = [
    token('T1', { label: 'V', characterId: 1, imageUrl: '/v.png' }),
    token('T2', { label: 'Ganger', combatantId: 'C2' }),
    token('T3', { label: 'Barril' }),
    token('T4', { label: 'V de novo', characterId: 1 }),
  ];
  const strip = buildStrip(tokens, [member(1, 'Valerie')], new Map([['C2', { woundState: 'Seriously' as const, dead: false }]]));

  it('shows the characters and the fighters, once each, and leaves props out', () => {
    expect(strip.map((s) => s.tokenId)).toEqual(['T1', 'T2']);
  });

  it('gives the PCs their numbers and the NPCs only what the viewer may see', () => {
    expect(strip[0]).toMatchObject({ name: 'Valerie', hp: 30, maxHp: 40, spHead: 7, spBody: 11, imageUrl: '/v.png' });
    expect(strip[1]).toMatchObject({ name: 'Ganger', woundState: 'Seriously', hp: null, spBody: null });
  });

  it('keeps the GM\'s numbers for NPCs when the fight gives them', () => {
    const gm = buildStrip([tokens[1]], [], new Map([['C2', { woundState: 'Lightly' as const, dead: false, hp: 12, maxHp: 20, spBody: 7, maxSpBody: 7, spHead: 4, maxSpHead: 4 }]]));
    expect(gm[0]).toMatchObject({ hp: 12, maxHp: 20, spBody: 7, spHead: 4 });
  });
});
