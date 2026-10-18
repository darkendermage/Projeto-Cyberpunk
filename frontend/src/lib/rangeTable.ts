// Range DV tables of the Cyberpunk RED Core Rulebook (p.173). The book itself measures areas in 2 m/yd squares
// (Shotgun Shell: "6m/yds (3 squares)"; explosives: "10m/yd by 10m/yd area (5 Square by 5 Square)", p.174),
// so a tile here is one of those squares.

export const METERS_PER_TILE = 2;

export interface RangeBand {
  /** Inclusive limits in meters/yards, as printed in the book. */
  min: number;
  max: number;
}

export const RANGE_BANDS: RangeBand[] = [
  { min: 0, max: 6 },
  { min: 7, max: 12 },
  { min: 13, max: 25 },
  { min: 26, max: 50 },
  { min: 51, max: 100 },
  { min: 101, max: 200 },
  { min: 201, max: 400 },
  { min: 401, max: 800 },
];

export const AUTOFIRE_BANDS = RANGE_BANDS.slice(0, 5);

export type RangeRowKey =
  | 'pistol' | 'smg' | 'shotgun' | 'assaultRifle' | 'sniperRifle' | 'bow' | 'grenadeLauncher' | 'rocketLauncher';

export interface RangeRow {
  key: RangeRowKey;
  label: string;
  /** DV per band; null = out of range (N/A in the book). */
  dv: (number | null)[];
}

/** Single Shot DVs Based on Range (CP:R p.173). */
export const SINGLE_SHOT: RangeRow[] = [
  { key: 'pistol', label: 'Pistola', dv: [13, 15, 20, 25, 30, 30, null, null] },
  { key: 'smg', label: 'SMG', dv: [15, 13, 15, 20, 25, 25, 30, null] },
  { key: 'shotgun', label: 'Escopeta (Slug)', dv: [13, 15, 20, 25, 30, 35, null, null] },
  { key: 'assaultRifle', label: 'Rifle de Assalto', dv: [17, 16, 15, 13, 15, 20, 25, 30] },
  { key: 'sniperRifle', label: 'Rifle de Precisão', dv: [30, 25, 25, 20, 15, 16, 17, 20] },
  { key: 'bow', label: 'Arco e Besta', dv: [15, 13, 15, 17, 20, 22, null, null] },
  { key: 'grenadeLauncher', label: 'Lança-Granadas', dv: [16, 15, 15, 17, 20, 22, 25, null] },
  { key: 'rocketLauncher', label: 'Lança-Foguetes', dv: [17, 16, 15, 15, 20, 20, 25, 30] },
];

/** Autofire DVs Based on Range (CP:R p.173). */
export const AUTOFIRE: RangeRow[] = [
  { key: 'smg', label: 'SMG', dv: [20, 17, 20, 25, 30] },
  { key: 'assaultRifle', label: 'Rifle de Assalto', dv: [22, 20, 17, 20, 25] },
];

/** Weapon class (catalog enum) to the row of the range table. Machine guns and melee weapons have no row. */
export const RANGE_ROW_BY_CLASS: Record<string, RangeRowKey> = {
  MediumPistol: 'pistol',
  HeavyPistol: 'pistol',
  VeryHeavyPistol: 'pistol',
  Smg: 'smg',
  HeavySmg: 'smg',
  Shotgun: 'shotgun',
  AssaultRifle: 'assaultRifle',
  SniperRifle: 'sniperRifle',
  Bow: 'bow',
  Crossbow: 'bow',
  GrenadeLauncher: 'grenadeLauncher',
  RocketLauncher: 'rocketLauncher',
};

/**
 * Band limits in tiles. Each band ends at floor(max / 2) and starts right after the previous band, so every
 * tile count falls in exactly one band (12 tiles = 24 m is still the 13–25 m band; 13 tiles = 26 m is the next).
 */
export function bandInTiles(index: number, bands: RangeBand[] = RANGE_BANDS): RangeBand {
  const max = Math.floor(bands[index].max / METERS_PER_TILE);
  const min = index === 0 ? 0 : Math.floor(bands[index - 1].max / METERS_PER_TILE) + 1;
  return { min, max };
}

export type RangeUnit = 'meters' | 'tiles';

export function bandLabel(index: number, unit: RangeUnit, bands: RangeBand[] = RANGE_BANDS): string {
  const band = unit === 'tiles' ? bandInTiles(index, bands) : bands[index];
  return `${band.min}–${band.max}`;
}

/** Index of the band that contains a distance, or -1 beyond the table. */
export function bandIndexFor(distance: number, unit: RangeUnit, bands: RangeBand[] = RANGE_BANDS): number {
  return bands.findIndex((_, i) => {
    const band = unit === 'tiles' ? bandInTiles(i, bands) : bands[i];
    return distance >= band.min && distance <= band.max;
  });
}

export function rangeRowsFor(weaponClass: string | undefined): { single?: RangeRow; autofire?: RangeRow } {
  const key = weaponClass ? RANGE_ROW_BY_CLASS[weaponClass] : undefined;
  if (!key) return {};
  return { single: SINGLE_SHOT.find((r) => r.key === key), autofire: AUTOFIRE.find((r) => r.key === key) };
}
