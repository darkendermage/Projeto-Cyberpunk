import { describe, expect, it, vi } from 'vitest';
import type { EncounterSummary } from '../../api/encounterTypes';
import { addCompendiumNpc, openEncounters } from './openEncounters';

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

describe('addCompendiumNpc', () => {
  it('sends the version the encounter has now', async () => {
    const api = { get: vi.fn(async () => ({ version: 7 })), command: vi.fn(async () => undefined) };
    await addCompendiumNpc(api, 3, 'adam-smasher', 'Enemy', 2);
    expect(api.get).toHaveBeenCalledWith(3);
    expect(api.command).toHaveBeenCalledWith(3, 'combatants', { version: 7, side: 'Enemy', compendiumNpcId: 'adam-smasher', count: 2 });
  });
  it('sends nothing when the encounter cannot be read', async () => {
    const api = { get: vi.fn(async () => { throw new Error('gone'); }), command: vi.fn(async () => undefined) };
    await expect(addCompendiumNpc(api, 3, 'adam-smasher', 'Enemy', 1)).rejects.toThrow('gone');
    expect(api.command).not.toHaveBeenCalled();
  });
});
