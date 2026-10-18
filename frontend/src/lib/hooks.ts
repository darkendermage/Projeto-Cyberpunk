import { useEffect, useRef, useState } from 'react';
import type { RangeUnit } from './rangeTable';

export function useDebouncedValue<T>(value: T, delay = 300): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(handle);
  }, [value, delay]);
  return debounced;
}

export function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState(() => typeof window !== 'undefined' && window.matchMedia(query).matches);
  useEffect(() => {
    const media = window.matchMedia(query);
    const onChange = () => setMatches(media.matches);
    onChange();
    media.addEventListener('change', onChange);
    return () => media.removeEventListener('change', onChange);
  }, [query]);
  return matches;
}

/** Calls the handler when the element scrolls into view (used for progressive loading). */
export function useInView<T extends Element>(onVisible: () => void, enabled: boolean) {
  const ref = useRef<T | null>(null);
  const handler = useRef(onVisible);
  handler.current = onVisible;

  useEffect(() => {
    const node = ref.current;
    if (!node || !enabled) return;
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting)) handler.current();
    }, { rootMargin: '600px 0px' });
    observer.observe(node);
    return () => observer.disconnect();
  }, [enabled]);

  return ref;
}

export function readStorage<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    return raw ? { ...fallback, ...JSON.parse(raw) } : fallback;
  } catch {
    return fallback;
  }
}

export function writeStorage(key: string, value: unknown) {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    /* storage unavailable (private mode) — settings just won't persist */
  }
}

const RANGE_UNIT_KEY = 'ncdb.rangeUnit';

/** Meters or 2 m tiles for range tables, remembered between the Tables page and the weapon sheet. */
export function useRangeUnit(): [RangeUnit, (unit: RangeUnit) => void] {
  const [unit, setUnit] = useState<RangeUnit>(() => readStorage(RANGE_UNIT_KEY, { unit: 'meters' as RangeUnit }).unit);
  return [unit, (value) => { setUnit(value); writeStorage(RANGE_UNIT_KEY, { unit: value }); }];
}
