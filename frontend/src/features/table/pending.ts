import type { PendingView } from '../../api/encounterTypes';
import { t } from '../../i18n';

export type PendingAction = 'dodge' | 'noDodge' | 'critical' | 'cancel';

// Defender picks dodge or not, attacker rolls the crit, GM can do any step or cancel. Everyone else just waits.
export function pendingPrompt(p: PendingView, mine: boolean, gm: boolean): { title: string; actions: PendingAction[] } | null {
  const step: PendingAction[] = p.step === 'Defense' ? ['dodge', 'noDodge'] : ['critical'];
  const title = p.step === 'Defense'
    ? t(`${p.attackerName} atira em ${p.targetName} com ${p.weapon}`, `${p.attackerName} shoots at ${p.targetName} with ${p.weapon}`)
    : t(`Dois 6 no dano de ${p.attackerName} em ${p.targetName}: Critical Injury!`, `Two 6s on ${p.attackerName}'s damage to ${p.targetName}: Critical Injury!`);
  if (gm) return { title, actions: [...step, 'cancel'] };
  if (mine) return { title, actions: step };
  return null;
}
