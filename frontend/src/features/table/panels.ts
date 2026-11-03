/** A side panel's width limits, in pixels. */
export interface PanelLimits { min: number; max: number; initial: number }

/** The GM's screen: scenes on the left, the tabs on the right, the map in between. */
export const GM_PANELS = {
  left: { min: 150, max: 420, initial: 190 },
  right: { min: 260, max: 680, initial: 320 },
} satisfies Record<string, PanelLimits>;

/** The player's screen: the map and the tabs on the right. */
export const PLAYER_PANELS = {
  right: { min: 260, max: 640, initial: 340 },
} satisfies Record<string, PanelLimits>;

export interface GmPanels { left: number; right: number; leftOpen: boolean; rightOpen: boolean }
export interface PlayerPanels { right: number; rightOpen: boolean }

/** The width of the grab handles between the panels and the map. */
export const HANDLE = 10;

export function clampPanel(limits: PanelLimits, width: number): number {
  return Math.round(Math.min(limits.max, Math.max(limits.min, width)));
}

export function gmColumns(p: GmPanels): string {
  return `${p.leftOpen ? p.left : 0}px ${HANDLE}px minmax(0, 1fr) ${HANDLE}px ${p.rightOpen ? p.right : 0}px`;
}

export function playerColumns(p: PlayerPanels): string {
  return `minmax(0, 1fr) ${HANDLE}px ${p.rightOpen ? p.right : 0}px`;
}

/** The sizes this browser remembers (storage can be blocked: the defaults then apply). */
export function readPanels<L extends Record<string, PanelLimits>>(key: string, limits: L): { [K in keyof L]: number } & { [K in keyof L as `${string & K}Open`]: boolean } {
  let stored: Record<string, unknown>;
  try { stored = JSON.parse(localStorage.getItem(key) ?? '{}') ?? {}; } catch { stored = {}; }
  const out: Record<string, number | boolean> = {};
  for (const [side, l] of Object.entries(limits)) {
    const width = stored[side];
    out[side] = typeof width === 'number' && Number.isFinite(width) ? clampPanel(l, width) : l.initial;
    out[`${side}Open`] = stored[`${side}Open`] !== false;
  }
  return out as ReturnType<typeof readPanels<L>>;
}

export function savePanels(key: string, panels: object) {
  try { localStorage.setItem(key, JSON.stringify(panels)); } catch { /* not remembered */ }
}
