import type { AttackMode, WeaponView } from '../../api/encounterTypes';
import type { TableAttack } from '../../api/tableTypes';
import { t } from '../../i18n';
import { modesFor } from '../combat/combat';

/** A quick attack being aimed from the cards under the map: who shoots, with what, how, and at whom. */
export interface Aim {
  attackerId: string;
  attackerName: string;
  weapon: WeaponView;
  mode: AttackMode;
  targetId: string | null;
}

/** Area attacks need a template on the map: they stay on the combat table's full form. */
export const quickModes = (weapon: WeaponView): AttackMode[] => modesFor(weapon).filter((m) => m !== 'Explosive' && m !== 'Suppressive');

export function startAim(attackerId: string, attackerName: string, weapon: WeaponView): Aim {
  return { attackerId, attackerName, weapon, mode: quickModes(weapon)[0] ?? 'Single', targetId: null };
}

/** The target picked on the map or on a card; the shooter can't aim at themselves. */
export const aimAt = (aim: Aim, targetId: string | null): Aim => (targetId == null || targetId === aim.attackerId ? aim : { ...aim, targetId });

/** A weapon as a button reads: name, attack total, damage and ammo. */
export function weaponLabel(w: WeaponView): string {
  const ammo = w.rounds != null && w.magazine != null ? ` · ${w.rounds}/${w.magazine}` : '';
  return `${w.name} +${w.attackTotal} · ${w.damage}${ammo}`;
}

/** Why a weapon can't fire right now, or null when it can. */
export function cannotFire(w: WeaponView): string | null {
  if (!w.melee && w.magazine != null && (w.rounds ?? 0) <= 0) return t('Sem munição: recarregue na ficha.', 'Out of ammo: reload on the sheet.');
  return null;
}

/** The toast after a shot: each target hit (with the damage when the reader may see it) or missed. */
export function attackSummary(a: TableAttack): string {
  return a.targets.map((target) => {
    if (!target.hit) return t(`Errou ${target.name}`, `Missed ${target.name}`);
    if (target.damage == null) return t(`Acertou ${target.name}`, `Hit ${target.name}`);
    const hp = target.through != null ? t(` (${target.through} no HP)`, ` (${target.through} to HP)`) : '';
    return t(`Acertou ${target.name}: ${target.damage} de dano${hp}`, `Hit ${target.name}: ${target.damage} damage${hp}`);
  }).join(' · ');
}

/** The fighter a token or a card stands for: its linked combatant, or the PC of its sheet. */
export function fighterIdOf(link: { combatantId: string | null; characterId: number | null }, queue: readonly { id: string; characterId: number | null }[]): string | null {
  if (link.combatantId && queue.some((f) => f.id === link.combatantId)) return link.combatantId;
  if (link.characterId != null) return queue.find((f) => f.characterId === link.characterId)?.id ?? null;
  return null;
}
