import type { AttackMode, WeaponView } from '../../api/encounterTypes';
import type { TableAttack } from '../../api/tableTypes';
import { t } from '../../i18n';
import { modesFor, pickMode, reloadsMain, weaponName } from '../combat/combat';

/** A quick attack being aimed from the cards under the map: who shoots, with what, how, and at whom. */
export interface Aim {
  attackerId: string;
  attackerName: string;
  /** The weapon mode in use (Slug, Shell, Throw...): what the shot works with. */
  weapon: WeaponView;
  /** The weapon with every mode, for the mode picker. */
  base: WeaponView;
  mode: AttackMode;
  targetId: string | null;
}

/** Area attacks need a template on the map: they stay on the combat table's full form. */
export const quickModes = (weapon: WeaponView): AttackMode[] => modesFor(weapon).filter((m) => m !== 'Explosive' && m !== 'Suppressive');

export function startAim(attackerId: string, attackerName: string, weapon: WeaponView): Aim {
  return { attackerId, attackerName, weapon, base: weapon, mode: quickModes(weapon)[0] ?? 'Single', targetId: null };
}

/** Another mode of the same weapon; the attack mode goes back to its first one. */
export function aimWith(aim: Aim, modeId: string): Aim {
  const weapon = pickMode(aim.base, modeId);
  return { ...aim, weapon, mode: quickModes(weapon)[0] ?? 'Single' };
}

/** The target picked on the map or on a card; the shooter can't aim at themselves. */
export const aimAt = (aim: Aim, targetId: string | null): Aim => (targetId == null || targetId === aim.attackerId ? aim : { ...aim, targetId });

/** A weapon as a button reads: name, attack total, damage and ammo. */
export function weaponLabel(w: WeaponView): string {
  const ammo = w.rounds != null && w.magazine != null ? ` · ${w.rounds}/${w.magazine}` : '';
  return `${weaponName(w)} +${w.attackTotal} · ${w.damage}${ammo}`;
}

/** Why a weapon can't fire right now, or null when it can. */
export function cannotFire(w: WeaponView): string | null {
  if (w.destroyed) return t('Destruída: não dá mais para usar.', "Destroyed: it can't be used anymore.");
  if (w.jammed) return w.bolt ? t('Ferrolho: gaste uma Ação para preparar o próximo tiro.', 'Bolt action: spend an Action to chamber the next round.') : t('Travada: gaste uma Ação para destravar.', 'Jammed: spend an Action to unjam.');
  if (w.thrown) return t('Arremessada: pegue do chão (ou espere o Targeting Scope trazê-la).', 'Thrown: pick it up (or wait for the Targeting Scope to bring it back).');
  if (!w.melee && w.magazine != null && (w.rounds ?? 0) <= 0) return t('Sem munição: recarregue.', 'Out of ammo: reload.');
  return null;
}

/** The attack total spelled out: "Handgun 12 + Precision Attack 1 + Excellent Quality 1". */
export function attackBreakdown(w: WeaponView): string {
  const parts = w.attackParts ?? [];
  if (parts.length <= 1) return `${w.attackTotal}`;
  return `${w.attackTotal} = ${parts.map((p, i) => (i === 0 ? `${p.label} ${p.value}` : `${p.label} ${p.value >= 0 ? '+' : '−'}${Math.abs(p.value)}`)).join(' + ')}`;
}

/** Why a weapon can't be reloaded, or null when it can (a magazine that isn't full). */
export function canReload(w: WeaponView): string | null {
  if (!reloadsMain(w) || w.magazine == null) return t('Sem pente.', 'No magazine.');
  if ((w.rounds ?? 0) >= w.magazine) return t('Pente cheio.', 'Full magazine.');
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
