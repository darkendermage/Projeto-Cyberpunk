import { describe, expect, it } from 'vitest';
import type { TableAttack, TokenView } from '../../../api/tableTypes';
import { effectKind, missPoint, planEffect, shotLabel, tokenOfFighter } from './effects';

const token = (id: string, change: Partial<TokenView>): TokenView => ({
  id, label: id, x: 0, y: 0, width: 1, height: 1, rotation: 0, locked: false, imageMediaId: null, imageUrl: null, color: '#fff',
  ownerSeatId: null, visibility: 'All', visibleSeatIds: [], characterId: null, combatantId: null, rangeRingMeters: null,
  controllable: false, npcId: null, compendiumNpcId: null, ...change,
});
const target = (id: string, change: Partial<TableAttack['targets'][number]> = {}): TableAttack['targets'][number] => ({
  id, name: id, pc: false, hit: true, damage: 12, through: 5, location: 'corpo', dv: 15, defenseRoll: null, damageDice: [], criticalInjury: null, ...change,
});
const attack = (change: Partial<TableAttack>): TableAttack => ({
  encounterId: 1, seq: 1, attackerId: 'C1', attackerName: 'Kaze', attackerPc: true, weapon: 'Rifle', mode: 'Single', melee: false,
  attackRoll: 7, attackTotal: 21, undone: false, targets: [target('C2')], ...change,
});

describe('effects', () => {
  const tokens = [token('T1', { characterId: 7, x: 2, y: 2 }), token('T2', { combatantId: 'C2', x: 6, y: 2, width: 2, height: 2 })];
  const queue = [{ id: 'C1', characterId: 7 }, { id: 'C2', characterId: null }];

  it('finds a PC by its sheet when its token is not linked to the fight', () => {
    expect(tokenOfFighter('C1', tokens, queue)?.id).toBe('T1');
    expect(tokenOfFighter('C2', tokens, queue)?.id).toBe('T2');
    expect(tokenOfFighter('C9', tokens, queue)).toBeNull();
  });

  it('draws from the shooter to each target center, by the kind of attack', () => {
    const plan = planEffect(attack({}), tokens, queue)!;
    expect(plan).toMatchObject({ kind: 'shot', from: { x: 2.5, y: 2.5 }, shots: [{ to: { x: 7, y: 3 }, hit: true, label: '−5' }] });
    expect(effectKind({ melee: true, mode: 'Single' })).toBe('melee');
    expect(effectKind({ melee: false, mode: 'Autofire' })).toBe('burst');
    expect(effectKind({ melee: false, mode: 'Explosive' })).toBe('blast');
  });

  it('shows the players a hit on an NPC without the number, and a miss as a miss', () => {
    expect(shotLabel(target('C2', { damage: null, through: null }))).toMatch(/ACERTO|HIT/);
    expect(shotLabel(target('C2', { hit: false }))).toMatch(/ERROU|MISS/);
    expect(shotLabel(target('C2', { through: 0 }))).toMatch(/ARMADURA|ARMOR/);
  });

  it('draws nothing when the shooter is not on this screen, and skips targets off it', () => {
    expect(planEffect(attack({ attackerId: 'C9' }), tokens, queue)).toBeNull();
    expect(planEffect(attack({ targets: [target('C9')] }), tokens, queue)).toBeNull();
  });

  it('sends a miss past the target', () => {
    const p = missPoint({ x: 0, y: 0 }, { x: 4, y: 0 });
    expect(p.x).toBeGreaterThan(4);
    expect(p.y).not.toBe(0);
  });
});
