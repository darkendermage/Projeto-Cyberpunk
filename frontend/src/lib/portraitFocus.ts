import type { CSSProperties } from 'react';

/** How a portrait is framed: the focal point in % of the picture (0–100) and the zoom (1–3). */
export interface PortraitFocus { x: number; y: number; zoom: number }

const clamp = (n: number, min: number, max: number) => Math.min(max, Math.max(min, n));

/** The CSS of an <img> with object-fit: cover inside a frame with overflow: hidden. */
export function focusStyle(f: PortraitFocus | null | undefined): Pick<CSSProperties, 'objectPosition' | 'transform' | 'transformOrigin'> {
  const x = f?.x ?? 50;
  const y = f?.y ?? 50;
  const zoomed = f != null && f.zoom > 1;
  return { objectPosition: `${x}% ${y}%`, transform: zoomed ? `scale(${f.zoom})` : undefined, transformOrigin: zoomed ? `${x}% ${y}%` : undefined };
}

/**
 * A texture covering a box (the token's shape), framed on the focus: the scale, and the sprite's center relative to the
 * box's center (anchor 0.5). The sprite always covers the box: the focus moves toward the center only as far as that allows.
 */
export function spriteFit(texW: number, texH: number, boxW: number, boxH: number, f: PortraitFocus | null | undefined): { scale: number; x: number; y: number } {
  const scale = Math.max(boxW / texW, boxH / texH) * (f?.zoom ?? 1);
  const w = texW * scale;
  const h = texH * scale;
  const axis = (size: number, box: number, pct: number) => {
    // Left (top) edge of the sprite in box coordinates, with the focus as near the middle as covering allows.
    const edge = clamp(box / 2 - (pct / 100) * size, box - size, 0);
    return edge + size / 2 - box / 2 + 0; // + 0 turns -0 into 0
  };
  return { scale, x: axis(w, boxW, f?.x ?? 50), y: axis(h, boxH, f?.y ?? 50) };
}

/** Dragging the picture by (dx, dy) px in a frame of (w, h) px: the picture follows the pointer. */
export function moveFocus(f: PortraitFocus, dx: number, dy: number, w: number, h: number): PortraitFocus {
  return {
    x: clamp(f.x - (dx / w) * 100 / f.zoom, 0, 100),
    y: clamp(f.y - (dy / h) * 100 / f.zoom, 0, 100),
    zoom: f.zoom,
  };
}
