import type { TableMusic } from '../../../api/tableTypes';

/** The GM's music state in the query cache. */
export const musicKey = ['table', 'music'] as const;

/** How far from the table's position a player may drift (ads, a slow network) before it is put back. */
export const DRIFT_SECONDS = 2;

/** Where the track should be now: the stored position, plus the time since it was stored when it is playing. */
export function expectedPosition(m: TableMusic, nowMs: number): number {
  return m.positionSeconds + (m.playing ? Math.max(0, (nowMs - Date.parse(m.updatedAt)) / 1000) : 0);
}

/** Whether the player is far enough from where it should be to seek. */
export function needsSeek(actual: number, expected: number): boolean {
  return Math.abs(actual - expected) > DRIFT_SECONDS;
}

/** YouTube player states (IFrame API): -1 unstarted, 0 ended, 1 playing, 2 paused, 3 buffering, 5 cued. */
export const YT_UNSTARTED = -1;
export const YT_PLAYING = 1;
export const YT_PAUSED = 2;
export const YT_BUFFERING = 3;
export const YT_CUED = 5;

/**
 * What a screen's player must do to match the table: pause when the table is paused but the player plays or is still
 * loading (a pause that arrived mid-load is ignored by YouTube), play when the table plays and this person enabled sound.
 */
export function musicAction(state: number, music: TableMusic, unlocked: boolean): 'play' | 'pause' | 'none' {
  const want = music.playing && unlocked;
  if (!want && (state === YT_PLAYING || state === YT_BUFFERING)) return 'pause';
  if (want && (state === YT_PAUSED || state === YT_CUED || state === YT_UNSTARTED)) return 'play';
  return 'none';
}
