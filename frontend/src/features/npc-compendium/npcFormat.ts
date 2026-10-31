import { t } from '../../i18n';

/** A number as printed in the book; null (a dash in the book) shows as an em dash. */
export function formatPrinted(value: number | null | undefined): string {
  return value == null ? '—' : String(value);
}

export const STAT_ORDER = ['INT', 'REF', 'DEX', 'TECH', 'COOL', 'WILL', 'LUCK', 'MOVE', 'BODY', 'EMP'] as const;

const WEAPON_CLASS_LABEL: Record<string, string> = {
  MediumPistol: t('Pistola Média', 'Medium Pistol'),
  HeavyPistol: t('Pistola Pesada', 'Heavy Pistol'),
  VeryHeavyPistol: t('Pistola Muito Pesada', 'Very Heavy Pistol'),
  Smg: 'SMG',
  HeavySmg: t('SMG Pesada', 'Heavy SMG'),
  Shotgun: t('Escopeta', 'Shotgun'),
  AssaultRifle: t('Rifle de Assalto', 'Assault Rifle'),
  SniperRifle: t('Rifle de Precisão', 'Sniper Rifle'),
  MachineGun: t('Metralhadora', 'Machine Gun'),
  Bow: t('Arco', 'Bow'),
  Crossbow: t('Besta', 'Crossbow'),
  GrenadeLauncher: t('Lança-Granadas', 'Grenade Launcher'),
  RocketLauncher: t('Lança-Foguetes', 'Rocket Launcher'),
  LightMelee: t('Corpo a Corpo Leve', 'Light Melee'),
  MediumMelee: t('Corpo a Corpo Médio', 'Medium Melee'),
  HeavyMelee: t('Corpo a Corpo Pesado', 'Heavy Melee'),
  VeryHeavyMelee: t('Corpo a Corpo Muito Pesado', 'Very Heavy Melee'),
};

/** The app's label for a WeaponClass value; an unknown value shows as it came. */
export const weaponClassLabel = (value: string) => WEAPON_CLASS_LABEL[value] ?? value;

export const RANGE_BANDS = ['0-6', '7-12', '13-25', '26-50', '51-100', '101-200', '201-400', '401-800'] as const;
