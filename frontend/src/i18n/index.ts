// English for the Portuguese texts the server sends as data; keys are the exact Portuguese text (built by tools/i18n).
import DATA_EN_JSON from './data-en.json';

const DATA_EN = DATA_EN_JSON as Record<string, string>;

import { pickLang, type Lang } from './lang';

export type { Lang };

const STORAGE_KEY = 'ncdb.lang';

function stored(): Lang {
  let value: string | null = null;
  try {
    value = localStorage.getItem(STORAGE_KEY);
  } catch {
    /* storage unavailable: the browser's language applies */
  }
  // Only a browser has a meaningful language (Node's navigator follows the OS, and tests expect English).
  return pickLang(value, typeof window === 'undefined' ? undefined : navigator.language);
}

// Read once at startup. Changing it saves and reloads, so module-level constants get rebuilt too.
export const lang: Lang = stored();

/** BCP 47 tag for Intl formatting and the Accept-Language header. */
export const locale = lang === 'pt' ? 'pt-BR' : 'en-US';

if (typeof document !== 'undefined') document.documentElement.lang = locale;

export function setLang(next: Lang) {
  if (next === lang) return;
  try {
    localStorage.setItem(STORAGE_KEY, next);
  } catch {
    /* storage unavailable: the choice lasts only for this page */
  }
  window.location.reload();
}

/** Interface text: the Portuguese and the English version side by side. */
export const t = (pt: string, en: string): string => (lang === 'pt' ? pt : en);

// Server data (rule files, catalog effects, labels) is stored in Portuguese; the English comes from the
// data dictionary.
export function tv<T>(text: T): T {
  if (lang === 'pt' || typeof text !== 'string') return text;
  return (DATA_EN[text] ?? text) as T;
}

/** Skill names: the Portuguese label in PT-BR, the book's English name otherwise. */
export const skillName = (name: string, namePt?: string | null): string => (lang === 'pt' ? namePt ?? name : name);
