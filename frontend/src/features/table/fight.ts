import type { CombatSide } from '../../api/encounterTypes';
import type { TokenView } from '../../api/tableTypes';

/** Where a fighter started from a token comes from: its PC, its saved NPC, its compendium NPC, or a generic enemy. */
export type FightEntryKind = 'pc' | 'npc' | 'compendium' | 'generic';

export interface FightEntry { tokenId: string; label: string; kind: FightEntryKind; include: boolean; side: CombatSide }

/**
 * The tokens of the map as the start of a fight: characters and NPCs come in, plain tokens (a barrel, a door) stay
 * out unless the GM ticks them (they then fight as generic enemies). PCs and the players' tokens are allies.
 */
export function fightEntries(tokens: TokenView[]): FightEntry[] {
  return tokens.map((tk) => {
    const kind: FightEntryKind = tk.characterId != null ? 'pc' : tk.npcId != null ? 'npc' : tk.compendiumNpcId ? 'compendium' : 'generic';
    return {
      tokenId: tk.id, label: tk.label, kind, include: kind !== 'generic',
      side: kind === 'pc' || tk.ownerSeatId != null ? 'Ally' : 'Enemy',
    };
  });
}

export function fightRequest(name: string, entries: FightEntry[]) {
  return {
    name: name.trim() || null,
    tokens: entries.filter((e) => e.include).map((e) => ({ tokenId: e.tokenId, side: e.side })),
  };
}
