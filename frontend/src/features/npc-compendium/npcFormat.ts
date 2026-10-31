/** A number as printed in the book; null (a dash in the book) shows as an em dash. */
export function formatPrinted(value: number | null | undefined): string {
  return value == null ? '—' : String(value);
}

export const STAT_ORDER = ['INT', 'REF', 'DEX', 'TECH', 'COOL', 'WILL', 'LUCK', 'MOVE', 'BODY', 'EMP'] as const;

export const RANGE_BANDS = ['0-6', '7-12', '13-25', '26-50', '51-100', '101-200', '201-400', '401-800'] as const;
