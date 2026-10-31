import type { NpcSummary } from '../../api/npcCompendium';

export type NpcFilters = { q: string; book: string; role: string; sheet: 'all' | 'sheet' | 'profile' };

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
