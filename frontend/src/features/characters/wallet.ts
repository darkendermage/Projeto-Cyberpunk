import type { CharacterEvent, CharacterEventKind } from '../../api/characterTypes';

export interface LastMove { amount: number; text: string; full: string }

const MAX_TEXT = 36;

/** The newest entry of a kind that moved the value (events come newest first), with a short text for the wallet. */
export function lastMove(events: readonly CharacterEvent[] | undefined, kind: CharacterEventKind): LastMove | null {
  const e = (events ?? []).find((x) => x.kind === kind && x.amount != null && x.amount !== 0 && !x.undone);
  if (!e) return null;
  const full = e.description.trim().replace(/\.$/, '');
  const text = full.length > MAX_TEXT ? `${full.slice(0, MAX_TEXT - 1).trimEnd()}…` : full;
  return { amount: e.amount!, text, full };
}
