import { createContext, useContext } from 'react';

// The table's sheet popup rolls a clicked skill in the chat; elsewhere (no chat) skills stay plain text.
export interface SheetRoll {
  /** luck: LUCK points spent before rolling; the server adds +1 each (CP:R p.130). reroll: 1 more LUCK reserved for a free reroll. */
  rollSkill: (label: string, total: number, luck?: number, reroll?: boolean) => Promise<void>;
}

export const SheetRollContext = createContext<SheetRoll | null>(null);

export const useSheetRoll = () => useContext(SheetRollContext);

/** A skill Check: 1d10 + the skill BASE (CP:R p.128). */
export const skillFormula = (total: number) => (total >= 0 ? `1d10+${total}` : `1d10${total}`);
