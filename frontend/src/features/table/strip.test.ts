import { describe, expect, it } from 'vitest';
import type { PartyMember, TokenView } from '../../api/tableTypes';
import { buildStrip } from './strip';

const token = (id: string, change: Partial<TokenView>): TokenView => ({
  id, label: id, x: 0, y: 0, width: 1, height: 1, rotation: 0, locked: false, imageMediaId: null, imageUrl: null, color: '#fff',
  ownerSeatId: null, visibility: 'All', visibleSeatIds: [], characterId: null, combatantId: null, rangeRingMeters: null,
  controllable: false, npcId: null, compendiumNpcId: null, ...change,
});
const member = (characterId: number, name: string, change: Partial<PartyMember> = {}): PartyMember => ({
  characterId, name, hp: 30, maxHp: 40, woundState: 'Lightly', spHead: 7, maxSpHead: 11, spBody: 11, maxSpBody: 11,
  seatId: characterId * 10, online: true, inScene: true, imageUrl: null, ...change,
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

describe('buildStrip with the whole party', () => {
  const tokens = [token('T1', { label: 'V', characterId: 1 }), token('T2', { label: 'Ganger', combatantId: 'C2' })];
  const party = [member(1, 'Valerie'), member(2, 'Kiroshi', { inScene: false, online: false, imageUrl: '/k.png' })];
  const fighters = new Map([['C2', { woundState: 'Unhurt' as const, dead: false }]]);

  it('appends off-scene members after token cards', () => {
    const strip = buildStrip(tokens, party, fighters);
    expect(strip.map((s) => s.name)).toEqual(['Valerie', 'Ganger', 'Kiroshi']);
    expect(strip[2]).toMatchObject({ offScene: true, online: false, tokenId: 'seat:20', imageUrl: '/k.png', hp: 30 });
    expect(strip[0]).toMatchObject({ offScene: false, online: true });
  });

  it('gives an off-scene member no combatantId, so it cannot be aimed at', () => {
    expect(buildStrip(tokens, party, fighters)[2].combatantId).toBeNull();
  });

  it('orders fighters by the queue, non-fighters after and off-scene last', () => {
    const more = [...tokens, token('T3', { label: 'Aliado', characterId: 3 })];
    const all = [...party, member(3, 'Aliada')];
    // Valerie's token has no combatantId: she is matched to her fighter (P1) through her characterId.
    const strip = buildStrip(more, all, fighters, [{ id: 'C2', characterId: null }, { id: 'P1', characterId: 1 }]);
    expect(strip.map((s) => s.name)).toEqual(['Ganger', 'Valerie', 'Aliada', 'Kiroshi']);
  });

  it('keeps token order without a queue order', () => {
    const strip = buildStrip([tokens[1], tokens[0]], party, fighters);
    expect(strip.map((s) => s.name)).toEqual(['Ganger', 'Valerie', 'Kiroshi']);
  });
});
