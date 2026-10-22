import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, request } from './client';
import type {
  DoorPrizeResult, GenerateMarketRequest, HaggleRequest, HaggleResult, IndexPage, IndexQuery, MarketEventResult,
  MysteryBoxResult, NightMarketDetail, NightMarketRules, NightMarketSummary, SellQuote, SellQuoteRequest,
} from './nightMarketTypes';

const json = (body: unknown) => JSON.stringify(body);
const post = <T>(path: string, body: unknown) => request<T>(path, { method: 'POST', body: json(body) });

function indexParams(q: IndexQuery): string {
  const p = new URLSearchParams({ page: String(q.page), pageSize: String(q.pageSize) });
  if (q.q) p.set('q', q.q);
  if (q.section) p.set('section', q.section);
  if (q.category) p.set('category', q.category);
  if (q.catalogued != null) p.set('catalogued', String(q.catalogued));
  return p.toString();
}

export const marketApi = {
  rules: (signal?: AbortSignal) => request<NightMarketRules>('/night-markets/rules', { signal }),
  index: (q: IndexQuery, signal?: AbortSignal) => request<IndexPage>(`/night-markets/index?${indexParams(q)}`, { signal }),
  haggle: (body: HaggleRequest) => post<HaggleResult>('/night-markets/haggle', body),
  sellQuote: (body: SellQuoteRequest) => post<SellQuote>('/night-markets/sell-quote', body),
  mysteryBox: (body: { boxRoll?: number | null; contentRoll?: number | null }) => post<MysteryBoxResult>('/night-markets/mystery-box', body),
  list: () => request<NightMarketSummary[]>('/night-markets'),
  get: (id: number, signal?: AbortSignal) => request<NightMarketDetail>(`/night-markets/${id}`, { signal }),
  create: (body: GenerateMarketRequest) => post<NightMarketDetail>('/night-markets', body),
  update: (id: number, body: { version: number; name: string; notes?: string | null; status: string }) =>
    request<NightMarketDetail>(`/night-markets/${id}`, { method: 'PUT', body: json(body) }),
  remove: (id: number) => request<void>(`/night-markets/${id}`, { method: 'DELETE' }),
  /** Every market command: POST (or PUT) /night-markets/{id}/{path} with the version the client last saw. */
  command: <T = NightMarketDetail>(id: number, path: string, body: Record<string, unknown>, method: 'POST' | 'PUT' = 'POST') =>
    request<T>(`/night-markets/${id}/${path}`, { method, body: json(body) }),
};

export const marketKeys = {
  rules: ['night-market-rules'] as const,
  list: ['night-markets'] as const,
  detail: (id: number) => ['night-markets', id] as const,
  index: (q: IndexQuery) => ['night-market-index', q] as const,
};

export const useMarketRules = () =>
  useQuery({ queryKey: marketKeys.rules, queryFn: ({ signal }) => marketApi.rules(signal), staleTime: Infinity });

export const useMarkets = () => useQuery({ queryKey: marketKeys.list, queryFn: marketApi.list });

export const useMarket = (id: number) =>
  useQuery({ queryKey: marketKeys.detail(id), queryFn: ({ signal }) => marketApi.get(id, signal), enabled: id > 0 });

export const useMarketIndex = (q: IndexQuery) =>
  useQuery({ queryKey: marketKeys.index(q), queryFn: ({ signal }) => marketApi.index(q, signal), placeholderData: keepPreviousData });

/** Writes a fresh server answer into the cache and refreshes the list. */
export function useStoreMarket() {
  const client = useQueryClient();
  return (detail: NightMarketDetail) => {
    client.setQueryData(marketKeys.detail(detail.id), detail);
    client.invalidateQueries({ queryKey: marketKeys.list, exact: true });
  };
}

/** Sends a market command with the version currently cached; a 409 reloads the market. */
export function useMarketCommand(id: number) {
  const client = useQueryClient();
  const store = useStoreMarket();
  return useMutation({
    mutationFn: ({ path, body = {}, method }: { path: string; body?: Record<string, unknown>; method?: 'POST' | 'PUT' }) => {
      const current = client.getQueryData<NightMarketDetail>(marketKeys.detail(id));
      return marketApi.command<NightMarketDetail | MarketEventResult<unknown>>(id, path, { ...body, version: current?.version ?? 0 }, method);
    },
    onSuccess: (answer) => store('market' in answer ? answer.market : answer),
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) client.invalidateQueries({ queryKey: marketKeys.detail(id) });
    },
  });
}

export type DoorPrizeAnswer = MarketEventResult<DoorPrizeResult>;
export type MysteryBoxAnswer = MarketEventResult<MysteryBoxResult>;
