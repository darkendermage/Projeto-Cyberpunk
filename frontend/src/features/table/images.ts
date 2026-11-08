import type { ImageInput, SceneImageView } from '../../api/tableTypes';
import type { Point } from './grid';

/** An image's place on the map: centre and size in cells, rotation in degrees. */
export interface ImgRect { x: number; y: number; width: number; height: number; rotation: number }

export type Corner = 'nw' | 'ne' | 'se' | 'sw';

export const MIN_SIZE = 0.25;
export const MAX_SIZE = 200;

const clampSize = (n: number) => Math.min(MAX_SIZE, Math.max(MIN_SIZE, n));
const round = (n: number) => Math.round(n * 1000) / 1000;

/** A new image's size: the picture's proportion, fitting a square of `maxCells` cells. */
export function fitInitial(texW: number, texH: number, maxCells = 4): { width: number; height: number } {
  const scale = maxCells / Math.max(texW, texH);
  return { width: round(clampSize(texW * scale)), height: round(clampSize(texH * scale)) };
}

const rotate = (p: Point, deg: number): Point => {
  const r = (deg * Math.PI) / 180;
  return { x: p.x * Math.cos(r) - p.y * Math.sin(r), y: p.x * Math.sin(r) + p.y * Math.cos(r) };
};

const SIGN: Record<Corner, Point> = { nw: { x: -1, y: -1 }, ne: { x: 1, y: -1 }, se: { x: 1, y: 1 }, sw: { x: -1, y: 1 } };

// Drag a corner while the opposite one stays put (rotation included). keepRatio keeps width:height.
export function resizeFromCorner(r: ImgRect, corner: Corner, point: Point, keepRatio: boolean): ImgRect {
  const s = SIGN[corner];
  // Work in the image's own axes: the pointer relative to the centre, unrotated.
  const local = rotate({ x: point.x - r.x, y: point.y - r.y }, -r.rotation);
  const opposite = { x: (-s.x * r.width) / 2, y: (-s.y * r.height) / 2 };
  let width = Math.abs(local.x - opposite.x);
  let height = Math.abs(local.y - opposite.y);
  if (keepRatio) {
    const k = Math.max(width / r.width, height / r.height);
    width = r.width * k;
    height = r.height * k;
  }
  width = clampSize(width);
  height = clampSize(height);
  const centreLocal = { x: opposite.x + (s.x * width) / 2, y: opposite.y + (s.y * height) / 2 };
  const centre = rotate(centreLocal, r.rotation);
  return { ...r, x: round(r.x + centre.x), y: round(r.y + centre.y), width: round(width), height: round(height) };
}

/** The rotation that points the handle above the image at `point`: 15° steps, free with Shift. */
export function rotationFor(centre: Point, point: Point, free: boolean): number {
  const deg = (Math.atan2(point.x - centre.x, centre.y - point.y) * 180) / Math.PI;
  const angle = free ? deg : Math.round(deg / 15) * 15;
  return round(((angle % 360) + 360) % 360);
}

/** Half-cell snapping of a centre (Alt drops it). */
export const snapHalf = (n: number) => Math.round(n * 2) / 2;

/** The images top first, as the layers list shows them. */
export function orderedImages<T extends { order: number }>(images: readonly T[]): T[] {
  return [...images].sort((a, b) => b.order - a.order);
}

// The image it'll sit right under; null = top, undefined = can't move that way.
export function aboveForMove(images: readonly { id: string; order: number }[], id: string, dir: 'up' | 'down'): string | null | undefined {
  const stack = [...images].sort((a, b) => a.order - b.order).map((i) => i.id); // bottom → top
  const at = stack.indexOf(id);
  if (at < 0) return undefined;
  if (dir === 'up') return at === stack.length - 1 ? undefined : stack[at + 2] ?? null;
  return at === 0 ? undefined : stack[at - 1];
}

/** The whole image as the API takes it, with `change` applied. */
export const inputOf = (i: SceneImageView, change: Partial<ImageInput> = {}): ImageInput => ({
  name: i.name, x: i.x, y: i.y, width: i.width, height: i.height, rotation: i.rotation, opacity: i.opacity, locked: i.locked, hidden: i.hidden, ...change,
});
