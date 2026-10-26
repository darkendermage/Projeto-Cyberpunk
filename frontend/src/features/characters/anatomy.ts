// Body regions of the cyberware screen. Placement of installed pieces comes from the server report; this module
// only decides which catalog items to suggest first for the region the player selected.
import type { StoreItem } from '../../api/characterTypes';
import { t } from '../../i18n';

export type RegionKey =
  | 'neural' | 'eye-left' | 'eye-right' | 'audio' | 'arm-left' | 'arm-right' | 'leg-left' | 'leg-right'
  | 'internal' | 'external' | 'fashion' | 'borgware';

/** Hotspot of each region on the 400 × 640 figure, and the side its panel sits on. */
export const HOTSPOTS: Record<RegionKey, { x: number; y: number; panel: 'left' | 'right' }> = {
  neural: { x: 200, y: 58, panel: 'left' },
  'eye-right': { x: 186, y: 86, panel: 'left' },
  'eye-left': { x: 214, y: 86, panel: 'right' },
  audio: { x: 232, y: 96, panel: 'right' },
  internal: { x: 200, y: 222, panel: 'left' },
  external: { x: 236, y: 268, panel: 'right' },
  fashion: { x: 164, y: 268, panel: 'left' },
  borgware: { x: 200, y: 330, panel: 'right' },
  'arm-right': { x: 108, y: 300, panel: 'left' },
  'arm-left': { x: 292, y: 300, panel: 'right' },
  'leg-right': { x: 176, y: 500, panel: 'left' },
  'leg-left': { x: 224, y: 500, panel: 'right' },
};

export const LEFT_REGIONS: RegionKey[] = ['neural', 'eye-right', 'internal', 'fashion', 'arm-right', 'leg-right'];
export const RIGHT_REGIONS: RegionKey[] = ['eye-left', 'audio', 'external', 'borgware', 'arm-left', 'leg-left'];

/** Whether a catalog item belongs in a region (catalog types follow CP:R p.110–117). */
export function fitsRegion(item: Pick<StoreItem, 'cyberType' | 'bodyLocation'>, region: RegionKey | null): boolean {
  if (!region) return true;
  const entry = item.cyberType;
  const loc = item.bodyLocation;
  switch (region) {
    case 'neural': return entry === 'Neuralware';
    case 'eye-left': case 'eye-right': return entry === 'Cyberoptics';
    case 'audio': return entry === 'Cyberaudio';
    case 'arm-left': case 'arm-right':
      return (entry === 'Cyberlimb' || entry === 'LimbOption' || entry === 'Cyberfinger') && loc !== 'Leg';
    case 'leg-left': case 'leg-right':
      return (entry === 'Cyberlimb' || entry === 'LimbOption') && (loc === 'Leg' || loc === 'FullBody');
    case 'internal': return entry === 'InternalBody';
    case 'external': return entry === 'ExternalBody';
    case 'fashion': return entry === 'Fashionware';
    case 'borgware': return entry === 'Borgware';
  }
}

export const CYBER_TYPE_LABEL: Record<string, string> = {
  Neuralware: 'Neuralware', Cyberoptics: 'Cyberoptics', Cyberaudio: 'Cyberaudio', InternalBody: 'Internal',
  ExternalBody: 'External', Cyberlimb: 'Cyberlimb', LimbOption: t('Opção de membro', 'Limb option'), Cyberfinger: 'Cyberfinger',
  Borgware: 'Borgware', Bioexotic: 'Bioexotics', Fashionware: 'Fashionware',
};

/** Kind of wearable host an option goes in (Smart Glasses take Cyberoptics, gloves arm options, Smart Ears Cyberaudio). */
export function wearableAccepts(cyberType: string | null | undefined): 'Cybereye' | 'Cyberarm' | 'Cyberaudio' | null {
  switch (cyberType) {
    case 'Cyberoptics': return 'Cybereye';
    case 'Cyberaudio': return 'Cyberaudio';
    case 'LimbOption':
    case 'Cyberfinger': return 'Cyberarm';
    default: return null;
  }
}

/** "HL 7 (2d6)" during play, "HL 7" while creating (preset, CP:R p.110). */
export function humanityLossLabel(fixed: number | null, dice: string | null, creating: boolean): string {
  if (fixed == null && !dice) return 'HL —';
  if (creating || !dice) return `HL ${fixed ?? 0}`;
  return `HL ${fixed ?? 0} (${dice})`;
}
