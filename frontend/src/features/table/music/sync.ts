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
