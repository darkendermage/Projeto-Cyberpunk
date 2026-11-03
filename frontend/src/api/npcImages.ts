import { API_BASE, ApiError, request } from './client';
import { locale, t } from '../i18n';

/** When each NPC's image last changed, by id (saved NPCs: numeric ids as strings; compendium: its ids). */
export type ImageStamps = Record<string, string>;

const url = (path: string, stamp: string) => `${API_BASE}${path}?v=${encodeURIComponent(stamp)}`;

/** Multipart upload of an image already shrunk by preparePortrait (the server keeps up to 2 MB). */
async function putImage(path: string, file: File): Promise<void> {
  const form = new FormData();
  form.append('file', file);
  let response: Response;
  try {
    response = await fetch(`${API_BASE}${path}`, { method: 'PUT', body: form, headers: { 'Accept-Language': locale } });
  } catch {
    // The server closes the connection on an oversized upload, so the browser only sees a network failure.
    throw new ApiError(0, { title: t('Não deu para enviar a imagem. Verifique se ela tem até 2 MB e se o programa está aberto.', 'Couldn\'t upload the image. Check that it is 2 MB or less and that the program is open.') });
  }
  if (!response.ok) {
    let problem = { title: t(`Erro ${response.status}`, `Error ${response.status}`) } as { title?: string; errors?: Record<string, string[]> };
    try { problem = await response.json(); } catch { /* non-JSON */ }
    throw new ApiError(response.status, problem);
  }
}

/** Images of saved NPCs (generator) and of the compendium's named NPCs; the table's tokens show them. */
export const npcImageApi = {
  savedStamps: () => request<ImageStamps>('/npcs/portraits'),
  compendiumStamps: () => request<ImageStamps>('/npc-compendium/portraits'),
  savedUrl: (id: number, stamp: string) => url(`/npcs/${id}/portrait`, stamp),
  compendiumUrl: (id: string, stamp: string) => url(`/npc-compendium/${encodeURIComponent(id)}/portrait`, stamp),
  uploadSaved: (id: number, file: File) => putImage(`/npcs/${id}/portrait`, file),
  uploadCompendium: (id: string, file: File) => putImage(`/npc-compendium/${encodeURIComponent(id)}/portrait`, file),
  removeSaved: (id: number) => request<void>(`/npcs/${id}/portrait`, { method: 'DELETE' }),
  removeCompendium: (id: string) => request<void>(`/npc-compendium/${encodeURIComponent(id)}/portrait`, { method: 'DELETE' }),
};

export const npcImageKeys = {
  saved: ['npc-images', 'saved'] as const,
  compendium: ['npc-images', 'compendium'] as const,
};
