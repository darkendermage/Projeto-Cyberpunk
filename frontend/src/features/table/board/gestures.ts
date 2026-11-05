import type { Point } from '../grid';

/** Holding the pointer still this long pings the spot. */
export const HOLD_MS = 500;
/** Moving more than this (screen pixels) turns a press into a drag. */
export const MOVE_TOLERANCE = 6;

export type Press = 'click' | 'drag' | 'hold';

/** What a press was, from how far the pointer went and how long it stayed down. */
export function classifyPress(movedPx: number, heldMs: number): Press {
  if (movedPx > MOVE_TOLERANCE) return 'drag';
  return heldMs >= HOLD_MS ? 'hold' : 'click';
}

export interface Rect { x: number; y: number; width: number; height: number }

/** The rectangle between two corners, whichever way it was drawn. */
export function rectFrom(a: Point, b: Point): Rect {
  return { x: Math.min(a.x, b.x), y: Math.min(a.y, b.y), width: Math.abs(a.x - b.x), height: Math.abs(a.y - b.y) };
}

export function inRect(p: Point, r: Rect): boolean {
  return p.x >= r.x && p.x <= r.x + r.width && p.y >= r.y && p.y <= r.y + r.height;
}

/** A click picks one thing; with Shift it adds it to the selection, or takes it out if it was in. */
export function toggleSelection(selected: readonly string[], id: string, additive: boolean): string[] {
  if (!additive) return [id];
  return selected.includes(id) ? selected.filter((x) => x !== id) : [...selected, id];
}
