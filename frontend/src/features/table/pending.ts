import type { PendingView } from '../../api/encounterTypes';
import { t } from '../../i18n';

export type PendingAction = 'dodge' | 'noDodge' | 'critical' | 'cancel';

/**
 * What a screen offers for a pending attack: the defending player chooses to dodge or not, the attacker's player rolls
 * the Critical Injury, the GM may take any step and cancel. Everyone else only sees that the fight waits.
 */
export function pendingPrompt(p: PendingView, mine: boolean, gm: boolean): { title: string; actions: PendingAction[] } | null {
  const step: PendingAction[] = p.step === 'Defense' ? ['dodge', 'noDodge'] : ['critical'];
  const title = p.step === 'Defense'
    ? t(`${p.attackerName} atira em ${p.targetName} com ${p.weapon}`, `${p.attackerName} shoots at ${p.targetName} with ${p.weapon}`)
    : t(`Dois 6 no dano de ${p.attackerName} em ${p.targetName}: Critical Injury!`, `Two 6s on ${p.attackerName}'s damage to ${p.targetName}: Critical Injury!`);
  if (gm) return { title, actions: [...step, 'cancel'] };
  if (mine) return { title, actions: step };
  return null;
}
