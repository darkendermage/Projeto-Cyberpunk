import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clampPanel, GM_PANELS, gmColumns, playerColumns, PLAYER_PANELS, readPanels, savePanels } from './panels';

function memoryStorage() {
  const data = new Map<string, string>();
  return { getItem: (k: string) => data.get(k) ?? null, setItem: (k: string, v: string) => { data.set(k, v); }, removeItem: (k: string) => { data.delete(k); } };
}

describe('clampPanel', () => {
  it('keeps a panel between its limits', () => {
    expect(clampPanel(GM_PANELS.left, 20)).toBe(GM_PANELS.left.min);
    expect(clampPanel(GM_PANELS.right, 5000)).toBe(GM_PANELS.right.max);
    expect(clampPanel(GM_PANELS.right, 333.6)).toBe(334);
  });
});

describe('the columns of the screens', () => {
  it('give the map whatever the panels leave, and nothing to a hidden panel', () => {
    expect(gmColumns({ left: 200, right: 320, leftOpen: true, rightOpen: true })).toBe('200px 10px minmax(0, 1fr) 10px 320px');
    expect(gmColumns({ left: 200, right: 320, leftOpen: false, rightOpen: true })).toBe('0px 10px minmax(0, 1fr) 10px 320px');
    expect(playerColumns({ right: 360, rightOpen: false })).toBe('minmax(0, 1fr) 10px 0px');
  });
});

describe('remembered sizes', () => {
  beforeEach(() => { vi.stubGlobal('localStorage', memoryStorage()); });
  afterEach(() => { vi.unstubAllGlobals(); });

  it('come back as saved, within the limits', () => {
    savePanels('k', { left: 999, right: 300, leftOpen: false, rightOpen: true });
    expect(readPanels('k', GM_PANELS)).toEqual({ left: GM_PANELS.left.max, right: 300, leftOpen: false, rightOpen: true });
  });

  it('fall back to the defaults when nothing or garbage is stored', () => {
    expect(readPanels('none', GM_PANELS)).toEqual({ left: GM_PANELS.left.initial, right: GM_PANELS.right.initial, leftOpen: true, rightOpen: true });
    localStorage.setItem('bad', '{not json');
    expect(readPanels('bad', PLAYER_PANELS).right).toBe(PLAYER_PANELS.right.initial);
  });
});
