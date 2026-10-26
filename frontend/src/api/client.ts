import type {
  CategorySchema, CategorySummary, GearListDetail, GearListSummary, GlobalSearchGroup, HealthInfo, Item,
  ItemDetail, NpcDetail, NpcGenerationRequest, NpcGenerationResult, NpcRules, NpcSheet, NpcSummary,
  NpcValidationReport, ProblemDetails, SearchRequest, SearchResult, UpdateInfo,
} from './types';
import { locale, t, tv } from '../i18n';

export const API_BASE = import.meta.env.VITE_API_BASE ?? '/api';
const BASE = API_BASE;

/** Error carrying the RFC 7807 payload returned by the API (title + per-field errors). */
export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.title ?? t(`Erro ${status}`, `Error ${status}`));
    this.status = status;
    this.problem = problem;
  }

  get fieldErrors(): Record<string, string[]> {
    return this.problem.errors ?? {};
  }
}

export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${BASE}${path}`, {
      ...init,
      headers: { Accept: 'application/json', 'Accept-Language': locale, ...(init?.body ? { 'Content-Type': 'application/json' } : {}), ...init?.headers },
    });
  } catch (error) {
    if ((error as Error).name === 'AbortError') throw error;
    throw new ApiError(0, { title: t('Sem conexão com a API. Verifique se o backend está rodando.', 'No connection to the API. Check that the backend is running.') });
  }

  if (!response.ok) {
    let problem: ProblemDetails = { title: t(`Erro ${response.status}`, `Error ${response.status}`) };
    try {
      problem = await response.json();
    } catch {
      /* non-JSON error body */
    }
    throw new ApiError(response.status, problem);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

const json = (body: unknown) => JSON.stringify(body);

/** The schema is display metadata kept in Portuguese on the server: labels, groups and option labels go through the data dictionary. */
function translateSchema(schema: CategorySchema): CategorySchema {
  return {
    ...schema,
    label: tv(schema.label),
    sublabel: tv(schema.sublabel),
    description: tv(schema.description),
    fields: schema.fields.map((f) => ({
      ...f,
      label: tv(f.label),
      group: tv(f.group),
      unit: tv(f.unit),
      description: tv(f.description),
      options: f.options?.map((o) => ({ ...o, label: tv(o.label) })),
    })),
    sortOptions: schema.sortOptions.map((o) => ({ ...o, label: tv(o.label) })),
  };
}

export const api = {
  health: (signal?: AbortSignal) => request<HealthInfo>('/health', { signal }),
  updateInfo: (signal?: AbortSignal) => request<UpdateInfo>('/app/update', { signal }),
  applyUpdate: (version: string) => request<{ restarting: boolean }>('/app/update', { method: 'POST', body: json({ version }) }),
  categories: (signal?: AbortSignal) => request<CategorySummary[]>('/categories', { signal })
    .then((list) => list.map((c) => ({ ...c, label: tv(c.label), sublabel: tv(c.sublabel) }))),
  schema: (category: string, signal?: AbortSignal) => request<CategorySchema>(`/categories/${category}/schema`, { signal }).then(translateSchema),
  search: (category: string, body: SearchRequest, signal?: AbortSignal) =>
    request<SearchResult>(`/catalog/${category}/search`, { method: 'POST', body: json(body), signal }),
  globalSearch: (q: string, signal?: AbortSignal) =>
    request<GlobalSearchGroup[]>(`/catalog/search?q=${encodeURIComponent(q)}&perCategory=4`, { signal }),
  item: (id: number, signal?: AbortSignal) => request<ItemDetail>(`/items/${id}`, { signal }),
  items: (ids: number[], signal?: AbortSignal) => request<Item[]>(`/items?ids=${ids.join(',')}`, { signal }),

  favoriteIds: () => request<number[]>('/favorites/ids'),
  favorites: () => request<Item[]>('/favorites'),
  addFavorite: (id: number) => request<void>(`/favorites/${id}`, { method: 'PUT' }),
  removeFavorite: (id: number) => request<void>(`/favorites/${id}`, { method: 'DELETE' }),

  lists: () => request<GearListSummary[]>('/lists'),
  list: (id: number) => request<GearListDetail>(`/lists/${id}`),
  createList: (name: string, description?: string) =>
    request<GearListDetail>('/lists', { method: 'POST', body: json({ name, description }) }),
  updateList: (id: number, name: string, description?: string) =>
    request<GearListDetail>(`/lists/${id}`, { method: 'PUT', body: json({ name, description }) }),
  deleteList: (id: number) => request<void>(`/lists/${id}`, { method: 'DELETE' }),
  addListEntry: (id: number, itemId: number, quantity = 1) =>
    request<GearListDetail>(`/lists/${id}/entries`, { method: 'POST', body: json({ itemId, quantity }) }),
  updateListEntry: (id: number, entryId: number, quantity: number, note?: string | null) =>
    request<GearListDetail>(`/lists/${id}/entries/${entryId}`, { method: 'PUT', body: json({ quantity, note }) }),
  removeListEntry: (id: number, entryId: number) =>
    request<GearListDetail>(`/lists/${id}/entries/${entryId}`, { method: 'DELETE' }),

  npcRules: (signal?: AbortSignal) => request<NpcRules>('/npcs/rules', { signal }),
  validateNpc: (sheet: NpcSheet, signal?: AbortSignal) =>
    request<NpcValidationReport>('/npcs/validate', { method: 'POST', body: json(sheet), signal }),
  generateNpc: (body: NpcGenerationRequest) =>
    request<NpcGenerationResult>('/npcs/generate', { method: 'POST', body: json(body) }),
  installCyberware: (sheet: NpcSheet, itemId: number) =>
    request<NpcSheet>('/npcs/cyberware/install', { method: 'POST', body: json({ sheet, itemId }) }),
  npcs: () => request<NpcSummary[]>('/npcs'),
  npc: (id: number) => request<NpcDetail>(`/npcs/${id}`),
  createNpc: (sheet: NpcSheet, saveAsDraft: boolean) =>
    request<NpcDetail>('/npcs', { method: 'POST', body: json({ sheet, saveAsDraft }) }),
  updateNpc: (id: number, sheet: NpcSheet, saveAsDraft: boolean) =>
    request<NpcDetail>(`/npcs/${id}`, { method: 'PUT', body: json({ sheet, saveAsDraft }) }),
  duplicateNpc: (id: number) => request<NpcDetail>(`/npcs/${id}/duplicate`, { method: 'POST' }),
  deleteNpc: (id: number) => request<void>(`/npcs/${id}`, { method: 'DELETE' }),

};
