import type { NpcSummary } from '../../api/npcCompendium';

export type NpcFilters = { q: string; book: string; role: string; sheet: 'all' | 'sheet' | 'profile' };

/** The filters as the list's URL keeps them, so they survive opening an NPC and coming back. */
export function filtersFromSearch(params: URLSearchParams): NpcFilters {
  const sheet = params.get('sheet');
  return {
    q: params.get('q') ?? '',
    book: params.get('book') ?? '',
    role: params.get('role') ?? '',
    sheet: sheet === 'sheet' || sheet === 'profile' ? sheet : 'all',
  };
}

export function filtersToSearch(f: NpcFilters): URLSearchParams {
  const params = new URLSearchParams();
  if (f.q) params.set('q', f.q);
  if (f.book) params.set('book', f.book);
  if (f.role) params.set('role', f.role);
  if (f.sheet !== 'all') params.set('sheet', f.sheet);
  return params;
}

const fold = (s: string) => s.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();

export function filterNpcs(list: NpcSummary[], f: NpcFilters): NpcSummary[] {
  const q = fold(f.q.trim());
  return list.filter((n) => {
    if (q && ![n.name, ...n.aliases].some((s) => fold(s).includes(q))) return false;
    if (f.book && !n.sources.some((s) => s.book === f.book)) return false;
    if (f.role && n.role !== f.role) return false;
    if (f.sheet === 'sheet' && n.kind !== 'Sheet') return false;
    if (f.sheet === 'profile' && n.kind !== 'Profile') return false;
    return true;
  });
}

const sortedUnique = (values: string[]) => [...new Set(values)].sort((a, b) => a.localeCompare(b));

export const npcBooks = (list: NpcSummary[]) => sortedUnique(list.flatMap((n) => n.sources.map((s) => s.book)));

export const npcRoles = (list: NpcSummary[]) => sortedUnique(list.flatMap((n) => (n.role ? [n.role] : [])));
