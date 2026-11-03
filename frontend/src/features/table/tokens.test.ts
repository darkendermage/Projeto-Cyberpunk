import { describe, expect, it } from 'vitest';
import { damageCommand, damageFormula, initials, tokenStatus } from './tokens';

describe('tokenStatus', () => {
  it('uses the real HP when the viewer may see it', () => {
    const s = tokenStatus({ woundState: 'Lightly', hp: 30, maxHp: 40, dead: false, conditions: [], injuries: [] });
    expect(s.ratio).toBeCloseTo(0.75);
    expect(s.exact).toBe(true);
  });

  it('falls back to the Wound State for other fighters', () => {
    expect(tokenStatus({ woundState: 'Unhurt', dead: false, conditions: [], injuries: [] }).ratio).toBe(1);
    expect(tokenStatus({ woundState: 'Seriously', dead: false, conditions: [], injuries: [] }).ratio).toBeLessThan(0.5);
    expect(tokenStatus({ woundState: 'Mortally', dead: false, conditions: [], injuries: [] }).exact).toBe(false);
  });

  it('empties the bar of the dead and lists conditions and injuries as badges', () => {
    const s = tokenStatus({ woundState: 'Mortally', dead: true, conditions: ['Prone', 'OnFire:fire-strong'], injuries: ['Broken Arm'] });
    expect(s.ratio).toBe(0);
    expect(s.badges).toEqual(['Prone', 'OnFire', 'Broken Arm']);
  });
});

describe('initials', () => {
  it('takes the first letters of up to two words', () => {
    expect(initials('Johnny Silverhand')).toBe('JS');
    expect(initials('ganger #2')).toBe('G2');
    expect(initials('  ')).toBe('?');
  });
});

describe('damageFormula', () => {
  it('keeps the dice of a weapon damage and drops the notes', () => {
    expect(damageFormula('3d6 (Shotgun Shell)')).toBe('3d6');
    expect(damageFormula('2d6+1')).toBe('2d6+1');
    expect(damageFormula('5d6')).toBe('5d6');
    expect(damageFormula('special')).toBeNull();
  });
});

describe('damageCommand', () => {
  it('carries the Critical Injury of the roll to the combat table', () => {
    const roll = { formula: '3d6', kind: 'Damage' as const, terms: [], total: 17, critical: null, criticalInjury: true };
    expect(damageCommand(roll, 'C2', 'Head')).toEqual({ path: 'combatants/C2/damage', body: { amount: 17, location: 'Head', critical: true } });
    expect(damageCommand({ ...roll, criticalInjury: false }, 'C2', 'Body').body.critical).toBe(false);
  });
});
