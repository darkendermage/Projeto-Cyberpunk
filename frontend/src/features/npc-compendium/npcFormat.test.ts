import { describe, expect, it } from 'vitest';
import { formatPrinted } from './npcFormat';

describe('formatPrinted', () => {
  it('shows a dash for null and undefined', () => {
    expect(formatPrinted(null)).toBe('—');
    expect(formatPrinted(undefined)).toBe('—');
  });
  it('keeps zero', () => {
    expect(formatPrinted(0)).toBe('0');
  });
  it('prints numbers', () => {
    expect(formatPrinted(17)).toBe('17');
  });
});
