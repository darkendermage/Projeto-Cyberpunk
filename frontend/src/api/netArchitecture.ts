import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { API_BASE, ApiError, request } from './client';
import type {
  GenerateArchitectureRequest, NetArchitectureDetail, NetArchitectureRules, NetArchitectureSummary,
} from './netArchitectureTypes';
import { locale, t } from '../i18n';

const json = (body: unknown) => JSON.stringify(body);

export const netApi = {
  rules: (signal?: AbortSignal) => request<NetArchitectureRules>('/net-architectures/rules', { signal }),
  list: () => request<NetArchitectureSummary[]>('/net-architectures'),
  get: (id: number, signal?: AbortSignal) => request<NetArchitectureDetail>(`/net-architectures/${id}`, { signal }),
  create: (body: GenerateArchitectureRequest) => request<NetArchitectureDetail>('/net-architectures', { method: 'POST', body: json(body) }),
  remove: (id: number) => request<void>(`/net-architectures/${id}`, { method: 'DELETE' }),
  /** Every edit: POST (or PUT) /net-architectures/{id}/{path} with the version the client last saw. */
  command: (id: number, path: string, body: Record<string, unknown>, method: 'POST' | 'PUT' = 'POST') =>
    request<NetArchitectureDetail>(`/net-architectures/${id}${path ? `/${path}` : ''}`, { method, body: json(body) }),
  exportUrl: (id: number, format: 'text' | 'markdown') => `${API_BASE}/net-architectures/${id}/export?format=${format}&lang=${locale}`,
  exportText: async (id: number, format: 'text' | 'markdown') => {
    const response = await fetch(netApi.exportUrl(id, format), { headers: { 'Accept-Language': locale } });
    if (!response.ok) throw new ApiError(response.status, { title: t(`Erro ${response.status}`, `Error ${response.status}`) });
    return response.text();
  },
};

export const netKeys = {
  rules: ['net-architecture-rules'] as const,
  list: ['net-architectures'] as const,
  detail: (id: number) => ['net-architectures', id] as const,
};

export const useNetRules = () =>
  useQuery({ queryKey: netKeys.rules, queryFn: ({ signal }) => netApi.rules(signal), staleTime: Infinity });

export const useArchitectures = () => useQuery({ queryKey: netKeys.list, queryFn: netApi.list });

export const useArchitecture = (id: number) =>
  useQuery({ queryKey: netKeys.detail(id), queryFn: ({ signal }) => netApi.get(id, signal), enabled: id > 0 });

/** Writes a fresh server answer into the cache and refreshes the list. */
export function useStoreArchitecture() {
  const client = useQueryClient();
  return (detail: NetArchitectureDetail) => {
    client.setQueryData(netKeys.detail(detail.id), detail);
    client.invalidateQueries({ queryKey: netKeys.list, exact: true });
  };
}

/** Sends an edit with the version currently cached; a 409 reloads the Architecture. */
export function useNetCommand(id: number) {
  const client = useQueryClient();
  const store = useStoreArchitecture();
  return useMutation({
    mutationFn: ({ path, body = {}, method }: { path: string; body?: Record<string, unknown>; method?: 'POST' | 'PUT' }) => {
      const current = client.getQueryData<NetArchitectureDetail>(netKeys.detail(id));
      return netApi.command(id, path, { ...body, version: current?.version ?? 0 }, method);
    },
    onSuccess: store,
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) client.invalidateQueries({ queryKey: netKeys.detail(id) });
    },
  });
}
