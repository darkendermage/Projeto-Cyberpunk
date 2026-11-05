import { describe, expect, it } from 'vitest';
import { pickLang } from './lang';

describe('pickLang', () => {
  it('keeps the language the person chose', () => {
    expect(pickLang('en', 'pt-BR')).toBe('en');
    expect(pickLang('pt', 'en-US')).toBe('pt');
  });

  it('follows the browser on a first visit (a player\'s phone in Portuguese gets Portuguese)', () => {
    expect(pickLang(null, 'pt-BR')).toBe('pt');
    expect(pickLang(null, 'pt-PT')).toBe('pt');
    expect(pickLang(null, 'es-AR')).toBe('en');
    expect(pickLang('garbage', undefined)).toBe('en');
  });
});
