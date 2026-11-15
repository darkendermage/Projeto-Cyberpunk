// A health bar with the held shield's HP as a second color after the HP (CP:R p.184): both share one scale, so a
// full shield shows how much more the character can take before HP starts dropping.
export interface HpBar { hpPct: number; shieldPct: number }

export function hpBar(hp: number, maxHp: number, shieldHp?: number | null, shieldMax?: number | null): HpBar {
  const shield = Math.max(0, shieldHp ?? 0);
  const scale = Math.max(1, maxHp + (shield > 0 ? Math.max(shieldMax ?? shield, shield) : 0));
  const hpPct = Math.max(0, Math.min(100, (Math.max(0, hp) / scale) * 100));
  return { hpPct, shieldPct: Math.max(0, Math.min(100 - hpPct, (shield / scale) * 100)) };
}
