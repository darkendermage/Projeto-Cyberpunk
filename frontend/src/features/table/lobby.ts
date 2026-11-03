import { ApiError } from '../../api/client';
import { problemMessage } from '../../api/characters';
import { t } from '../../i18n';

/** The key of the seat this device picked or opened, so the general link enters it again directly. */
export const SEAT_KEY = 'ncdb.table.seat';

// Storage can be blocked (private windows, cleared site data): the entrance then simply asks again.
export function rememberSeat(token: string) {
  try { localStorage.setItem(SEAT_KEY, token); } catch { /* not remembered */ }
}

export function savedSeat(): string | null {
  try { return localStorage.getItem(SEAT_KEY); } catch { return null; }
}

export function forgetSeat() {
  try { localStorage.removeItem(SEAT_KEY); } catch { /* nothing to forget */ }
}

/** The table refused the key (released or revoked seat); any other failure (tunnel hiccup, no network) keeps it. */
export function isRefusedKey(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401;
}

/** Why picking a seat failed: the GM closed the entrance (404), someone took it first (409), or anything else. */
export function claimErrorMessage(error: unknown): { closed: boolean; text: string } {
  if (error instanceof ApiError && error.status === 404)
    return { closed: true, text: t('A entrada fechou. Peça ao mestre para abrir.', 'The entrance closed. Ask the GM to open it.') };
  if (error instanceof ApiError && error.status === 409)
    return { closed: false, text: t('Esse personagem acabou de ser escolhido. Escolha outro.', 'That character was just taken. Pick another one.') };
  return { closed: false, text: problemMessage(error) };
}
