import { useCallback, useState } from 'react';

// UI convenience kept for the browser session (open sub-tab, open sections). Storage can be blocked, so every
// access is guarded and the default is used instead.
function read<T>(key: string, fallback: T): T {
  try {
    const raw = sessionStorage.getItem(key);
    return raw === null ? fallback : (JSON.parse(raw) as T);
  } catch {
    return fallback;
  }
}

export function useSessionState<T>(key: string, fallback: T): [T, (next: T) => void] {
  const [value, setValue] = useState<T>(() => read(key, fallback));
  const set = useCallback((next: T) => {
    setValue(next);
    try { sessionStorage.setItem(key, JSON.stringify(next)); } catch { /* storage blocked: keep it in memory */ }
  }, [key]);
  return [value, set];
}
