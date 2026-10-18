/** Icon key per category key (the API also sends it, this is the offline fallback for search results). */
export const CATEGORY_ICONS: Record<string, string> = {
  weapons: 'weapons',
  ammunition: 'ammo',
  'weapon-attachments': 'attachment',
  gear: 'gear',
  armor: 'armor',
  cyberware: 'cyberware',
  netrunning: 'netrunning',
  drugs: 'drugs',
  vehicles: 'vehicle',
  'vehicle-upgrades': 'vehicleUpgrade',
  'hq-upgrades': 'hq',
};

/** Short HUD code shown on cards, e.g. WPN-0042. */
export const CATEGORY_CODES: Record<string, string> = {
  weapons: 'WPN',
  ammunition: 'AMO',
  'weapon-attachments': 'UPG',
  gear: 'GER',
  armor: 'ARM',
  cyberware: 'CYB',
  netrunning: 'NET',
  drugs: 'DRG',
  vehicles: 'VHC',
  'vehicle-upgrades': 'VUP',
  'hq-upgrades': 'HQU',
};

export const DEFAULT_CATEGORY = 'weapons';
