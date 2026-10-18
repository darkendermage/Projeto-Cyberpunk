import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { readStorage, writeStorage } from '../lib/hooks';

export interface EffectsSettings {
  scanlines: boolean;
  glitch: boolean;
  motion: boolean;
  /** 0..1.5 multiplier for neon glows */
  glow: number;
  cityArt: boolean;
  density: 'comfortable' | 'compact';
}

const STORAGE_KEY = 'ncdb.effects';

const prefersReducedMotion = () =>
  typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

const defaults = (): EffectsSettings => ({
  scanlines: true,
  glitch: !prefersReducedMotion(),
  motion: !prefersReducedMotion(),
  glow: 1,
  cityArt: true,
  density: 'comfortable',
});

interface EffectsContextValue {
  settings: EffectsSettings;
  update: (patch: Partial<EffectsSettings>) => void;
  reset: () => void;
}

const EffectsContext = createContext<EffectsContextValue | null>(null);

/** Visual effects are opt-out and applied as attributes on <html>, so CSS alone reacts to them. */
export function EffectsProvider({ children }: { children: ReactNode }) {
  const [settings, setSettings] = useState<EffectsSettings>(() => readStorage(STORAGE_KEY, defaults()));

  useEffect(() => {
    const root = document.documentElement;
    root.dataset.scanlines = settings.scanlines ? 'on' : 'off';
    root.dataset.glitch = settings.glitch ? 'on' : 'off';
    root.dataset.motion = settings.motion ? 'on' : 'off';
    root.dataset.density = settings.density;
    root.style.setProperty('--glow-strength', String(settings.glow));
    writeStorage(STORAGE_KEY, settings);
  }, [settings]);

  const value = useMemo<EffectsContextValue>(() => ({
    settings,
    update: (patch) => setSettings((s) => ({ ...s, ...patch })),
    reset: () => setSettings(defaults()),
  }), [settings]);

  return <EffectsContext.Provider value={value}>{children}</EffectsContext.Provider>;
}

export function useEffectsSettings() {
  const ctx = useContext(EffectsContext);
  if (!ctx) throw new Error('useEffectsSettings must be used inside EffectsProvider');
  return ctx;
}
