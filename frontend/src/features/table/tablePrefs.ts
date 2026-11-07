import { useCallback, useState } from 'react';
import { readStorage, writeStorage } from '../../lib/hooks';

/** What each person turns on or off for themselves at the table: the sounds, the 3D dice and the music's volume. */
export interface TablePrefs { sound: boolean; dice3d: boolean; musicVolume: number; musicMuted: boolean }

const KEY = 'ncdb.table.fx';
const DEFAULTS: TablePrefs = { sound: true, dice3d: true, musicVolume: 80, musicMuted: false };

export function readTablePrefs(): TablePrefs {
  return { ...DEFAULTS, ...readStorage<Partial<TablePrefs>>(KEY, {}) };
}

export function useTablePrefs(): [TablePrefs, (patch: Partial<TablePrefs>) => void] {
  const [prefs, setPrefs] = useState(readTablePrefs);
  const update = useCallback((patch: Partial<TablePrefs>) => setPrefs((p) => {
    const next = { ...p, ...patch };
    writeStorage(KEY, next);
    return next;
  }), []);
  return [prefs, update];
}
