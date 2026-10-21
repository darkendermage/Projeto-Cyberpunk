import { describe, expect, it } from 'vitest';
import { parseWholeNumber } from './numbers';

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
