import { describe, expect, it } from 'vitest';
import type { CharacterEvent } from '../../api/characterTypes';
import { lastMove } from './wallet';

const event = (over: Partial<CharacterEvent>): CharacterEvent => ({
  id: 1, kind: 'Ledger', action: 'manual-adjustment', amount: 100, before: null, after: null,
  description: 'Job pay.', isManual: true, createdAt: '2026-11-17T00:00:00Z', canUndo: false, undone: false, ...over,
});

describe('lastMove', () => {
  it('takes the newest entry of the kind', () => {
    const events = [event({ id: 3, kind: 'Progression', amount: 10 }), event({ id: 2, amount: -50, description: 'Bribe' }), event({ id: 1 })];
    expect(lastMove(events, 'Ledger')).toEqual({ amount: -50, text: 'Bribe', full: 'Bribe' });
    expect(lastMove(events, 'Progression')?.amount).toBe(10);
  });

  it('skips entries that moved nothing and undone ones', () => {
    const events = [event({ id: 3, amount: 0 }), event({ id: 2, amount: null }), event({ id: 1, undone: true })];
    expect(lastMove(events, 'Ledger')).toBeNull();
  });

  it('shortens long reasons and keeps the full one', () => {
    const long = 'Payment for the job at the Afterlife with the fixer Rogue';
    const move = lastMove([event({ description: `${long}.` })], 'Ledger')!;
    expect(move.text.length).toBeLessThanOrEqual(36);
    expect(move.text.endsWith('…')).toBe(true);
    expect(move.full).toBe(long);
  });

  it('is empty without events', () => {
    expect(lastMove(undefined, 'Ledger')).toBeNull();
  });
});
