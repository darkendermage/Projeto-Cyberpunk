import { describe, expect, it } from 'vitest';
import type { TableAttack } from '../../../api/tableTypes';
import { attackDice } from './attackDice';

const attack = (over: Partial<TableAttack> = {}): TableAttack => ({
  encounterId: 1, seq: 1, attackerId: 'pc1', attackerName: 'Suzu', attackerPc: true, weapon: 'Pistol', mode: 'Single', melee: false,
  attackRoll: 10, attackTotal: 25, undone: false, attackExtra: 4, critDice: [3, 5],
  targets: [
    { id: 'g1', name: 'Ganger', pc: false, hit: true, damage: 14, through: 7, location: 'Body', dv: 15, defenseRoll: 6, damageDice: [6, 6, 2], criticalInjury: 'Broken Arm' },
    { id: 'g2', name: 'Other', pc: false, hit: false, damage: null, through: null, location: 'Body', dv: 15, defenseRoll: null, damageDice: [], criticalInjury: null },
  ],
  ...over,
});

describe('attackDice', () => {
  it('lists the attack d10 with its extra, the defense, the damage and the critical dice', () => {
    const groups = attackDice(attack());

    expect(groups.map((g) => g.dice.map((d) => `${d.value}/${d.sides}${d.extra ? '+' : ''}`))).toEqual([
      ['10/10', '4/10+'],
      ['6/10'],
      ['6/6', '6/6', '2/6'],
      ['3/6', '5/6'],
    ]);
    expect(groups[1].label).toContain('Ganger');
  });

  it('a negative extra (natural 1) stays negative and a miss shows no damage', () => {
    const groups = attackDice(attack({ attackRoll: 1, attackExtra: -7, critDice: null, targets: [attack().targets[1]] }));

    expect(groups).toHaveLength(1);
    expect(groups[0].dice).toEqual([{ value: 1, sides: 10 }, { value: -7, sides: 10, extra: true }]);
  });

  it('has nothing when the numbers are hidden', () => {
    expect(attackDice(attack({ attackRoll: null, attackExtra: null, critDice: null, targets: [] }))).toEqual([]);
  });
});
