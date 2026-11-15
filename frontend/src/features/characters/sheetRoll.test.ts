import { describe, expect, it } from 'vitest';
import { skillFormula } from './sheetRoll';

describe('skillFormula', () => {
  it('adds the skill BASE to 1d10, keeping the sign of a negative one', () => {
    expect(skillFormula(12)).toBe('1d10+12');
    expect(skillFormula(0)).toBe('1d10+0');
    expect(skillFormula(-2)).toBe('1d10-2');
  });
});
