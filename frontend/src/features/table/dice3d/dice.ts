import type { TableMessage } from '../../../api/tableTypes';
import { t } from '../../../i18n';

/** One die of a throw: CP:R only rolls d6 and d10. */
export interface DieRoll { sides: 6 | 10; value: number }

/** What the dice tray shows for a chat line: the dice landing on what the server rolled, then the total. */
export interface DiceThrow { id: number; dice: DieRoll[]; total: number; label: string; critical: 'Success' | 'Failure' | null; color?: string | null }

/** Numbers that read on a die of this color: dark on light dice, light on dark ones (relative luminance). */
export function inkFor(hex: string): '#0b0d10' | '#f4f6f8' {
  const n = Number.parseInt(hex.replace('#', ''), 16);
  const lin = (c: number) => { const v = c / 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; };
  const l = 0.2126 * lin((n >> 16) & 255) + 0.7152 * lin((n >> 8) & 255) + 0.0722 * lin(n & 255);
  return l > 0.5 ? '#0b0d10' : '#f4f6f8';
}

/** The tray stays readable: past this the rest still counts in the total. */
export const MAX_DICE = 12;

/**
 * The dice of a roll or an attack line, as this reader sees it; null when there is nothing to throw (a concealed
 * secret roll, a formula without d6 or d10, an attack whose numbers the reader may not see).
 */
export function throwOf(m: TableMessage): DiceThrow | null {
  const dice: DieRoll[] = [];
  const add = (sides: number, value: number) => { if ((sides === 6 || sides === 10) && dice.length < MAX_DICE) dice.push({ sides, value }); };
  if (m.kind === 'Roll' && m.roll && !m.concealed) {
    for (const term of m.roll.terms) {
      const sides = Number(term.expression.split('d')[1]);
      term.dice.forEach((d) => add(sides, d));
      // A Check's critical rolls one more d10 (CP:R p.130).
      if (term.extra != null) add(10, Math.abs(term.extra));
    }
    return dice.length ? { id: m.id, dice, total: m.roll.total, label: m.text, critical: m.roll.critical, color: m.roll.diceColor ?? null } : null;
  }
  if (m.kind === 'Attack' && m.attack && !m.attack.undone) {
    const a = m.attack;
    if (a.attackRoll != null) add(10, a.attackRoll);
    // The extra d10 of a natural 10 or 1 (CP:R p.130).
    if (a.attackExtra != null) add(10, Math.abs(a.attackExtra));
    // The Critical Injury's 2d6 (CP:R p.187), rolled by the attacker.
    a.critDice?.forEach((d) => add(6, d));
    a.targets.find((target) => target.hit && target.damageDice.length > 0)?.damageDice.forEach((d) => add(6, d));
    return dice.length ? { id: m.id, dice, total: a.attackTotal, label: t(`Ataque: ${a.weapon}`, `Attack: ${a.weapon}`), critical: null, color: a.diceColor ?? null } : null;
  }
  return null;
}
