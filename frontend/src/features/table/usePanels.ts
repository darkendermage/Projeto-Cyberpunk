import { useCallback, useState } from 'react';
import { readPanels, savePanels, type PanelLimits } from './panels';

/** Panel sizes that survive a reload in this browser. */
export function usePanels<L extends Record<string, PanelLimits>>(key: string, limits: L) {
  const [panels, setPanels] = useState(() => readPanels(key, limits));
  const update = useCallback((change: Partial<typeof panels>) => setPanels((p) => {
    const next = { ...p, ...change };
    savePanels(key, next);
    return next;
  }), [key]);
  return [panels, update] as const;
}
