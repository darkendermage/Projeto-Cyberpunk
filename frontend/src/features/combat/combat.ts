// Pure helpers for the combat table: labels and which attack modes a weapon offers. The rules themselves (DVs,
// damage, armor, Critical Injuries) are applied by the server (POST /api/encounters/{id}/attack).
import type { AttackMode, FighterView, WeaponView, WoundState } from '../../api/encounterTypes';
import { t, tv } from '../../i18n';

export const WOUND_LABEL: Record<WoundState, string> = {
  Unhurt: t('Inteiro', 'Unhurt'),
  Lightly: 'Lightly Wounded',
  Seriously: 'Seriously Wounded',
  Mortally: 'Mortally Wounded',
};

export const WOUND_TONE: Record<WoundState, 'default' | 'cyan' | 'amber' | 'red'> = {
  Unhurt: 'cyan',
  Lightly: 'default',
  Seriously: 'amber',
  Mortally: 'red',
};

export const MODE_LABEL: Record<AttackMode, string> = {
  Single: t('Ataque normal', 'Normal attack'),
  Autofire: 'Autofire',
  AimedHead: t('Mirado: cabeça (−8)', 'Aimed: head (−8)'),
  AimedHand: t('Mirado: item na mão (−8)', 'Aimed: held item (−8)'),
  AimedLeg: t('Mirado: perna (−8)', 'Aimed: leg (−8)'),
  ShotgunShell: 'Shotgun Shell',
  Explosive: t('Explosivo (área)', 'Explosive (area)'),
  Suppressive: 'Suppressive Fire',
};

export const CONDITIONS = ['Prone', 'Grappled', 'Inconsciente', 'Escondido'] as const;

export const THROWN = 'thrown';

const EXPLOSIVE_CLASSES = new Set(['GrenadeLauncher', 'RocketLauncher']);

/** The modes a weapon can be used in (CP:R p.170-178): autofire needs an Autofire value, shells need a shotgun…
 *  weapon-rules.json can take some away (no aimed shots, shells only) or add one (a grenade launcher mode). */
export function modesFor(weapon: WeaponView | null, thrown = false): AttackMode[] {
  if (thrown) return ['Single', 'Explosive'];
  if (!weapon) return ['Single'];
  const traits = weapon.traits;
  if (weapon.autofireOnly) return ['Autofire', 'Suppressive'];
  const shells = traits?.shells !== 'none' && (weapon.class === 'Shotgun' || traits?.shells === 'only');
  const modes: AttackMode[] = traits?.shells === 'only' ? [] : ['Single'];
  if (weapon.autofireMultiplier != null) modes.push('Autofire', 'Suppressive');
  const aimable = !weapon.melee || weapon.skill === 'melee-weapon' || weapon.skill.startsWith('martial-arts');
  if (aimable && !traits?.noAimed && traits?.shells !== 'only') modes.push('AimedHead', 'AimedHand', 'AimedLeg');
  if (shells) modes.push('ShotgunShell');
  if (traits?.explosive || (weapon.class && EXPLOSIVE_CLASSES.has(weapon.class))) modes.push('Explosive');
  return modes.length > 0 ? modes : ['Single'];
}

/** The modes an attack can use now: free ones plus the switched mode that's on. */
export const usableModes = (weapon: WeaponView): WeaponView[] => (weapon.modes ?? [weapon]).filter((m) => m.modeActive !== false);

/** Modes that stay on the weapon (switching costs an Action, a Move Action or a minute). */
export const switchedModes = (weapon: WeaponView): WeaponView[] => (weapon.modes ?? []).filter((m) => m.modeSwitch && m.modeSwitch !== 'free');

/** The view an attack with this mode works with (the weapon itself when the id is unknown). */
export const pickMode = (weapon: WeaponView, modeId: string | null | undefined): WeaponView =>
  (modeId ? weapon.modes?.find((m) => m.modeId === modeId) : undefined) ?? weapon;

export const SWITCH_COST: Record<string, string> = {
  action: t('uma Ação', 'an Action'),
  move: t('uma Move Action', 'a Move Action'),
  minute: t('1 minuto', '1 minute'),
};

/** Damage dice to type for this mode: Autofire 2d6, shells 3d6 (or the weapon's own), none for an effect. */
export function expectedDiceFor(weapon: WeaponView | null, mode: AttackMode, thrownDamage = ''): number {
  if (!weapon) return diceCount(thrownDamage || '1d6');
  if (weapon.traits?.effect && mode !== 'Autofire') return 0;
  if (mode === 'Autofire') return 2;
  if (mode === 'ShotgunShell') return diceCount(weapon.traits?.shellDamage ?? '3d6');
  if (mode.startsWith('Aimed') && weapon.traits?.aimedDamage) return diceCount(weapon.traits.aimedDamage);
  return diceCount(weapon.damage);
}

/** "Micro Cutie [Hypurrburst]": the name plus the mode switched on, when the weapon has such modes. */
export const weaponName = (w: WeaponView): string =>
  w.modeLabel && w.modeSwitch && w.modeSwitch !== 'free' ? `${tv(w.name)} [${w.modeLabel}]` : tv(w.name);

/** The main magazine can be reloaded (melee weapons have none, except a stun baton running on a battery). */
export const reloadsMain = (w: WeaponView): boolean =>
  w.magazine != null && w.magazineId == null && (!w.melee || !!w.traits?.meleeCharges);

/** Extra magazines (not the main one), for their own reload buttons. */
export const extraMagazines = (weapon: WeaponView) => (weapon.magazines ?? []).filter((m) => m.id != null);

/** Area attacks hit several targets with one roll of damage. */
export const isArea = (mode: AttackMode) => mode === 'Explosive' || mode === 'ShotgunShell' || mode === 'Suppressive';

/** Melee attacks ignore range; ranged ones need it (except Shotgun Shell, always DV13). */
export const needsRange = (weapon: WeaponView | null, mode: AttackMode, thrown: boolean) =>
  thrown || (!!weapon && !weapon.melee && mode !== 'ShotgunShell' && mode !== 'Suppressive');

/** "4d6" -> 4, "5d6 / 3d6" -> 5 */
export function diceCount(expression: string): number {
  const match = /^\s*(\d+)d\d+/i.exec(expression);
  return match ? Number(match[1]) : 0;
}

/** Parses typed dice ("5 5 6" or "5,5,6"); empty means "let the app roll". */
export function parseDice(text: string): number[] | null {
  const parts = text.split(/[\s,;]+/).filter(Boolean).map(Number);
  return parts.length === 0 || parts.some((n) => !Number.isInteger(n)) ? null : parts;
}

export const hpPercent = (f: FighterView) => (f.maxHp > 0 ? Math.max(0, Math.min(100, (f.hp / f.maxHp) * 100)) : 0);

export const isDown = (f: FighterView) => f.dead || f.woundState === 'Mortally';
