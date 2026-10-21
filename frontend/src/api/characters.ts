import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { API_BASE, ApiError, request } from './client';
import type {
  CharacterDetail, CharacterEvent, CharacterEventKind, CharacterRules, CharacterSheet, CharacterSummary,
  CreateCharacterRequest, InstallPreview, InstallRequestBody, StorePage,
} from './characterTypes';

const json = (body: unknown) => JSON.stringify(body);
const post = <T>(path: string, body: unknown) => request<T>(path, { method: 'POST', body: json(body) });

export const characterApi = {
  rules: (signal?: AbortSignal) => request<CharacterRules>('/characters/rules', { signal }),
  list: () => request<CharacterSummary[]>('/characters'),
  /** One page of priced catalog items (the server clamps the page to the last one). */
  store: (q: string, category: string | null, page: number, pageSize: number, signal?: AbortSignal) =>
    request<StorePage>(`/characters/store?q=${encodeURIComponent(q)}${category ? `&category=${category}` : ''}&page=${page}&pageSize=${pageSize}`, { signal }),
  get: (id: number, signal?: AbortSignal) => request<CharacterDetail>(`/characters/${id}`, { signal }),
  events: (id: number, kind?: CharacterEventKind) =>
    request<CharacterEvent[]>(`/characters/${id}/events${kind ? `?kind=${kind}` : ''}`),
  create: (body: CreateCharacterRequest) => post<CharacterDetail>('/characters', body),
  duplicate: (id: number) => request<CharacterDetail>(`/characters/${id}/duplicate`, { method: 'POST' }),
  exportUrl: (id: number) => `${API_BASE}/characters/${id}/export`,
  /** Creates a new character from the content of an exported file. */
  importFile: (content: unknown) => post<CharacterDetail>('/characters/import', content),
  remove: (id: number) => request<void>(`/characters/${id}`, { method: 'DELETE' }),
  save: (id: number, version: number, sheet: CharacterSheet, manualReason?: string) =>
    request<CharacterDetail>(`/characters/${id}`, { method: 'PUT', body: json({ version, sheet, manualReason }) }),
  /** Every rule command: POST /characters/{id}/{path} with the version the client last saw. */
  command: (id: number, path: string, body: Record<string, unknown>) => post<CharacterDetail>(`/characters/${id}/${path}`, body),
  previewInstall: (id: number, body: InstallRequestBody, signal?: AbortSignal) =>
    request<InstallPreview>(`/characters/${id}/cyberware/preview`, { method: 'POST', body: json({ version: 0, ...body }), signal }),
  portraitUrl: (id: number, stamp: string) => `${API_BASE}/characters/${id}/portrait?v=${encodeURIComponent(stamp)}`,
  uploadPortrait: async (id: number, file: File) => {
    const form = new FormData();
    form.append('file', file);
    const response = await fetch(`${API_BASE}/characters/${id}/portrait`, { method: 'PUT', body: form });
    if (!response.ok) {
      let problem = { title: `Erro ${response.status}` } as { title?: string; errors?: Record<string, string[]> };
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

export const useCharacters = () => useQuery({ queryKey: characterKeys.list, queryFn: characterApi.list });

export const useCharacter = (id: number) =>
  useQuery({ queryKey: characterKeys.detail(id), queryFn: ({ signal }) => characterApi.get(id, signal), enabled: id > 0 });

export const useCharacterEvents = (id: number, enabled = true) =>
  useQuery({ queryKey: characterKeys.events(id), queryFn: () => characterApi.events(id), enabled });

/**
 * Message of an API failure. Rule refusals come back as field errors (400); the title alone only says
 * "invalid fields", so the first field message is what the player needs to read.
 */
export function problemMessage(error: unknown, fallback = 'Algo deu errado.'): string {
  if (error instanceof ApiError) {
    const first = Object.values(error.fieldErrors).flat()[0];
    return first ?? error.message;
  }
  return error instanceof Error && error.message ? error.message : fallback;
}

/**
 * Writes a fresh server answer into the cache so every tab of the same character updates at once, and refreshes the
 * list and the history.
 */
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

/** Reads a chosen file as JSON, refusing what is clearly not an export before it reaches the server. */
export async function readCharacterFile(file: File): Promise<unknown> {
  if (file.size > IMPORT_MAX_BYTES) throw new Error('O arquivo passa de 12 MB.');
  try {
    return JSON.parse(await file.text());
  } catch {
    throw new Error('O arquivo não é um personagem exportado (JSON inválido).');
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
