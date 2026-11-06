import { describe, expect, it } from 'vitest';
import type { WeaponView } from '../../api/encounterTypes';
import type { TableAttack } from '../../api/tableTypes';
import { aimAt, attackSummary, cannotFire, quickModes, startAim, weaponLabel } from './aim';

const rifle: WeaponView = {
  key: 'w1', name: 'Assault Rifle', class: 'AssaultRifle', skill: 'shoulder-arms', attackTotal: 14, autofireTotal: 12, damage: '5d6',
  rof: 1, rounds: 25, magazine: 25, autofireMultiplier: 4, melee: false, halvesArmor: false,
};

describe('aim', () => {
  it('offers the weapon modes without the area ones', () => {
    expect(quickModes(rifle)).toEqual(['Single', 'Autofire', 'AimedHead', 'AimedHand', 'AimedLeg']);
    expect(quickModes({ ...rifle, class: 'GrenadeLauncher', autofireMultiplier: null })).not.toContain('Explosive');
  });

  it('starts without a target and never aims at the shooter', () => {
    const aim = startAim('C1', 'Kaze', rifle);
    expect(aim).toMatchObject({ mode: 'Single', targetId: null });
    expect(aimAt(aim, 'C1').targetId).toBeNull();
    expect(aimAt(aim, 'C2').targetId).toBe('C2');
  });

  it('labels the weapon and refuses an empty magazine', () => {
    expect(weaponLabel(rifle)).toBe('Assault Rifle +14 · 5d6 · 25/25');
    expect(cannotFire(rifle)).toBeNull();
    expect(cannotFire({ ...rifle, rounds: 0 })).not.toBeNull();
    expect(cannotFire({ ...rifle, melee: true, magazine: null, rounds: null })).toBeNull();
  });

  it('sums up hits, misses and the damage the reader may see', () => {
    const base: TableAttack = {
      encounterId: 1, seq: 1, attackerId: 'C1', attackerName: 'Kaze', attackerPc: true, weapon: 'Rifle', mode: 'Single', melee: false,
      attackRoll: 7, attackTotal: 21, undone: false, targets: [],
    };
    const target = { id: 'C2', name: 'Ganger', pc: false, hit: true, damage: 12, through: 5, location: 'corpo', dv: 15, defenseRoll: null, damageDice: [], criticalInjury: null };
    expect(attackSummary({ ...base, targets: [target] })).toContain('12');
    expect(attackSummary({ ...base, targets: [{ ...target, damage: null, through: null }] })).not.toMatch(/\d/);
    expect(attackSummary({ ...base, targets: [{ ...target, hit: false }] })).toMatch(/Errou|Missed/);
  });
});
