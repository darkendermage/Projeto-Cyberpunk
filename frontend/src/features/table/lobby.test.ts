import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { claimErrorMessage, forgetSeat, isRefusedKey, rememberSeat, savedSeat } from './lobby';

/** A browser's storage, for the node test environment. */
function memoryStorage() {
  const data = new Map<string, string>();
  return {
    getItem: (k: string) => data.get(k) ?? null,
    setItem: (k: string, v: string) => { data.set(k, v); },
    removeItem: (k: string) => { data.delete(k); },
  };
}

const blocked = () => { throw new Error('blocked'); };

describe('the seat this device holds', () => {
  beforeEach(() => { vi.stubGlobal('localStorage', memoryStorage()); });
  afterEach(() => { vi.unstubAllGlobals(); });

  it('is remembered and forgotten', () => {
    expect(savedSeat()).toBeNull();
    rememberSeat('abc');
    expect(savedSeat()).toBe('abc');
    forgetSeat();
    expect(savedSeat()).toBeNull();
  });

  it('reads as none when the browser blocks storage', () => {
    vi.stubGlobal('localStorage', { getItem: blocked, setItem: blocked, removeItem: blocked });
    expect(() => rememberSeat('abc')).not.toThrow();
    expect(() => forgetSeat()).not.toThrow();
    expect(savedSeat()).toBeNull();
  });
});

describe('claimErrorMessage', () => {
  it('tells a taken seat apart from a closed entrance', () => {
    const taken = claimErrorMessage(new ApiError(409, { title: 'That character was just taken.' }));
    expect(taken.closed).toBe(false);
    expect(taken.text).toMatch(/escolhido|taken/);
    expect(claimErrorMessage(new ApiError(404, { title: 'closed' })).closed).toBe(true);
    expect(claimErrorMessage(new Error('boom')).closed).toBe(false);
  });
});

describe('isRefusedKey', () => {
  it('drops a saved key only when the table refuses it, not when the connection fails', () => {
    expect(isRefusedKey(new ApiError(401, { title: 'no' }))).toBe(true);
    expect(isRefusedKey(new ApiError(502, { title: 'Bad Gateway' }))).toBe(false);
    expect(isRefusedKey(new ApiError(0, { title: 'network' }))).toBe(false);
    expect(isRefusedKey(new TypeError('Failed to fetch'))).toBe(false);
  });
});
