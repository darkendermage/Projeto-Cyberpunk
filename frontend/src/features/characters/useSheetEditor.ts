import { useCallback, useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../../api/client';
import { characterApi, characterKeys, problemMessage, useStoreCharacter } from '../../api/characters';
import type { CharacterDetail, CharacterSheet } from '../../api/characterTypes';
import { useToast } from '../../state/ToastContext';

export type SaveStatus = 'saved' | 'pending' | 'saving' | 'error';

const AUTOSAVE_DELAY = 700;

/**
 * Local copy of the editable sheet with queued autosave. Edits show at once; saves go out one at a time, each with
 * the version returned by the previous one, so consecutive edits never conflict with themselves. When the server
 * answers (a save or any rule command) and nothing is waiting to be saved, the local copy adopts the server sheet.
 */
export function useSheetEditor(detail: CharacterDetail) {
  const toast = useToast();
  const client = useQueryClient();
  const store = useStoreCharacter();
  const [draft, setDraft] = useState(detail.sheet);
  const [status, setStatus] = useState<SaveStatus>('saved');
  const draftRef = useRef(detail.sheet);
  const versionRef = useRef(detail.version);
  const pendingRef = useRef<CharacterSheet | null>(null);
  const savingRef = useRef<Promise<void> | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout>>();
  const id = detail.id;

  useEffect(() => {
    versionRef.current = detail.version;
    if (!pendingRef.current && !savingRef.current) {
      draftRef.current = detail.sheet;
      setDraft(detail.sheet);
    }
  }, [detail]);

  const flush = useCallback(async (): Promise<void> => {
    clearTimeout(timerRef.current);
    if (savingRef.current) {
      await savingRef.current;
      if (pendingRef.current) return flush();
      return;
    }
    const sheet = pendingRef.current;
    if (!sheet) return;
    pendingRef.current = null;
    setStatus('saving');
    const run = (async () => {
      try {
        const saved = await characterApi.save(id, versionRef.current, sheet);
        versionRef.current = saved.version;
        store(saved);
        setStatus(pendingRef.current ? 'pending' : 'saved');
      } catch (error) {
        setStatus('error');
        toast.notify(problemMessage(error), 'error');
        if (error instanceof ApiError && error.status === 409) {
          pendingRef.current = null;
          await client.invalidateQueries({ queryKey: characterKeys.detail(id) });
        }
      }
    })();
    savingRef.current = run;
    await run;
    savingRef.current = null;
    if (pendingRef.current) await flush();
  }, [client, id, store, toast]);

  const update = useCallback((change: (sheet: CharacterSheet) => CharacterSheet) => {
    const next = change(draftRef.current);
    draftRef.current = next;
    pendingRef.current = next;
    setDraft(next);
    setStatus('pending');
    clearTimeout(timerRef.current);
    timerRef.current = setTimeout(() => void flush(), AUTOSAVE_DELAY);
  }, [flush]);

  // Ctrl+S saves immediately; leaving the page with unsaved edits asks first.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
        e.preventDefault();
        void flush();
      }
    };
    const onLeave = (e: BeforeUnloadEvent) => {
      if (pendingRef.current || savingRef.current) e.preventDefault();
    };
    window.addEventListener('keydown', onKey);
    window.addEventListener('beforeunload', onLeave);
    return () => {
      window.removeEventListener('keydown', onKey);
      window.removeEventListener('beforeunload', onLeave);
      // Navigating inside the app unmounts the editor: send what is waiting instead of dropping it.
      if (pendingRef.current) void flush();
      else clearTimeout(timerRef.current);
    };
  }, [flush]);

  return { draft, update, flush, status };
}

export type SheetEditor = ReturnType<typeof useSheetEditor>;
