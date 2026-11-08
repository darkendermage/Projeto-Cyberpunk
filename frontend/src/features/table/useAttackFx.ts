import { useCallback, useEffect, useRef, type RefObject } from 'react';
import type { SceneView, TableAttack } from '../../api/tableTypes';
import { useEffectsSettings } from '../../state/EffectsContext';
import type { BoardHandle } from './board/TableBoard';
import { effectKind, planEffect } from './board/effects';
import { playAttack } from './sound';
import type { TablePrefs } from './tablePrefs';

// Motion only when the app's effects allow it, sound only when this person has it on.
export function useAttackFx(board: RefObject<BoardHandle | null>, prefs: TablePrefs) {
  const { settings } = useEffectsSettings();
  const current = useRef({ motion: settings.motion, sound: prefs.sound });
  useEffect(() => { current.current = { motion: settings.motion, sound: prefs.sound }; });
  return useCallback((attack: TableAttack, scene: SceneView | null | undefined, queue: readonly { id: string; characterId: number | null }[]) => {
    if (!scene || scene.encounterId !== attack.encounterId || attack.undone) return;
    const plan = planEffect(attack, scene.tokens, queue);
    if (plan) board.current?.showAttack(plan, current.current.motion);
    if (current.current.sound) playAttack(effectKind(attack));
  }, [board]);
}
