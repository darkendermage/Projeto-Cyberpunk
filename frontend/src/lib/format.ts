import type { AttributeValue, CategorySchema, FieldDescriptor, Item } from '../api/types';
import { locale, t, tv } from '../i18n';

const numberFormat = new Intl.NumberFormat(locale);

export function formatEb(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : numberFormat.format(value);
}

export function formatNumber(value: number): string {
  return numberFormat.format(value);
}

export const PRICE_CATEGORY_LABEL: Record<string, string> = {
  Cheap: 'Cheap', Everyday: 'Everyday', Costly: 'Costly', Premium: 'Premium', Expensive: 'Expensive',
  VeryExpensive: 'Very Expensive', Luxury: 'Luxury', SuperLuxury: 'Super Luxury',
};

export const DATA_ORIGIN_LABEL: Record<string, string> = {
  Compendium: t('Compêndio importado (não verificado)', 'Imported compendium (not verified)'),
  Demo: t('Dados de demonstração', 'Demo data'),
  Custom: t('Cadastro manual', 'Manual entry'),
  Verified: t('Conferido com PDF oficial', 'Checked against the official PDF'),
};

export const RULE_SYSTEM_LABEL: Record<string, string> = {
  Cyberpunk2020: 'Cyberpunk 2020',
  CyberpunkRed: 'Cyberpunk RED',
};

export function optionLabel(field: FieldDescriptor | undefined, value: string): string {
  return field?.options?.find((o) => o.value === value)?.label ?? tv(value);
}

export interface FormattedValue {
  text: string;
  unit?: string;
  empty: boolean;
}

const WEAPON_GROUPS: { label: string; classes: string[] }[] = [
  { label: t('Pistolas', 'Pistols'), classes: ['MediumPistol', 'HeavyPistol', 'VeryHeavyPistol'] },
  { label: 'SMGs', classes: ['Smg', 'HeavySmg'] },
  { label: t('Escopetas', 'Shotguns'), classes: ['Shotgun'] },
  { label: t('Rifles de Assalto', 'Assault Rifles'), classes: ['AssaultRifle'] },
  { label: t('Rifles de Precisão', 'Sniper Rifles'), classes: ['SniperRifle'] },
  { label: t('Metralhadoras', 'Machine Guns'), classes: ['MachineGun'] },
  { label: t('Arcos e Bestas', 'Bows & Crossbows'), classes: ['Bow', 'Crossbow'] },
  { label: t('Lançadores', 'Launchers'), classes: ['GrenadeLauncher', 'RocketLauncher'] },
  { label: t('Corpo a corpo', 'Melee'), classes: ['LightMelee', 'MediumMelee', 'HeavyMelee', 'VeryHeavyMelee'] },
];
const RANGED_CLASSES = WEAPON_GROUPS.filter((g) => g.label !== t('Corpo a corpo', 'Melee')).flatMap((g) => g.classes);

/**
 * Compact labels for a set of weapon classes ("Pistolas", "Todas de longo alcance"...). A group is named when
 * every class of it is present; leftover classes keep their own label.
 */
export function weaponClassGroups(values: string[], field?: FieldDescriptor): string[] {
  const set = new Set(values);
  const has = (classes: string[]) => classes.every((c) => set.has(c));
  const melee = WEAPON_GROUPS[WEAPON_GROUPS.length - 1].classes;
  if (has(RANGED_CLASSES) && has(melee)) return [t('Qualquer arma', 'Any weapon')];
  const labels: string[] = [];
  const covered = new Set<string>();
  if (has(RANGED_CLASSES)) {
    labels.push(t('Todas de longo alcance', 'All ranged'));
    RANGED_CLASSES.forEach((c) => covered.add(c));
  }
  for (const g of WEAPON_GROUPS) {
    if (g.classes.some((c) => covered.has(c))) continue;
    if (has(g.classes)) {
      labels.push(g.label);
      g.classes.forEach((c) => covered.add(c));
    }
  }
  for (const v of values) if (!covered.has(v)) labels.push(field ? optionLabel(field, v) : v);
  return labels;
}

/** Formats an attribute for display (cards, detail, comparison). */
export function formatAttribute(field: FieldDescriptor | undefined, value: AttributeValue | undefined): FormattedValue {
  if (value === null || value === undefined || (Array.isArray(value) && value.length === 0) || value === '') {
    return { text: '—', empty: true };
  }
  if (!field) return { text: String(value), empty: false };

  switch (field.kind) {
    case 'boolean':
      return { text: value ? t('Sim', 'Yes') : t('Não', 'No'), empty: false };
    case 'enum':
      return { text: optionLabel(field, String(value)), empty: false };
    case 'set':
      if (field.key === 'compatibleClasses') return { text: weaponClassGroups(value as string[], field).join(', '), empty: false };
      return { text: (value as string[]).map((v) => optionLabel(field, v)).join(', '), empty: false };
    case 'number': {
      const n = Number(value);
      if (field.unit === 'penalty') return { text: n === 0 ? '0' : `−${n}`, empty: false };
      if (field.unit === 'd6') return { text: `${n}d6`, empty: false };
      if (field.key === 'maxRange' && n === 0) return { text: t('Contato', 'Contact'), empty: false };
      return { text: formatNumber(n), unit: field.unit ?? undefined, empty: false };
    }
    default:
      return { text: tv(String(value)), empty: false };
  }
}

/** Card stats: the first N card fields that apply to the item's subtype and have a value. */
export function cardStats(
  schema: CategorySchema, item: Item, count = 4, keepEmpty = false,
): { field: FieldDescriptor; value: FormattedValue }[] {
  const fields = new Map(schema.fields.map((f) => [f.key, f]));
  const subtype = item.subtype?.value;
  const picked: { field: FieldDescriptor; value: FormattedValue }[] = [];
  for (const key of schema.cardFields) {
    const field = fields.get(key);
    if (!field) continue;
    if (field.appliesTo?.length && subtype && !field.appliesTo.includes(subtype)) continue;
    const value = formatAttribute(field, item.attributes[key]);
    if (value.empty && field.kind !== 'boolean' && !keepEmpty) continue;
    picked.push({ field, value });
    if (picked.length === count) break;
  }
  return picked;
}

/** Short labels used in compact stat cells. */
export const SHORT_LABELS: Record<string, string> = {
  damage: t('Dano', 'Damage'),
  damageDice: t('Dano', 'Damage'),
  autofire: 'Autofire',
  hands: t('Mãos', 'Hands'),
  minBody: t('BODY mín.', 'Min. BODY'),
  calibers: t('Munição', 'Ammo'),
  quality: t('Qualidade', 'Quality'),
  skill: t('Perícia', 'Skill'),
  requiresItem: t('Requer', 'Requires'),
  foundational: t('Peça base', 'Foundational'),
  requiresPair: t('Exige par', 'Needs a pair'),
  hasPenalty: t('Penalidade', 'Penalty'),
  hasSkillBonus: t('Dá bônus', 'Gives a bonus'),
  hasFunctionalEffect: t('Funcional', 'Functional'),
  exotic: t('Exótica', 'Exotic'),
  armored: t('Blindado', 'Armored'),
  requirements: t('Requisitos', 'Requirements'),
  kind: t('Tipo', 'Type'),
  rof: 'ROF',
  maxRange: t('Alcance', 'Range'),
  concealable: 'Conceal',
  magazine: t('Pente', 'Magazine'),
  quantityPerPack: t('Qtd.', 'Qty.'),
  resistDv: 'DV',
  area: t('Área', 'Area'),
  effectType: t('Efeito', 'Effect'),
  gearClass: t('Classe', 'Class'),
  skillBonus: t('Bônus', 'Bonus'),
  weightKg: t('Peso', 'Weight'),
  gearType: t('Tipo', 'Type'),
  sp: 'SP',
  shieldHp: 'HP',
  bodyValue: 'BODY',
  hitPoints: 'HP',
  refPenalty: 'REF',
  dexPenalty: 'DEX',
  movePenalty: 'MOVE',
  humanityLoss: 'HL',
  clothingSlot: t('Peça', 'Piece'),
  clothingStyle: t('Estilo', 'Style'),
  installLocation: t('Instalação', 'Install'),
  slotsRequired: 'Slots',
  slotsProvided: t('Oferece', 'Provides'),
  bodyLocation: t('Local', 'Location'),
  compatibleClasses: t('Compatível', 'Compatible'),
  eligibleWeapons: t('Restrições', 'Restrictions'),
  netType: t('Tipo', 'Type'),
  programClass: t('Classe', 'Class'),
  atk: 'ATK',
  def: 'DEF',
  rez: 'REZ',
  per: 'PER',
  spd: 'SPD',
  totalSlots: t('Slots do deck', 'Deck slots'),
  sharedSlots: t('Livres', 'Free'),
  programSlots: t('Programa', 'Program'),
  hardwareSlots: 'Hardware',
  motoRank: 'Moto',
  duration: t('Duração', 'Duration'),
  addictionRisk: t('Vício', 'Addiction'),
  speedMove: 'MOVE',
  topSpeedMph: t('Vel. máx.', 'Top speed'),
  seats: t('Ocup.', 'Seats'),
  sdp: 'SDP',
  armorSp: 'SP',
  improvementPoints: 'IP',
  upgradeType: t('Tipo', 'Type'),
  spaceRequired: t('Espaço', 'Space'),
};

export function shortLabel(field: FieldDescriptor): string {
  return SHORT_LABELS[field.key] ?? field.label;
}

/** Whether a higher value is better, for highlighting in the comparison view. */
export const COMPARE_DIRECTION: Record<string, 'high' | 'low'> = {
  damageDice: 'high', rof: 'high', magazine: 'high', maxRange: 'high', autofire: 'high',
  sp: 'high', shieldHp: 'high', bodyValue: 'high', hitPoints: 'high',
  speedMove: 'high', topSpeedMph: 'high', seats: 'high', sdp: 'high', armorSp: 'high', cargoKg: 'high', maneuverability: 'high',
  slotsProvided: 'high', quantityPerPack: 'high',
  price: 'low', humanityLoss: 'low', refPenalty: 'low', dexPenalty: 'low', movePenalty: 'low', slotsRequired: 'low',
  weightKg: 'low', improvementPoints: 'low', minBody: 'low',
};

export function downloadJson(filename: string, data: unknown) {
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
