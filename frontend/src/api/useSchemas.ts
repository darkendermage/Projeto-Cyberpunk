import { useQueries } from '@tanstack/react-query';
import { api } from './client';
import { queryKeys } from './hooks';
import type { CategorySchema } from './types';

/** Loads the schema of every category present in a mixed list (favorites, lists, comparison). */
export function useSchemas(categories: string[]): Map<string, CategorySchema> {
  const unique = [...new Set(categories)].sort();
  const results = useQueries({
    queries: unique.map((key) => ({
      queryKey: queryKeys.schema(key),
      queryFn: ({ signal }: { signal: AbortSignal }) => api.schema(key, signal),
      staleTime: 5 * 60_000,
    })),
  });
  const map = new Map<string, CategorySchema>();
  results.forEach((r, i) => { if (r.data) map.set(unique[i], r.data); });
  return map;
}
