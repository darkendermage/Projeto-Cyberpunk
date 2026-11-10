import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { API_BASE, ApiError, request, seatContext } from './client';
import type {
  CharacterDetail, CharacterEvent, CharacterEventKind, CharacterRules, CharacterSheet, CharacterSummary,
  CharacterMatch, CreateCharacterRequest, ImportMode, InstallPreview, InstallRequestBody, StorePage,
} from './characterTypes';
import { locale, t } from '../i18n';

const json = (body: unknown) => JSON.stringify(body);
const post = <T>(path: string, body: unknown) => request<T>(path, { method: 'POST', body: json(body) });

/** Columns the store sorts by on the server, before paging. */
export type StoreSortKey = 'name' | 'category' | 'price';
const STORE_SORT: Record<StoreSortKey, string> = { name: 'Name', category: 'Category', price: 'Price' };

export const characterApi = {
  rules: (signal?: AbortSignal) => request<CharacterRules>('/characters/rules', { signal }),
  list: () => request<CharacterSummary[]>('/characters'),
  /** One page of priced catalog items (the server clamps the page to the last one). */
  store: (q: string, category: string | null, page: number, pageSize: number, signal?: AbortSignal, sort?: { key: StoreSortKey; dir: 'asc' | 'desc' } | null) =>
    request<StorePage>(`/characters/store?q=${encodeURIComponent(q)}${category ? `&category=${category}` : ''}&page=${page}&pageSize=${pageSize}${sort ? `&sort=${STORE_SORT[sort.key]}&desc=${sort.dir === 'desc'}` : ''}`, { signal }),
  get: (id: number, signal?: AbortSignal) => request<CharacterDetail>(`/characters/${id}`, { signal }),
  events: (id: number, kind?: CharacterEventKind) =>
    request<CharacterEvent[]>(`/characters/${id}/events${kind ? `?kind=${kind}` : ''}`),
  create: (body: CreateCharacterRequest) => post<CharacterDetail>('/characters', body),
  duplicate: (id: number) => request<CharacterDetail>(`/characters/${id}/duplicate`, { method: 'POST' }),
  exportUrl: (id: number) => `${API_BASE}/characters/${id}/export`,
  /** From the content of an exported file: 'update' overwrites the sheet with the file's uid, 'new' always creates. */
  importFile: ({ content, mode = 'new' }: { content: unknown; mode?: ImportMode }) => post<CharacterDetail>(`/characters/import?mode=${mode}`, content),
  /** The sheet this install already has with that uid, or null. */
  byUid: async (uid: string): Promise<CharacterMatch | null> => {
    try {
      return await request<CharacterMatch>(`/characters/by-uid/${encodeURIComponent(uid)}`);
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null;
      throw error;
    }
  },
  remove: (id: number) => request<void>(`/characters/${id}`, { method: 'DELETE' }),
  save: (id: number, version: number, sheet: CharacterSheet, manualReason?: string) =>
    request<CharacterDetail>(`/characters/${id}`, { method: 'PUT', body: json({ version, sheet, manualReason }) }),
  /** Every rule command: POST /characters/{id}/{path} with the version the client last saw. */
  command: (id: number, path: string, body: Record<string, unknown>) => post<CharacterDetail>(`/characters/${id}/${path}`, body),
  previewInstall: (id: number, body: InstallRequestBody, signal?: AbortSignal) =>
    request<InstallPreview>(`/characters/${id}/cyberware/preview`, { method: 'POST', body: json({ version: 0, ...body }), signal }),
  // Images load without headers: on a player's screen the seat key goes in the query string.
  portraitUrl: (id: number, stamp: string) => `${API_BASE}/characters/${id}/portrait?v=${encodeURIComponent(stamp)}${seatContext() ? `&seat=${encodeURIComponent(seatContext()!)}` : ''}`,
  uploadPortrait: async (id: number, file: File) => {
    const form = new FormData();
    form.append('file', file);
    let response: Response;
    try {
      response = await fetch(`${API_BASE}/characters/${id}/portrait`, { method: 'PUT', body: form, headers: { 'Accept-Language': locale, ...(seatContext() ? { 'X-Seat': seatContext()! } : {}) } });
    } catch {
      // The server closes the connection on an oversized upload, so the browser only sees a network failure.
      throw new ApiError(0, { title: t('Não deu para enviar o retrato. Verifique se a imagem tem até 2 MB e se o programa está aberto.', 'Couldn\'t upload the portrait. Check that the image is 2 MB or less and that the program is open.') });
    }
    if (!response.ok) {
      let problem = { title: t(`Erro ${response.status}`, `Error ${response.status}`) } as { title?: string; errors?: Record<string, string[]> };
      try { problem = await response.json(); } catch { /* non-JSON */ }
      throw new ApiError(response.status, problem);
    }
  },
  deletePortrait: (id: number) => request<void>(`/characters/${id}/portrait`, { method: 'DELETE' }),
};

export const characterKeys = {
  rules: ['character-rules'] as const,
  list: ['characters'] as const,
  detail: (id: number) => ['characters', id] as const,
  events: (id: number) => ['characters', id, 'events'] as const,
};

export const useCharacterRules = () =>
  useQuery({ queryKey: characterKeys.rules, queryFn: ({ signal }) => characterApi.rules(signal), staleTime: Infinity });

export const useCharacters = (enabled = true) => useQuery({ queryKey: characterKeys.list, queryFn: characterApi.list, enabled });

export const useCharacter = (id: number) =>
  useQuery({ queryKey: characterKeys.detail(id), queryFn: ({ signal }) => characterApi.get(id, signal), enabled: id > 0 });

export const useCharacterEvents = (id: number, enabled = true) =>
  useQuery({ queryKey: characterKeys.events(id), queryFn: () => characterApi.events(id), enabled });

// Rule refusals come back as 400 field errors and the title just says "invalid fields", so show the first
// field message instead.
export function problemMessage(error: unknown, fallback = t('Algo deu errado.', 'Something went wrong.')): string {
  if (error instanceof ApiError) {
    const first = Object.values(error.fieldErrors).flat()[0];
    return first ?? error.message;
  }
  return error instanceof Error && error.message ? error.message : fallback;
}

// Puts a fresh server answer in the cache so every tab of the character updates, then refreshes list + history.
export function useStoreCharacter() {
  const client = useQueryClient();
  return (detail: CharacterDetail) => {
    client.setQueryData(characterKeys.detail(detail.id), detail);
    client.invalidateQueries({ queryKey: characterKeys.list, exact: true });
    client.invalidateQueries({ queryKey: characterKeys.events(detail.id) });
  };
}

/** Sends a rule command for the character, always with the version currently cached. */
export function useCharacterCommand(id: number) {
  const client = useQueryClient();
  const store = useStoreCharacter();
  return useMutation({
    mutationFn: ({ path, body = {} }: { path: string; body?: Record<string, unknown> }) => {
      const current = client.getQueryData<CharacterDetail>(characterKeys.detail(id));
      return characterApi.command(id, path, { ...body, version: current?.version ?? 0 });
    },
    onSuccess: store,
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) client.invalidateQueries({ queryKey: characterKeys.detail(id) });
    },
  });
}

/** Largest file the import accepts (the server enforces the same limit). */
export const IMPORT_MAX_BYTES = 12 * 1024 * 1024;

/** Downloads one character's export file; the server names it after the character. */
export function downloadCharacter(id: number) {
  const link = document.createElement('a');
  link.href = characterApi.exportUrl(id);
  link.download = '';
  document.body.appendChild(link);
  link.click();
  link.remove();
}

/** The stable id an exported file carries (files from older versions have none). */
export function fileUid(content: unknown): string | null {
  const uid = (content as { uid?: unknown } | null)?.uid;
  return typeof uid === 'string' && uid.length > 0 ? uid : null;
}

/** Reads a chosen file as JSON, refusing what is clearly not an export before it reaches the server. */
export async function readCharacterFile(file: File): Promise<unknown> {
  if (file.size > IMPORT_MAX_BYTES) throw new Error(t('O arquivo passa de 12 MB.', 'The file is larger than 12 MB.'));
  try {
    return JSON.parse(await file.text());
  } catch {
    throw new Error(t('O arquivo não é um personagem exportado (JSON inválido).', 'The file is not an exported character (invalid JSON).'));
  }
}

export function useCharacterLibrary() {
  const client = useQueryClient();
  const refresh = () => client.invalidateQueries({ queryKey: characterKeys.list });
  return {
    create: useMutation({ mutationFn: characterApi.create, onSuccess: refresh }),
    duplicate: useMutation({ mutationFn: characterApi.duplicate, onSuccess: refresh }),
    importFile: useMutation({ mutationFn: characterApi.importFile, onSuccess: refresh }),
    remove: useMutation({
      mutationFn: characterApi.remove,
      onSuccess: (_, id) => {
        client.removeQueries({ queryKey: characterKeys.detail(id) });
        return refresh();
      },
    }),
  };
}
