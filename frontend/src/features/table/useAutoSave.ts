import { useEffect, useRef, useState } from 'react';
import { createSaveQueue, type SaveStatus } from './saveQueue';

// Saves a form by itself a moment after the last change (no "Salvar" button). The first render and values
// equal to the last one saved don't save; leaving the editor saves what is still waiting.
export function useAutoSave<T>(value: T, save: (value: T) => Promise<unknown>, { delay = 600, enabled = true } = {}): SaveStatus {
  const [status, setStatus] = useState<SaveStatus>('idle');
  const saveRef = useRef(save);
  useEffect(() => { saveRef.current = save; });
  const queue = useRef<ReturnType<typeof createSaveQueue<T>> | null>(null);
  queue.current ??= createSaveQueue<T>((v) => saveRef.current(v), setStatus);
  const lastKey = useRef(JSON.stringify(value));
  const waiting = useRef<{ value: T } | null>(null);

  useEffect(() => {
    const key = JSON.stringify(value);
    if (!enabled || key === lastKey.current) return;
    waiting.current = { value };
    const timer = setTimeout(() => {
      lastKey.current = key;
      waiting.current = null;
      void queue.current!.push(value);
    }, delay);
    return () => clearTimeout(timer);
  }, [value, enabled, delay]);

  // Closing the inspector right after a change still saves it.
  useEffect(() => () => { if (waiting.current) void queue.current!.push(waiting.current.value); }, []);

  return status;
}

export const SAVE_STATUS_LABEL: Record<SaveStatus, [string, string]> = {
  idle: ['As mudanças salvam sozinhas.', 'Changes save by themselves.'],
  saving: ['Salvando…', 'Saving…'],
  saved: ['Salvo.', 'Saved.'],
  error: ['Não salvou — veja o aviso.', 'Not saved — see the notice.'],
};
