import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, request } from './client';
import type { EncounterDetail, EncounterRules, EncounterSummary } from './encounterTypes';

const json = (body: unknown) => JSON.stringify(body);

export const encounterApi = {
  rules: (signal?: AbortSignal) => request<EncounterRules>('/encounters/rules', { signal }),
  list: () => request<EncounterSummary[]>('/encounters'),
  get: (id: number, signal?: AbortSignal) => request<EncounterDetail>(`/encounters/${id}`, { signal }),
  create: (name: string | null) => request<EncounterDetail>('/encounters', { method: 'POST', body: json({ name }) }),
  remove: (id: number) => request<void>(`/encounters/${id}`, { method: 'DELETE' }),
  /** Every action: POST (or PUT) /encounters/{id}/{path} with the version the client last saw. */
  command: (id: number, path: string, body: Record<string, unknown>, method: 'POST' | 'PUT' = 'POST') =>
    request<EncounterDetail>(`/encounters/${id}${path ? `/${path}` : ''}`, { method, body: json(body) }),
};

export const encounterKeys = {
  rules: ['encounter-rules'] as const,
  list: ['encounters'] as const,
  detail: (id: number) => ['encounters', id] as const,
};

export const useEncounterRules = () =>
  useQuery({ queryKey: encounterKeys.rules, queryFn: ({ signal }) => encounterApi.rules(signal), staleTime: Infinity });

export const useEncounters = () => useQuery({ queryKey: encounterKeys.list, queryFn: encounterApi.list });

export const useEncounter = (id: number) =>
  useQuery({ queryKey: encounterKeys.detail(id), queryFn: ({ signal }) => encounterApi.get(id, signal), enabled: id > 0 });

/**
 * Sends an action with the version currently cached; a 409 reloads the encounter. Player characters live on their
 * sheets, so any action may have changed one: their cached sheets are refreshed too.
 */
export function useEncounterCommand(id: number) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ path, body = {}, method }: { path: string; body?: Record<string, unknown>; method?: 'POST' | 'PUT' }) => {
      const current = client.getQueryData<EncounterDetail>(encounterKeys.detail(id));
      return encounterApi.command(id, path, { ...body, version: current?.version ?? 0 }, method);
    },
    onSuccess: (detail) => {
      client.setQueryData(encounterKeys.detail(id), detail);
      client.invalidateQueries({ queryKey: encounterKeys.list, exact: true });
      client.invalidateQueries({ queryKey: ['characters'] });
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) client.invalidateQueries({ queryKey: encounterKeys.detail(id) });
    },
  });
}
