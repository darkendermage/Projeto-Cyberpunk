import { describe, expect, it } from 'vitest';
import { classifyPress, HOLD_MS, inRect, MOVE_TOLERANCE, rectFrom, toggleSelection } from './gestures';

describe('classifyPress', () => {
  it('tells a click, a drag and a hold apart', () => {
    expect(classifyPress(0, 100)).toBe('click');
    expect(classifyPress(MOVE_TOLERANCE + 1, 100)).toBe('drag');
    expect(classifyPress(2, HOLD_MS)).toBe('hold');
    expect(classifyPress(MOVE_TOLERANCE + 1, HOLD_MS + 300)).toBe('drag');
  });
});

describe('the lasso', () => {
  it('works whichever way it is drawn', () => {
    expect(rectFrom({ x: 5, y: 1 }, { x: 2, y: 4 })).toEqual({ x: 2, y: 1, width: 3, height: 3 });
  });

  it('catches the points inside it, edges included', () => {
    const r = rectFrom({ x: 0, y: 0 }, { x: 4, y: 4 });
    expect(inRect({ x: 2, y: 2 }, r)).toBe(true);
    expect(inRect({ x: 4, y: 0 }, r)).toBe(true);
    expect(inRect({ x: 4.1, y: 2 }, r)).toBe(false);
  });
});

describe('toggleSelection', () => {
  it('replaces the selection, or adds and removes with Shift', () => {
    expect(toggleSelection(['a', 'b'], 'c', false)).toEqual(['c']);
    expect(toggleSelection(['a'], 'b', true)).toEqual(['a', 'b']);
    expect(toggleSelection(['a', 'b'], 'a', true)).toEqual(['b']);
  });
});
