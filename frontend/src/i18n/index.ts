// English for the Portuguese texts the server sends as data; keys are the exact Portuguese text (built by tools/i18n).
import DATA_EN_JSON from './data-en.json';

const DATA_EN = DATA_EN_JSON as Record<string, string>;

export type Lang = 'en' | 'pt';

const STORAGE_KEY = 'ncdb.lang';

function stored(): Lang {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    if (value === 'pt' || value === 'en') return value;
  } catch {
    /* storage unavailable: the default applies */
  }
  return 'en';
}

/**
 * The interface language, read once at startup. Changing it saves the choice and reloads the page, so every text —
 * module-level constants included — is built again in the new language.
 */
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

/**
 * Text that comes from the server data (rule files, catalog effects, field labels). The server keeps it in Portuguese,
 * the canonical form saved in sheets, and the English version comes from the data dictionary.
 */
export function tv<T>(text: T): T {
  if (lang === 'pt' || typeof text !== 'string') return text;
  return (DATA_EN[text] ?? text) as T;
}

/** Skill names: the Portuguese label in PT-BR, the book's English name otherwise. */
export const skillName = (name: string, namePt?: string | null): string => (lang === 'pt' ? namePt ?? name : name);
