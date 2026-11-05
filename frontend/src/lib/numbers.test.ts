import { describe, expect, it } from 'vitest';
import { parseDecimal, parseWholeNumber } from './numbers';

describe('parseWholeNumber', () => {
  it('waits while the field is empty instead of jumping to the minimum', () => {
    expect(parseWholeNumber('', 2, 10)).toBeNull();
    expect(parseWholeNumber('  ', 1, 99)).toBeNull();
  });

  it('keeps typed values within the limits', () => {
    expect(parseWholeNumber('5', 0, 10)).toBe(5);
    expect(parseWholeNumber('25', 0, 10)).toBe(10);
    expect(parseWholeNumber('-3', 0, 10)).toBe(0);
    expect(parseWholeNumber('4.6', 0, 10)).toBe(5);
  });
});

describe('parseDecimal', () => {
  it('waits while the field is empty or holds something that is not a number yet', () => {
    expect(parseDecimal('', 0, 10)).toBeNull();
    expect(parseDecimal('-', -5, 10)).toBeNull();
    expect(parseDecimal('abc', 0, 10)).toBeNull();
  });

  it('keeps decimals and the limits', () => {
    expect(parseDecimal('2.5', 0, 10)).toBe(2.5);
    expect(parseDecimal('2,5', 0, 10)).toBe(2.5);
    expect(parseDecimal('6', 0, 5)).toBe(5);
    expect(parseDecimal('0.1', 0.5, 5)).toBe(0.5);
  });
});
