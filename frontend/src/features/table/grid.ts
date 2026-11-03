import type { SceneGrid } from '../../api/tableTypes';

export const DEFAULT_GRID: SceneGrid = {
  type: 'Square', sizePx: 70, offsetX: 0, offsetY: 0, metersPerCell: 2, color: '#00f0ff', opacity: 0.35, snap: true,
};

export interface Point { x: number; y: number }

/** Cells (the scene's unit) to image pixels, through the grid offset. */
export const cellToPx = (g: SceneGrid, x: number, y: number): Point => ({ x: g.offsetX + x * g.sizePx, y: g.offsetY + y * g.sizePx });

export const pxToCell = (g: SceneGrid, x: number, y: number): Point => ({ x: (x - g.offsetX) / g.sizePx, y: (y - g.offsetY) / g.sizePx });

/** Where a dropped token's corner lands: whole cells, or half cells for tokens smaller than one. */
export const snapCell = (value: number, size: number): number => (size < 1 ? Math.round(value * 2) / 2 : Math.round(value));

export interface Distance {
  /** Squares moved, a diagonal counting as one; null without a grid. */
  squares: number | null;
  meters: number;
  straightMeters: number;
}

/**
 * The ruler: on a square grid it counts the squares between the cells the two points are in, a diagonal step counting
 * as one square (a table convention: the Core gives movement in squares of 2 m, p.168, but no diagonal rule), and
 * shows the straight line too.
 */
export function gridDistance(g: SceneGrid, a: Point, b: Point): Distance {
  const straightMeters = Math.hypot(b.x - a.x, b.y - a.y) * g.metersPerCell;
  if (g.type === 'None') return { squares: null, meters: straightMeters, straightMeters };
  const squares = Math.max(Math.abs(Math.floor(b.x) - Math.floor(a.x)), Math.abs(Math.floor(b.y) - Math.floor(a.y)));
  return { squares, meters: squares * g.metersPerCell, straightMeters };
}

export function formatMeters(meters: number, lang: 'pt' | 'en'): string {
  const text = meters < 10 ? (Math.round(meters * 10) / 10).toString() : Math.round(meters).toString();
  return `${lang === 'pt' ? text.replace('.', ',') : text} m`;
}
