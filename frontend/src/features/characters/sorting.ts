export type SortDir = 'asc' | 'desc';
export interface SortState<K extends string = string> { key: K; dir: SortDir }

/** Clicking a column: the first click sorts it ascending, the next one flips it. */
export function nextSort<K extends string>(current: SortState<K> | null, key: K): SortState<K> {
  return current?.key === key ? { key, dir: current.dir === 'asc' ? 'desc' : 'asc' } : { key, dir: 'asc' };
}

/** A sorted copy of the rows; empty values always go last, numbers compare as numbers and text by the locale. */
export function sortRows<T, K extends string>(rows: readonly T[], sort: SortState<K> | null, value: (row: T, key: K) => string | number | null | undefined): T[] {
  if (!sort) return [...rows];
  const sign = sort.dir === 'asc' ? 1 : -1;
  return rows
    .map((row, index) => ({ row, index, v: value(row, sort.key) }))
    .sort((a, b) => {
      const empty = (a.v == null || a.v === '' ? 1 : 0) - (b.v == null || b.v === '' ? 1 : 0);
      if (empty !== 0) return empty;
      const diff = typeof a.v === 'number' && typeof b.v === 'number'
        ? a.v - b.v
        : String(a.v ?? '').localeCompare(String(b.v ?? ''), undefined, { sensitivity: 'base', numeric: true });
      return diff !== 0 ? diff * sign : a.index - b.index;
    })
    .map((x) => x.row);
}

/** One page of the rows, with the page clamped to the ones that exist. */
export function pageOf<T>(rows: readonly T[], page: number, size: number): { items: T[]; page: number; totalPages: number } {
  const totalPages = Math.max(1, Math.ceil(rows.length / size));
  const current = Math.min(Math.max(1, page), totalPages);
  return { items: rows.slice((current - 1) * size, current * size), page: current, totalPages };
}

/** The aria-sort of a header holding these columns. */
export function ariaSort<K extends string>(sort: SortState<K> | null, columns: K[]): 'ascending' | 'descending' | 'none' {
  return sort && columns.includes(sort.key) ? (sort.dir === 'asc' ? 'ascending' : 'descending') : 'none';
}
