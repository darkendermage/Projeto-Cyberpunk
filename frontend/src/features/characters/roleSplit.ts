import type { RoleSplitRule } from '../../api/characterTypes';
import { t } from '../../i18n';

/** The total a split may use at this Rank: the Rank, or twice it for the Tech's specialties (CP:R p.147). */
export const splitTotal = (rule: RoleSplitRule, rank: number) => (rule.max === '2xrank' ? rank * 2 : rank);

// The −/+ buttons of a part: one point at a time, or the next allowed value (Fumble Recovery 0 ↔ 4); never below 0,
// above the part's cap or past the points still free. Returns the current value when it can't move.
export function stepSplit(current: number, dir: 1 | -1, opts: { max: number; free: number; allowed?: readonly number[] }): number {
  const ceiling = Math.min(opts.max, current + Math.max(0, opts.free));
  if (opts.allowed?.length) {
    const sorted = [...opts.allowed].sort((a, b) => a - b);
    const next = dir > 0 ? sorted.find((v) => v > current) : [...sorted].reverse().find((v) => v < current);
    return next === undefined || next > ceiling ? current : next;
  }
  return Math.max(0, Math.min(ceiling, current + dir));
}

/** One Combat Awareness effect as the table shows it; <c>used</c> is set for the once-a-Round ones. */
export interface AwarenessEffect { key: string; label: string; value: string; detail: string; used?: boolean }

// CP:R p.146, fixed order, parts with no effect left out. flags = once-a-Round effects already used.
export function awarenessEffects(split: Record<string, number>, flags: readonly string[]): AwarenessEffect[] {
  const p = (k: string) => split[k] ?? 0;
  const out: AwarenessEffect[] = [];
  const precision = Math.min(3, Math.floor(p('precisionAttack') / 3));
  if (precision > 0) out.push({ key: 'precisionAttack', label: 'Precision Attack', value: `+${precision}`, detail: t('nos ataques', 'to attacks') });
  const deflect = Math.min(5, Math.floor(p('damageDeflection') / 2));
  if (deflect > 0) out.push({ key: 'damageDeflection', label: 'Damage Deflection', value: `−${deflect}`, detail: t('no primeiro dano da Rodada', 'off the first damage of the Round'), used: flags.includes('damageDeflection') });
  if (p('spotWeakness') > 0) out.push({ key: 'spotWeakness', label: 'Spot Weakness', value: `+${p('spotWeakness')}`, detail: t('no dano do primeiro acerto da Rodada', 'to the damage of the first hit of the Round'), used: flags.includes('spotWeakness') });
  if (p('initiativeReaction') > 0) out.push({ key: 'initiativeReaction', label: 'Initiative Reaction', value: `+${p('initiativeReaction')}`, detail: t('na Iniciativa', 'to Initiative') });
  if (p('fumbleRecovery') >= 4) out.push({ key: 'fumbleRecovery', label: 'Fumble Recovery', value: '✓', detail: t('ignora falhas críticas ao atacar', 'ignores critical failures when attacking') });
  if (p('threatDetection') > 0) out.push({ key: 'threatDetection', label: 'Threat Detection', value: `+${p('threatDetection')}`, detail: t('em Perception', 'to Perception') });
  return out;
}
