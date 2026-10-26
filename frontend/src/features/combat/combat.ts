// Pure helpers for the combat table: labels and which attack modes a weapon offers. The rules themselves (DVs,
// damage, armor, Critical Injuries) are applied by the server (POST /api/encounters/{id}/attack).
import type { AttackMode, FighterView, WeaponView, WoundState } from '../../api/encounterTypes';
import { t } from '../../i18n';

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

/** The modes a weapon can be used in (CP:R p.170-178): autofire needs an Autofire value, shells need a shotgun… */
export function modesFor(weapon: WeaponView | null, thrown = false): AttackMode[] {
  if (thrown) return ['Single', 'Explosive'];
  if (!weapon) return ['Single'];
  const modes: AttackMode[] = ['Single'];
  if (weapon.autofireMultiplier != null) modes.push('Autofire', 'Suppressive');
  if (!weapon.melee || weapon.skill === 'melee-weapon' || weapon.skill.startsWith('martial-arts')) modes.push('AimedHead', 'AimedHand', 'AimedLeg');
  if (weapon.class === 'Shotgun') modes.push('ShotgunShell');
  if (weapon.class && EXPLOSIVE_CLASSES.has(weapon.class)) modes.push('Explosive');
  return modes;
}

/** Area attacks hit several targets with one roll of damage. */
export const isArea = (mode: AttackMode) => mode === 'Explosive' || mode === 'ShotgunShell' || mode === 'Suppressive';

/** Melee attacks ignore range; ranged ones need it (except Shotgun Shell, always DV13). */
export const needsRange = (weapon: WeaponView | null, mode: AttackMode, thrown: boolean) =>
  thrown || (!!weapon && !weapon.melee && mode !== 'ShotgunShell' && mode !== 'Suppressive');

/** "4d6" → 4 */
export function diceCount(expression: string): number {
  const match = /^(\d+)d\d+$/i.exec(expression.trim());
  return match ? Number(match[1]) : 0;
}

/** Parses typed dice ("5 5 6" or "5,5,6"); empty means "let the app roll". */
export function parseDice(text: string): number[] | null {
  const parts = text.split(/[\s,;]+/).filter(Boolean).map(Number);
  return parts.length === 0 || parts.some((n) => !Number.isInteger(n)) ? null : parts;
}

export const hpPercent = (f: FighterView) => (f.maxHp > 0 ? Math.max(0, Math.min(100, (f.hp / f.maxHp) * 100)) : 0);

export const isDown = (f: FighterView) => f.dead || f.woundState === 'Mortally';
