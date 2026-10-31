import type { EncounterSummary } from '../../api/encounterTypes';

/** Encounters that still take new combatants (the ended ones are history). */
export const openEncounters = (list: EncounterSummary[]): EncounterSummary[] => list.filter((e) => e.status === 'Active');
