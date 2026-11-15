// One save in flight at a time; values pushed meanwhile collapse into the latest, so a slow request never
// lands after a newer one and typing fast doesn't fire a request per keystroke.
export type SaveStatus = 'idle' | 'saving' | 'saved' | 'error';

export interface SaveQueue<T> {
  push: (value: T) => Promise<void>;
  /** Saves still waiting (the one in flight included). */
  busy: () => boolean;
}

export function createSaveQueue<T>(save: (value: T) => Promise<unknown>, onStatus: (status: SaveStatus) => void = () => {}): SaveQueue<T> {
  let pending: { value: T } | null = null;
  let running: Promise<void> | null = null;

  const drain = async () => {
    while (pending) {
      const { value } = pending;
      pending = null;
      onStatus('saving');
      try {
        await save(value);
        onStatus(pending ? 'saving' : 'saved');
      } catch {
        onStatus('error');
      }
    }
  };

  return {
    push: (value) => {
      pending = { value };
      running ??= drain().finally(() => { running = null; });
      return running;
    },
    busy: () => running != null,
  };
}
