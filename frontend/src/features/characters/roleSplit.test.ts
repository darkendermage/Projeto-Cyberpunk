import { describe, expect, it } from 'vitest';
import { awarenessEffects } from './roleSplit';

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
