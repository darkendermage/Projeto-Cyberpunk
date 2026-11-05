export type Lang = 'pt' | 'en';

/** The language to start in: the one the person chose, else the browser's (Portuguese for any pt-*), else English. */
export function pickLang(stored: string | null, browser: string | undefined): Lang {
  if (stored === 'pt' || stored === 'en') return stored;
  return browser?.toLowerCase().startsWith('pt') ? 'pt' : 'en';
}
