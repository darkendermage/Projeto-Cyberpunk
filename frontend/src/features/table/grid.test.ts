import { describe, expect, it } from 'vitest';
import { DEFAULT_GRID, cellToPx, formatMeters, gridDistance, pxToCell, snapCell } from './grid';

const grid = { ...DEFAULT_GRID, sizePx: 50, offsetX: 10, offsetY: 20, metersPerCell: 2 };

describe('cells and pixels', () => {
  it('converts both ways through the grid offset', () => {
    expect(cellToPx(grid, 2, 3)).toEqual({ x: 110, y: 170 });
    expect(pxToCell(grid, 110, 170)).toEqual({ x: 2, y: 3 });
  });

  it('snaps whole tokens to whole cells and small ones to half cells', () => {
    expect(snapCell(2.4, 1)).toBe(2);
    expect(snapCell(2.6, 2)).toBe(3);
    expect(snapCell(2.3, 0.5)).toBe(2.5);
  });
});

describe('gridDistance', () => {
  it('counts squares (a diagonal counts as one) and meters', () => {
    const d = gridDistance(grid, { x: 0.5, y: 0.5 }, { x: 3.5, y: 4.5 });
    expect(d.squares).toBe(4);
    expect(d.meters).toBe(8);
    expect(d.straightMeters).toBeCloseTo(10, 5);
  });

  it('measures from the cell each point is in', () => {
    expect(gridDistance(grid, { x: 0.1, y: 0.1 }, { x: 0.9, y: 0.9 }).squares).toBe(0);
    expect(gridDistance(grid, { x: 0.9, y: 0 }, { x: 1.1, y: 0 }).squares).toBe(1);
  });

  it('uses the straight line when the scene has no grid', () => {
    const d = gridDistance({ ...grid, type: 'None' }, { x: 0, y: 0 }, { x: 3, y: 4 });
    expect(d.squares).toBeNull();
    expect(d.meters).toBeCloseTo(10, 5);
  });
});

describe('formatMeters', () => {
  it('keeps one decimal under 10 m', () => {
    expect(formatMeters(2.5, 'en')).toBe('2.5 m');
    expect(formatMeters(2.5, 'pt')).toBe('2,5 m');
    expect(formatMeters(12.34, 'en')).toBe('12 m');
  });
});
