import type { HealthInfo, UpdateInfo } from '../api/types';

/** The banner shows only when the executable can install the release and the user has not dismissed that version. */
export function shouldOfferUpdate(info: UpdateInfo | undefined, dismissedVersion: string | null): info is UpdateInfo & { latest: string } {
  return Boolean(info?.available && info.canApply && info.latest && info.latest !== dismissedVersion);
}

interface WaitOptions {
  intervalMs?: number;
  timeoutMs?: number;
  sleep?: (ms: number) => Promise<void>;
}

/**
 * After POST /app/update the old process stops and the new one starts on the same port.
 * Polls /health until it answers with another version; false when it never comes back.
 */
export async function waitForNewVersion(
  previous: string,
  health: () => Promise<HealthInfo>,
  { intervalMs = 1000, timeoutMs = 60_000, sleep = (ms) => new Promise((r) => setTimeout(r, ms)) }: WaitOptions = {},
): Promise<boolean> {
  for (let waited = 0; waited < timeoutMs; waited += intervalMs) {
    await sleep(intervalMs);
    try {
      if ((await health()).version !== previous) return true;
    } catch {
      /* restarting: nothing listening yet */
    }
  }
  return false;
}
