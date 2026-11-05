import { describe, expect, it } from 'vitest';
import { DEFAULT_GRID } from './grid';
import { initialDraft, TEMPLATE_PRESETS, templateShape } from './templates';

const grid = { ...DEFAULT_GRID, sizePx: 50, offsetX: 0, offsetY: 0, metersPerCell: 2 };
const base = { id: 'A1', color: '#ff0000', label: null, locked: false, ownerSeatId: null };

describe('templateShape', () => {
  it('draws a circle whose radius is the size in meters', () => {
    expect(templateShape(grid, { ...base, shape: 'Circle', x: 2, y: 2, sizeMeters: 4, angle: 0 }))
      .toEqual({ kind: 'circle', cx: 100, cy: 100, r: 100 });
  });

  it('draws a square of the given side, centered on its origin', () => {
    const s = templateShape(grid, { ...base, shape: 'Square', x: 4, y: 4, sizeMeters: 10, angle: 0 });
    expect(s.kind).toBe('poly');
    if (s.kind !== 'poly') return;
    const xs = s.points.filter((_, i) => i % 2 === 0);
    expect(Math.min(...xs)).toBeCloseTo(75, 5);
    expect(Math.max(...xs)).toBeCloseTo(325, 5);
  });

  it('points a line along its angle, one cell wide', () => {
    const s = templateShape(grid, { ...base, shape: 'Line', x: 0, y: 0, sizeMeters: 10, angle: 90 });
    if (s.kind !== 'poly') throw new Error('poly expected');
    const ys = s.points.filter((_, i) => i % 2 === 1);
    const xs = s.points.filter((_, i) => i % 2 === 0);
    expect(Math.max(...ys)).toBeCloseTo(250, 5);
    expect(Math.max(...xs) - Math.min(...xs)).toBeCloseTo(50, 5);
  });

  it('opens a cone from its apex, as wide as it is long at the end', () => {
    const s = templateShape(grid, { ...base, shape: 'Cone', x: 0, y: 0, sizeMeters: 6, angle: 0 });
    if (s.kind !== 'poly') throw new Error('poly expected');
    expect(s.points.slice(0, 2)).toEqual([0, 0]);
    const ys = s.points.filter((_, i) => i % 2 === 1);
    expect(Math.max(...ys) - Math.min(...ys)).toBeCloseTo(150, 0);
  });
});

describe('presets', () => {
  it('cite the book or say they are a table convention', () => {
    for (const p of TEMPLATE_PRESETS) expect(p.source.length).toBeGreaterThan(0);
    expect(TEMPLATE_PRESETS.find((p) => p.id === 'explosive')).toMatchObject({ shape: 'Square', sizeMeters: 10 });
  });
});

describe('the area tool draft', () => {
  it('starts as the book\'s explosion, a 10×10 m square', () => {
    expect(initialDraft(null)).toMatchObject({ shape: 'Square', sizeMeters: 10 });
  });

  it('comes back as it was last used', () => {
    expect(initialDraft(JSON.stringify({ shape: 'Cone', sizeMeters: 6, color: '#00f0ff' }))).toEqual({ shape: 'Cone', sizeMeters: 6, color: '#00f0ff' });
  });

  it('ignores a stored draft it cannot trust', () => {
    expect(initialDraft('{"shape":"Hexagon","sizeMeters":-3}')).toMatchObject({ shape: 'Square', sizeMeters: 10 });
    expect(initialDraft('not json')).toMatchObject({ shape: 'Square' });
  });
});
