import { describe, expect, it } from 'vitest';
import { focusStyle, moveFocus, spriteFit } from './portraitFocus';

describe('spriteFit', () => {
  it('covers the box and moves toward the focus', () => {
    // 200×100 into 50×50: scaled by 0.5 to 100×50; focus on the left edge -> the sprite's left edge on the box's.
    const fit = spriteFit(200, 100, 50, 50, { x: 0, y: 50, zoom: 1 });
    expect(fit.scale).toBe(0.5);
    expect(fit.x).toBe(25);
    expect(fit.y).toBe(0);
  });

  it('centers without a focus', () => {
    expect(spriteFit(200, 100, 50, 50, null)).toEqual({ scale: 0.5, x: 0, y: 0 });
  });

  it('zooms in and never shows the background', () => {
    const fit = spriteFit(100, 100, 50, 50, { x: 100, y: 100, zoom: 2 });
    expect(fit.scale).toBe(1);
    // 100×100 shown in 50×50 with the bottom-right corner in focus: the sprite's corner sits on the box's corner.
    expect(fit.x).toBe(-25);
    expect(fit.y).toBe(-25);
  });
});

describe('focusStyle', () => {
  it('defaults to center', () => {
    expect(focusStyle(null)).toEqual({ objectPosition: '50% 50%', transform: undefined, transformOrigin: undefined });
  });

  it('places and zooms around the focus', () => {
    expect(focusStyle({ x: 20, y: 30, zoom: 1.5 })).toEqual({ objectPosition: '20% 30%', transform: 'scale(1.5)', transformOrigin: '20% 30%' });
  });
});

describe('moveFocus', () => {
  it('drags the picture: moving right shows more of the left side', () => {
    expect(moveFocus({ x: 50, y: 50, zoom: 1 }, 10, 0, 100, 100)).toEqual({ x: 40, y: 50, zoom: 1 });
  });

  it('moves less when zoomed and stays within 0–100', () => {
    expect(moveFocus({ x: 50, y: 50, zoom: 2 }, 10, 0, 100, 100).x).toBe(45);
    expect(moveFocus({ x: 2, y: 98, zoom: 1 }, 50, -50, 100, 100)).toEqual({ x: 0, y: 100, zoom: 1 });
  });
});
