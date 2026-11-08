// null while empty or not a number yet (still typing), otherwise rounded and clamped.
export function parseWholeNumber(raw: string, min: number, max: number): number | null {
  if (raw.trim() === '') return null;
  const n = Number(raw);
  if (!Number.isFinite(n)) return null;
  return Math.min(max, Math.max(min, Math.round(n)));
}

// Comma works as the decimal point. null while empty or incomplete, otherwise clamped.
export function parseDecimal(raw: string, min: number, max: number): number | null {
  const text = raw.trim().replace(',', '.');
  if (text === '') return null;
  const n = Number(text);
  if (!Number.isFinite(n)) return null;
  return Math.min(max, Math.max(min, n));
}
