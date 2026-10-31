import type { CombatSide, EncounterSummary } from '../../api/encounterTypes';

/** Encounters that still take new combatants (the ended ones are history). */
export const openEncounters = (list: EncounterSummary[]): EncounterSummary[] => list.filter((e) => e.status === 'Active');

type EncounterCommands = {
  get: (id: number) => Promise<{ version: number }>;
  command: (id: number, path: string, body: Record<string, unknown>) => Promise<unknown>;
};

/** The version cached by the list may be stale; the encounter itself says which one is current. */
export async function addCompendiumNpc(api: EncounterCommands, id: number, npcId: string, side: CombatSide, count: number) {
  const current = await api.get(id);
  await api.command(id, 'combatants', { version: current.version, side, compendiumNpcId: npcId, count });
}
