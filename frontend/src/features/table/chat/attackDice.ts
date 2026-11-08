import type { TableAttack } from '../../../api/tableTypes';
import { t } from '../../../i18n';

/** One die on the attack card; <c>extra</c> marks the additional d10 of a natural 10 (+) or 1 (−). */
export interface DieChip { value: number; sides: number; extra?: boolean }
export interface DiceGroup { label: string; dice: DieChip[] }

// In the order the fight rolled them: attack d10 (+ extra), defenses, damage of the hits, crit 2d6.
// Hidden numbers just aren't there.
export function attackDice(a: TableAttack): DiceGroup[] {
  const groups: DiceGroup[] = [];
  if (a.attackRoll != null) {
    const dice: DieChip[] = [{ value: a.attackRoll, sides: 10 }];
    if (a.attackExtra != null) dice.push({ value: a.attackExtra, sides: 10, extra: true });
    groups.push({ label: t('Ataque (d10)', 'Attack (d10)'), dice });
  }
  for (const target of a.targets) {
    if (target.defenseRoll != null) groups.push({ label: t(`Defesa de ${target.name} (d10)`, `${target.name}'s defense (d10)`), dice: [{ value: target.defenseRoll, sides: 10 }] });
    if (target.hit && target.damageDice.length > 0)
      groups.push({ label: t(`Dano em ${target.name}`, `Damage on ${target.name}`), dice: target.damageDice.map((value) => ({ value, sides: 6 })) });
  }
  if (a.critDice?.length) groups.push({ label: 'Critical Injury (2d6)', dice: a.critDice.map((value) => ({ value, sides: 6 })) });
  return groups;
}
