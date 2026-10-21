/**
 * Reads what was typed in a whole-number field: null while it is empty or not a number (the player is still typing),
 * otherwise the value rounded and kept within [min, max].
 */
export function parseWholeNumber(raw: string, min: number, max: number): number | null {
  if (raw.trim() === '') return null;
  const n = Number(raw);
  if (!Number.isFinite(n)) return null;
  return Math.min(max, Math.max(min, Math.round(n)));
}
