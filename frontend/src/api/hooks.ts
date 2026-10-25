import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './client';
import type { FilterCriterionDto, NpcSheet } from './types';

export const PAGE_SIZES = [12, 24, 48, 96];

export const queryKeys = {
  categories: ['categories'] as const,
  schema: (category: string) => ['schema', category] as const,
  search: (category: string, params: unknown) => ['search', category, params] as const,
  item: (id: number) => ['item', id] as const,
  items: (ids: number[]) => ['items', ids] as const,
  favorites: ['favorites'] as const,
  favoriteIds: ['favorites', 'ids'] as const,
  lists: ['lists'] as const,
  list: (id: number) => ['lists', id] as const,
  global: (q: string) => ['global-search', q] as const,
  npcRules: ['npc-rules'] as const,
  npcs: ['npcs'] as const,
  npcValidation: (sheet: NpcSheet) => ['npc-validation', sheet] as const,
  update: ['app-update'] as const,
};

/** Newer release of the desktop app; checked once per hour, failures just hide the banner. */
export const useUpdateInfo = () =>
  useQuery({ queryKey: queryKeys.update, queryFn: ({ signal }) => api.updateInfo(signal), staleTime: 60 * 60_000, retry: false });

export const useCategories = () =>
  useQuery({ queryKey: queryKeys.categories, queryFn: ({ signal }) => api.categories(signal), staleTime: 60_000 });

/** Maps either a category key ("weapons") or its enum name ("Weapon") to the route key. */
export function useCategoryKey() {
  const categories = useCategories();
  return (value: string) => categories.data?.find((c) => c.key === value || c.category === value)?.key ?? value;
}

export const useSchema = (category: string | undefined) =>
  useQuery({
    queryKey: queryKeys.schema(category ?? ''),
    queryFn: ({ signal }) => api.schema(category!, signal),
    enabled: Boolean(category),
    staleTime: 5 * 60_000,
  });

export interface SearchParams {
  q: string;
  filters: FilterCriterionDto[];
  sort?: string;
  dir?: 'asc' | 'desc';
  page: number;
  pageSize: number;
}

/** Server-side faceted search, one page at a time (keeps the previous page on screen while loading). */
export const useCatalogSearch = (category: string | undefined, params: SearchParams, enabled: boolean) =>
  useQuery({
    queryKey: queryKeys.search(category ?? '', params),
    queryFn: ({ signal }) => api.search(category!, { ...params, includeFacets: true }, signal),
    enabled: Boolean(category) && enabled,
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  });

export const useItemDetail = (id: number | null) =>
  useQuery({
    queryKey: queryKeys.item(id ?? 0),
    queryFn: ({ signal }) => api.item(id!, signal),
    enabled: id !== null,
  });

export const useItems = (ids: number[]) =>
  useQuery({
    queryKey: queryKeys.items(ids),
    queryFn: ({ signal }) => api.items(ids, signal),
    enabled: ids.length > 0,
    placeholderData: keepPreviousData,
  });

export const useGlobalSearch = (q: string) =>
  useQuery({
    queryKey: queryKeys.global(q),
    queryFn: ({ signal }) => api.globalSearch(q, signal),
    enabled: q.trim().length >= 2,
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  });

// ---------------------------------------------------------------- favorites

export const useFavoriteIds = () =>
  useQuery({ queryKey: queryKeys.favoriteIds, queryFn: api.favoriteIds, staleTime: 60_000 });

export const useFavorites = () => useQuery({ queryKey: queryKeys.favorites, queryFn: api.favorites });

/** Optimistic toggle so the star reacts instantly. */
export function useToggleFavorite() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, favorite }: { id: number; favorite: boolean }) =>
      favorite ? api.addFavorite(id) : api.removeFavorite(id),
    onMutate: async ({ id, favorite }) => {
      await client.cancelQueries({ queryKey: queryKeys.favoriteIds });
      const previous = client.getQueryData<number[]>(queryKeys.favoriteIds);
      client.setQueryData<number[]>(queryKeys.favoriteIds, (ids = []) =>
        favorite ? [id, ...ids.filter((x) => x !== id)] : ids.filter((x) => x !== id));
      return { previous };
    },
    onError: (_error, _vars, context) => client.setQueryData(queryKeys.favoriteIds, context?.previous),
    onSettled: () => client.invalidateQueries({ queryKey: queryKeys.favorites }),
  });
}

// ---------------------------------------------------------------- lists

export const useLists = () => useQuery({ queryKey: queryKeys.lists, queryFn: api.lists });

export const useList = (id: number | null) =>
  useQuery({ queryKey: queryKeys.list(id ?? 0), queryFn: () => api.list(id!), enabled: id !== null });

export function useListMutations() {
  const client = useQueryClient();
  const refresh = (id?: number) => {
    client.invalidateQueries({ queryKey: queryKeys.lists, exact: true });
    if (id) client.invalidateQueries({ queryKey: queryKeys.list(id) });
  };
  return {
    create: useMutation({
      mutationFn: ({ name, description }: { name: string; description?: string }) => api.createList(name, description),
      onSuccess: (list) => { client.setQueryData(queryKeys.list(list.id), list); refresh(); },
    }),
    update: useMutation({
      mutationFn: ({ id, name, description }: { id: number; name: string; description?: string }) => api.updateList(id, name, description),
      onSuccess: (list) => { client.setQueryData(queryKeys.list(list.id), list); refresh(); },
    }),
    remove: useMutation({ mutationFn: (id: number) => api.deleteList(id), onSuccess: () => refresh() }),
    addEntry: useMutation({
      mutationFn: ({ listId, itemId, quantity }: { listId: number; itemId: number; quantity?: number }) =>
        api.addListEntry(listId, itemId, quantity),
      onSuccess: (list) => { client.setQueryData(queryKeys.list(list.id), list); refresh(); },
    }),
    updateEntry: useMutation({
      mutationFn: ({ listId, entryId, quantity, note }: { listId: number; entryId: number; quantity: number; note?: string | null }) =>
        api.updateListEntry(listId, entryId, quantity, note),
      onSuccess: (list) => { client.setQueryData(queryKeys.list(list.id), list); refresh(); },
    }),
    removeEntry: useMutation({
      mutationFn: ({ listId, entryId }: { listId: number; entryId: number }) => api.removeListEntry(listId, entryId),
      onSuccess: (list) => { client.setQueryData(queryKeys.list(list.id), list); refresh(); },
    }),
  };
}

// ---------------------------------------------------------------- enemy generator

export const useNpcRules = () =>
  useQuery({ queryKey: queryKeys.npcRules, queryFn: ({ signal }) => api.npcRules(signal), staleTime: Infinity });

export const useNpcs = () => useQuery({ queryKey: queryKeys.npcs, queryFn: api.npcs });

/** Live validation of the sheet being edited, through the same server-side validator as every other mode. */
export const useNpcValidation = (sheet: NpcSheet) =>
  useQuery({
    queryKey: queryKeys.npcValidation(sheet),
    queryFn: ({ signal }) => api.validateNpc(sheet, signal),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  });

export function useNpcMutations() {
  const client = useQueryClient();
  const refresh = () => client.invalidateQueries({ queryKey: queryKeys.npcs });
  return {
    save: useMutation({
      mutationFn: ({ id, sheet, draft }: { id: number | null; sheet: NpcSheet; draft: boolean }) =>
        id === null ? api.createNpc(sheet, draft) : api.updateNpc(id, sheet, draft),
      onSuccess: refresh,
    }),
    duplicate: useMutation({ mutationFn: (id: number) => api.duplicateNpc(id), onSuccess: refresh }),
    remove: useMutation({ mutationFn: (id: number) => api.deleteNpc(id), onSuccess: refresh }),
  };
}
