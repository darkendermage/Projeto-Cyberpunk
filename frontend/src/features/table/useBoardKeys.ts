import { useEffect, useRef } from 'react';

/** What the board's keys do: Delete/Backspace clears the selection, Ctrl+A selects all, Esc lets go. */
export interface BoardKeys { onDelete: () => void; onSelectAll: () => void; onClear: () => void }

/** The selection being worked on: tokens and areas by id. */
export interface Selection { tokens: string[]; templates: string[] }
export const NO_SELECTION: Selection = { tokens: [], templates: [] };

/** Board shortcuts, ignored while typing in a field (the chat, a name, a number). */
export function useBoardKeys(keys: BoardKeys) {
  const ref = useRef(keys);
  useEffect(() => { ref.current = keys; });
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const el = e.target as HTMLElement | null;
      if (el && (el.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(el.tagName))) return;
      if (e.key === 'Delete' || e.key === 'Backspace') { e.preventDefault(); ref.current.onDelete(); }
      else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'a') { e.preventDefault(); ref.current.onSelectAll(); }
      else if (e.key === 'Escape') ref.current.onClear();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);
}
