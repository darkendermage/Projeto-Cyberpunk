import { useState } from 'react';
import type { QuickAttackResult } from '../../api/tableTypes';
import { aimAt, type Aim } from './aim';

// fire sends it (player or GM route); results and errors go back to the page.
export function useQuickAttack(fire: (aim: Aim, targetId: string) => Promise<QuickAttackResult>, onDone: (result: QuickAttackResult) => void, onError: (error: unknown) => void) {
  const [aim, setAim] = useState<Aim | null>(null);
  const [busy, setBusy] = useState(false);
  const shoot = async () => {
    if (!aim?.targetId || busy) return;
    setBusy(true);
    try {
      const attack = await fire(aim, aim.targetId);
      setAim(null);
      onDone(attack);
    } catch (e) {
      onError(e);
    } finally {
      setBusy(false);
    }
  };
  return {
    aim, busy, shoot,
    start: (next: Aim) => setAim(next),
    target: (fighterId: string | null) => setAim((a) => (a ? aimAt(a, fighterId) : a)),
    mode: (mode: Aim['mode']) => setAim((a) => (a ? { ...a, mode } : a)),
    cancel: () => setAim(null),
  };
}
