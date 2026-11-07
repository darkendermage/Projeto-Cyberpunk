import { describe, expect, it } from 'vitest';
import { aboveForMove, fitInitial, orderedImages, resizeFromCorner, rotationFor, snapHalf, type ImgRect } from './images';

const rect = (change: Partial<ImgRect> = {}): ImgRect => ({ x: 10, y: 10, width: 4, height: 2, rotation: 0, ...change });
const close = (a: number, b: number) => expect(a).toBeCloseTo(b, 6);

describe('fitInitial', () => {
  it('fits 4×4 cells keeping the proportion', () => {
    expect(fitInitial(800, 400)).toEqual({ width: 4, height: 2 });
    expect(fitInitial(300, 600)).toEqual({ width: 2, height: 4 });
  });
  it('never goes under a quarter cell', () => expect(fitInitial(10000, 10).height).toBe(0.25));
});

describe('resizeFromCorner', () => {
  it('drags the south-east corner and keeps the north-west one', () => {
    // rect spans 8..12 × 9..11; drag SE to (14, 12) freely
    const r = resizeFromCorner(rect(), 'se', { x: 14, y: 12 }, false);
    expect(r).toMatchObject({ width: 6, height: 3, x: 11, y: 10.5 });
  });

  it('keeps the proportion when asked', () => {
    const r = resizeFromCorner(rect(), 'se', { x: 16, y: 11 }, true);
    expect(r.width / r.height).toBeCloseTo(2);
    expect(r.width).toBeCloseTo(8);
  });

  it('keeps the opposite corner on a rotated image', () => {
    const r0 = rect({ rotation: 90 });
    // Rotated 90°, the NW corner (local -2,-1) sits at world (11, 8).
    const nwBefore = { x: 10 + 1, y: 10 - 2 };
    const r = resizeFromCorner(r0, 'se', { x: 7, y: 15 }, false);
    const rad = (r.rotation * Math.PI) / 180;
    const local = { x: -r.width / 2, y: -r.height / 2 };
    close(r.x + local.x * Math.cos(rad) - local.y * Math.sin(rad), nwBefore.x);
    close(r.y + local.x * Math.sin(rad) + local.y * Math.cos(rad), nwBefore.y);
  });

  it('clamps to a quarter cell', () => {
    const r = resizeFromCorner(rect(), 'se', { x: 8, y: 9 }, false);
    expect(r.width).toBe(0.25);
    expect(r.height).toBe(0.25);
  });
});

describe('rotationFor', () => {
  it('points the handle at the pointer in 15° steps', () => {
    expect(rotationFor({ x: 0, y: 0 }, { x: 0, y: -5 }, false)).toBe(0);
    expect(rotationFor({ x: 0, y: 0 }, { x: 5, y: 0 }, false)).toBe(90);
    expect(rotationFor({ x: 0, y: 0 }, { x: 5, y: -4.6 }, false)).toBe(45);
  });
  it('is free with Shift', () => expect(rotationFor({ x: 0, y: 0 }, { x: 5, y: -4 }, true)).toBeCloseTo(51.34, 1));
});

describe('stack', () => {
  const images = [{ id: 'a', order: 0 }, { id: 'b', order: 2 }, { id: 'c', order: 1 }];

  it('lists the top first', () => expect(orderedImages(images).map((i) => i.id)).toEqual(['b', 'c', 'a']));

  it('tells where a move up or down goes', () => {
    // bottom → top: a, c, b
    expect(aboveForMove(images, 'a', 'up')).toBe('b'); // a goes under b, over c
    expect(aboveForMove(images, 'c', 'up')).toBeNull(); // c to the top
    expect(aboveForMove(images, 'b', 'up')).toBeUndefined(); // already on top
    expect(aboveForMove(images, 'b', 'down')).toBe('c'); // b under c
    expect(aboveForMove(images, 'a', 'down')).toBeUndefined(); // already at the bottom
  });
});

describe('snapHalf', () => {
  it('snaps to half cells', () => {
    expect(snapHalf(3.3)).toBe(3.5);
    expect(snapHalf(3.2)).toBe(3);
  });
});
