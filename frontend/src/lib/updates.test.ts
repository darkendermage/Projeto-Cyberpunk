import { describe, expect, it } from 'vitest';
import type { UpdateInfo } from '../api/types';
import { shouldOfferUpdate, waitForNewVersion } from './updates';

const info: UpdateInfo = {
  current: '1.0.0', latest: '1.1.0', available: true, notes: null, releaseUrl: null, canApply: true,
};

describe('shouldOfferUpdate', () => {
  it('offers a newer release the executable can install', () => {
    expect(shouldOfferUpdate(info, null)).toBe(true);
  });

  it('hides the banner outside the executable, when up to date or when dismissed', () => {
    expect(shouldOfferUpdate(undefined, null)).toBe(false);
    expect(shouldOfferUpdate({ ...info, canApply: false }, null)).toBe(false);
    expect(shouldOfferUpdate({ ...info, available: false }, null)).toBe(false);
    expect(shouldOfferUpdate(info, '1.1.0')).toBe(false);
  });

  it('offers again when a release newer than the dismissed one appears', () => {
    expect(shouldOfferUpdate({ ...info, latest: '1.2.0' }, '1.1.0')).toBe(true);
  });
});

describe('waitForNewVersion', () => {
  const sleep = () => Promise.resolve();

  it('keeps polling through the restart until the version changes', async () => {
    const answers: (string | Error)[] = ['1.0.0', new Error('offline'), new Error('offline'), '1.1.0'];
    const health = async () => {
      const next = answers.shift()!;
      if (next instanceof Error) throw next;
      return { status: 'online', version: next };
    };
    await expect(waitForNewVersion('1.0.0', health, { sleep })).resolves.toBe(true);
    expect(answers).toHaveLength(0);
  });

  it('gives up after the timeout', async () => {
    const health = async () => ({ status: 'online', version: '1.0.0' });
    await expect(waitForNewVersion('1.0.0', health, { sleep, intervalMs: 10, timeoutMs: 50 })).resolves.toBe(false);
  });
});
