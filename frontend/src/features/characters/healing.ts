interface HealthLike { currentHp: number; maxHp: number; woundState: string; bodyForDerived: number }

export interface HealingOptions {
  /** HP left to recover. */
  missing: number;
  /** Quick heal buttons that still fit under the max. */
  quick: number[];
  canHeal: boolean;
  /** A full day of rest heals BODY once stabilized (CP:R p.222). */
  canRest: boolean;
  rest: number;
}

/** What the heal controls may offer: Mortally Wounded needs stabilizing first (CP:R p.186). */
export function healing(h: HealthLike, stabilized: boolean): HealingOptions {
  const missing = Math.max(0, h.maxHp - h.currentHp);
  const canHeal = missing > 0 && h.woundState !== 'Mortally';
  return {
    missing,
    quick: canHeal ? [1, 5].filter((n) => n <= missing) : [],
    canHeal,
    canRest: canHeal && stabilized,
    rest: Math.min(h.bodyForDerived, missing),
  };
}
