import { describe, expect, it } from 'vitest';
import type { EncounterSummary } from '../../api/encounterTypes';
import { openEncounters } from './openEncounters';

const enc = (id: number, status: EncounterSummary['status']): EncounterSummary => ({
  id, name: `E${id}`, status, round: 1, allies: 0, enemies: 0, down: 0, updatedAt: '2026-01-01',
});

describe('openEncounters', () => {
  it('drops ended encounters and keeps the order', () => {
    expect(openEncounters([enc(3, 'Active'), enc(2, 'Ended'), enc(1, 'Active')]).map((e) => e.id)).toEqual([3, 1]);
  });
  it('returns an empty list for an empty list', () => {
    expect(openEncounters([])).toEqual([]);
  });
});
