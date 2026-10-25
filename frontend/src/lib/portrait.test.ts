import { describe, expect, it } from 'vitest';
import { canUploadAsIs, fitSize, PORTRAIT_MAX_BYTES } from './portrait';

describe('fitSize', () => {
  it('shrinks the longest side to 1024 and keeps the proportion', () => {
    expect(fitSize(1536, 1024)).toEqual({ width: 1024, height: 683 });
    expect(fitSize(1000, 3000)).toEqual({ width: 341, height: 1024 });
  });

  it('never enlarges a small picture', () => {
    expect(fitSize(640, 480)).toEqual({ width: 640, height: 480 });
  });
});

describe('canUploadAsIs', () => {
  it('sends small pictures untouched', () => {
    expect(canUploadAsIs({ type: 'image/png', size: 300_000 }, 800, 600)).toBe(true);
  });

  it('re-encodes big files or big pictures', () => {
    expect(canUploadAsIs({ type: 'image/png', size: PORTRAIT_MAX_BYTES + 1 }, 800, 600)).toBe(false);
    expect(canUploadAsIs({ type: 'image/jpeg', size: 500_000 }, 1536, 1024)).toBe(false);
  });

  it('keeps small GIFs whatever their size in pixels, to keep the animation', () => {
    expect(canUploadAsIs({ type: 'image/gif', size: 900_000 }, 1600, 900)).toBe(true);
  });
});
