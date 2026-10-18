import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import type { Item } from '../api/types';

export const MAX_COMPARE = 4;
const STORAGE_KEY = 'ncdb.compare';

export interface CompareEntry {
  id: number;
  name: string;
  category: string;
}

interface CompareContextValue {
  entries: CompareEntry[];
  has: (id: number) => boolean;
  toggle: (item: Pick<Item, 'id' | 'name' | 'category'>) => 'added' | 'removed' | 'full';
  remove: (id: number) => void;
  clear: () => void;
}

const CompareContext = createContext<CompareContextValue | null>(null);

function load(): CompareEntry[] {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as CompareEntry[]).slice(0, MAX_COMPARE) : [];
  } catch {
    return [];
  }
}

export function CompareProvider({ children }: { children: ReactNode }) {
  const [entries, setEntries] = useState<CompareEntry[]>(load);

  useEffect(() => {
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(entries));
    } catch {
      /* ignore */
    }
  }, [entries]);

  const value = useMemo<CompareContextValue>(() => ({
    entries,
    has: (id) => entries.some((e) => e.id === id),
    toggle: (item) => {
      if (entries.some((e) => e.id === item.id)) {
        setEntries((list) => list.filter((e) => e.id !== item.id));
        return 'removed';
      }
      if (entries.length >= MAX_COMPARE) return 'full';
      setEntries((list) => [...list, { id: item.id, name: item.name, category: item.category }]);
      return 'added';
    },
    remove: (id) => setEntries((list) => list.filter((e) => e.id !== id)),
    clear: () => setEntries([]),
  }), [entries]);

  return <CompareContext.Provider value={value}>{children}</CompareContext.Provider>;
}

export function useCompare() {
  const ctx = useContext(CompareContext);
  if (!ctx) throw new Error('useCompare must be used inside CompareProvider');
  return ctx;
}
