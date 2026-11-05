import { t } from '../../../i18n';

export type BarrierPattern = 'plates' | 'blocks' | 'grain' | 'glass' | 'plain';

export interface BarrierLook { pattern: BarrierPattern; base: number; line: number; alpha: number }

/** The drawn texture of each material of the cover table (CP:R p.182): no image files, just lines on the canvas. */
export function barrierLook(material: string): BarrierLook {
  switch (material) {
    case 'steel': return { pattern: 'plates', base: 0x5d6873, line: 0xb8c4cf, alpha: 0.95 };
    case 'stone': return { pattern: 'blocks', base: 0x6e6457, line: 0x3b352d, alpha: 0.95 };
    case 'concrete': return { pattern: 'blocks', base: 0x7d7d7d, line: 0x4a4a4a, alpha: 0.95 };
    case 'wood': return { pattern: 'grain', base: 0x7a4f27, line: 0xb07a45, alpha: 0.95 };
    case 'bulletproof-glass': return { pattern: 'glass', base: 0x7fd4e8, line: 0xe8fbff, alpha: 0.35 };
    default: return { pattern: 'plain', base: 0xb9b1a2, line: 0x8a8274, alpha: 0.9 };
  }
}

/** How much of the barrier is left; at 0 HP it no longer stops anything (CP:R p.183). */
export function barrierState(b: { hp: number; maxHp: number }) {
  const ratio = b.maxHp > 0 ? Math.max(0, Math.min(1, b.hp / b.maxHp)) : 0;
  return { ratio, destroyed: b.hp <= 0, text: b.hp <= 0 ? t('destruída', 'destroyed') : `${b.hp}/${b.maxHp} HP` };
}
