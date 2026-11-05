import { describe, expect, it } from 'vitest';
import { healing } from './healing';

const health = (currentHp: number, maxHp = 40, woundState = 'Lightly', bodyForDerived = 6) => ({ currentHp, maxHp, woundState, bodyForDerived });

describe('healing', () => {
  it('heals up to the missing HP, never past the max', () => {
    expect(healing(health(30), false)).toMatchObject({ missing: 10, quick: [1, 5], canHeal: true });
    expect(healing(health(37), false).quick).toEqual([1]);
    expect(healing(health(40, 40, 'Unhurt'), true)).toMatchObject({ missing: 0, quick: [], canHeal: false, canRest: false });
  });

  it('a day of rest heals BODY once stabilized (CP:R p.222), capped at the missing HP', () => {
    expect(healing(health(20), false)).toMatchObject({ canRest: false, rest: 6 });
    expect(healing(health(20), true)).toMatchObject({ canRest: true, rest: 6 });
    expect(healing(health(37), true).rest).toBe(3);
  });

  it('a Mortally Wounded character must be stabilized before healing', () => {
    expect(healing(health(0, 40, 'Mortally'), false)).toMatchObject({ canHeal: false, canRest: false });
  });
});
