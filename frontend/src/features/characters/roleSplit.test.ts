import { describe, expect, it } from 'vitest';
import { awarenessEffects, stepSplit } from './roleSplit';

describe('awarenessEffects', () => {
  it('turns Combat Awareness points into the effects, with the once-a-Round ones marked used', () => {
    const effects = awarenessEffects({ precisionAttack: 6, damageDeflection: 4, spotWeakness: 2, initiativeReaction: 0 }, ['damageDeflection']);

    expect(effects.map((e) => [e.key, e.value, e.used ?? null])).toEqual([
      ['precisionAttack', '+2', null],
      ['damageDeflection', '−2', true],
      ['spotWeakness', '+2', false],
    ]);
  });

  it('caps Precision at +3 and Deflection at −5, and Fumble Recovery needs 4 points', () => {
    const effects = awarenessEffects({ precisionAttack: 10, damageDeflection: 11, fumbleRecovery: 4, initiativeReaction: 3, threatDetection: 1 }, []);

    expect(effects.map((e) => [e.key, e.value])).toEqual([
      ['precisionAttack', '+3'], ['damageDeflection', '−5'], ['initiativeReaction', '+3'], ['fumbleRecovery', '✓'], ['threatDetection', '+1'],
    ]);
  });
});

describe('stepSplit', () => {
  it('moves one point at a time inside the cap and the free points', () => {
    expect(stepSplit(0, 1, { max: 4, free: 4 })).toBe(1);
    expect(stepSplit(4, 1, { max: 4, free: 0 })).toBe(4);
    expect(stepSplit(2, 1, { max: 4, free: 0 })).toBe(2);
    expect(stepSplit(0, -1, { max: 4, free: 4 })).toBe(0);
    expect(stepSplit(3, -1, { max: 4, free: 1 })).toBe(2);
  });

  it('jumps between the allowed values and stops when they do not fit', () => {
    expect(stepSplit(0, 1, { max: 4, free: 4, allowed: [0, 4] })).toBe(4);
    expect(stepSplit(0, 1, { max: 4, free: 3, allowed: [0, 4] })).toBe(0);
    expect(stepSplit(4, -1, { max: 4, free: 0, allowed: [0, 4] })).toBe(0);
  });
});
