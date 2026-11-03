import { API_BASE, ApiError, request } from './client';
import { locale, t } from '../i18n';
import type {
  ClaimResult, Lobby, MediaInfo, PlayerCharacter, PlayerEncounter, PlayerSeat, PlayerState, RollRequest, SceneGrid, SceneSummary, SceneView, Seat,
  SendMessage, TableMessage, TemplateInput, TokenInput, TokenMove, TunnelStatus,
} from './tableTypes';

const json = (body: unknown) => JSON.stringify(body);

/** The GM's side of the virtual table (only on the GM's port). */
export const tableApi = {
  scenes: () => request<SceneSummary[]>('/table/scenes'),
  scene: (id: number, signal?: AbortSignal) => request<SceneView>(`/table/scenes/${id}`, { signal }),
  createScene: (name: string) => request<SceneView>('/table/scenes', { method: 'POST', body: json({ name }) }),
  updateScene: (id: number, body: { version: number; name: string; grid: SceneGrid; backgroundMediaId: string | null; backgroundWidth: number; backgroundHeight: number; encounterId: number | null }) =>
    request<SceneView>(`/table/scenes/${id}`, { method: 'PUT', body: json(body) }),
  duplicateScene: (id: number) => request<SceneView>(`/table/scenes/${id}/duplicate`, { method: 'POST' }),
  deleteScene: (id: number) => request<void>(`/table/scenes/${id}`, { method: 'DELETE' }),
  activateScene: (id: number) => request<SceneView>(`/table/scenes/${id}/activate`, { method: 'POST' }),
  reorderScenes: (ids: number[]) => request<SceneSummary[]>('/table/scenes/reorder', { method: 'POST', body: json({ ids }) }),
  addToken: (id: number, token: TokenInput) => request<SceneView>(`/table/scenes/${id}/tokens`, { method: 'POST', body: json(token) }),
  addEncounterTokens: (id: number) => request<SceneView>(`/table/scenes/${id}/encounter-tokens`, { method: 'POST' }),
  /** A new fight from the chosen tokens, linked to the scene. */
  startFight: (id: number, body: { name: string | null; tokens: { tokenId: string; side: 'Ally' | 'Enemy' }[] }) =>
    request<SceneView>(`/table/scenes/${id}/fight`, { method: 'POST', body: json(body) }),
  updateToken: (id: number, tokenId: string, token: TokenInput) =>
    request<SceneView>(`/table/scenes/${id}/tokens/${encodeURIComponent(tokenId)}`, { method: 'PUT', body: json(token) }),
  moveToken: (id: number, tokenId: string, move: TokenMove) =>
    request<SceneView>(`/table/scenes/${id}/tokens/${encodeURIComponent(tokenId)}/move`, { method: 'POST', body: json(move) }),
  removeToken: (id: number, tokenId: string) => request<SceneView>(`/table/scenes/${id}/tokens/${encodeURIComponent(tokenId)}`, { method: 'DELETE' }),
  addTemplate: (id: number, tpl: TemplateInput) => request<SceneView>(`/table/scenes/${id}/templates`, { method: 'POST', body: json(tpl) }),
  updateTemplate: (id: number, templateId: string, tpl: TemplateInput) =>
    request<SceneView>(`/table/scenes/${id}/templates/${encodeURIComponent(templateId)}`, { method: 'PUT', body: json(tpl) }),
  removeTemplate: (id: number, templateId: string) =>
    request<SceneView>(`/table/scenes/${id}/templates/${encodeURIComponent(templateId)}`, { method: 'DELETE' }),

  seats: () => request<Seat[]>('/table/seats'),
  onlineSeats: () => request<number[]>('/table/seats/online'),
  createSeat: (label: string, characterId: number | null) => request<Seat>('/table/seats', { method: 'POST', body: json({ label, characterId }) }),
  updateSeat: (id: number, body: { label: string; characterId: number | null; color: string }) =>
    request<Seat>(`/table/seats/${id}`, { method: 'PUT', body: json(body) }),
  revokeSeat: (id: number) => request<Seat>(`/table/seats/${id}/revoke`, { method: 'POST' }),
  /** Gives the seat back to the entrance with a new key; whoever held it is thrown out. */
  releaseSeat: (id: number) => request<Seat>(`/table/seats/${id}/release`, { method: 'POST' }),
  lobby: () => request<{ open: boolean }>('/table/lobby'),
  setLobby: (open: boolean) => request<{ open: boolean }>('/table/lobby', { method: 'PUT', body: json({ open }) }),

  messages: () => request<TableMessage[]>('/table/messages'),
  send: (body: SendMessage) => request<TableMessage>('/table/messages', { method: 'POST', body: json(body) }),
  roll: (body: RollRequest) => request<TableMessage>('/table/rolls', { method: 'POST', body: json(body) }),
  clearMessages: () => request<void>('/table/messages', { method: 'DELETE' }),

  tunnel: () => request<TunnelStatus>('/table/tunnel'),
  startTunnel: () => request<TunnelStatus>('/table/tunnel/start', { method: 'POST' }),
  stopTunnel: () => request<TunnelStatus>('/table/tunnel/stop', { method: 'POST' }),
  downloadTunnel: () => request<TunnelStatus>('/table/tunnel/download', { method: 'POST' }),

  /** Multipart upload of a map or token image (already shrunk by prepareImage). */
  upload: async (file: File): Promise<MediaInfo> => {
    const form = new FormData();
    form.append('file', file);
    let response: Response;
    try {
      response = await fetch(`${API_BASE}/table/media`, { method: 'POST', body: form, headers: { 'Accept-Language': locale } });
    } catch {
      throw new ApiError(0, { title: t('Não deu para enviar a imagem. Verifique se o programa está aberto.', 'Couldn\'t upload the image. Check that the program is open.') });
    }
    if (!response.ok) {
      let problem = { title: t(`Erro ${response.status}`, `Error ${response.status}`) } as { title?: string; errors?: Record<string, string[]> };
      try { problem = await response.json(); } catch { /* non-JSON */ }
      throw new ApiError(response.status, problem);
    }
    return response.json() as Promise<MediaInfo>;
  },
};

/** The general link's entrance: no seat key yet. */
export const lobbyApi = {
  list: () => request<Lobby>('/table/play/lobby'),
  claim: (seatId: number) => request<ClaimResult>(`/table/play/lobby/${seatId}/claim`, { method: 'POST' }),
  portraitUrl: (seatId: number) => `${API_BASE}/table/play/lobby/${seatId}/portrait`,
};

/** The player's side: every call carries the seat link token. */
export function playerApi(seat: string) {
  const headers = { 'X-Seat': seat };
  return {
    me: () => request<PlayerSeat>('/table/play/me', { headers }),
    state: () => request<PlayerState>('/table/play/state', { headers }),
    encounter: () => request<PlayerEncounter | undefined>('/table/play/encounter', { headers }).then((e) => e ?? null),
    character: () => request<PlayerCharacter | undefined>('/table/play/character', { headers }).then((c) => c ?? null),
    messages: () => request<TableMessage[]>('/table/play/messages', { headers }),
    send: (body: SendMessage) => request<TableMessage>('/table/play/messages', { method: 'POST', body: json(body), headers }),
    roll: (body: RollRequest) => request<TableMessage>('/table/play/rolls', { method: 'POST', body: json(body), headers }),
    moveToken: (tokenId: string, move: TokenMove) =>
      request<SceneView>(`/table/play/tokens/${encodeURIComponent(tokenId)}/move`, { method: 'POST', body: json(move), headers }),
    /** The player's own token on the live scene (one per seat). */
    placeToken: (x: number, y: number) => request<SceneView>('/table/play/my-token', { method: 'POST', body: json({ x, y }), headers }),
    addTemplate: (tpl: TemplateInput) => request<SceneView>('/table/play/templates', { method: 'POST', body: json(tpl), headers }),
    updateTemplate: (templateId: string, tpl: TemplateInput) =>
      request<SceneView>(`/table/play/templates/${encodeURIComponent(templateId)}`, { method: 'PUT', body: json(tpl), headers }),
    removeTemplate: (templateId: string) =>
      request<SceneView>(`/table/play/templates/${encodeURIComponent(templateId)}`, { method: 'DELETE', headers }),
  };
}

export type PlayerApi = ReturnType<typeof playerApi>;

export const tableKeys = {
  scenes: ['table', 'scenes'] as const,
  scene: (id: number) => ['table', 'scene', id] as const,
  seats: ['table', 'seats'] as const,
  online: ['table', 'online'] as const,
  messages: ['table', 'messages'] as const,
  tunnel: ['table', 'tunnel'] as const,
  lobby: ['table', 'lobby'] as const,
  lobbyStatus: ['table', 'lobby-status'] as const,
  player: (seat: string) => ['table', 'player', seat] as const,
  playerEncounter: (seat: string) => ['table', 'player', seat, 'encounter'] as const,
  playerCharacter: (seat: string) => ['table', 'player', seat, 'character'] as const,
  playerMessages: (seat: string) => ['table', 'player', seat, 'messages'] as const,
};
