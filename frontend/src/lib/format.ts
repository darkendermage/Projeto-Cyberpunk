import type { AttributeValue, CategorySchema, FieldDescriptor, Item } from '../api/types';

const numberFormat = new Intl.NumberFormat('pt-BR');

export function formatEb(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : numberFormat.format(value);
}

export function formatNumber(value: number): string {
  return numberFormat.format(value);
}

export const DATA_ORIGIN_LABEL: Record<string, string> = {
  Compendium: 'Compêndio importado (não verificado)',
  Demo: 'Dados de demonstração',
  Custom: 'Cadastro manual',
  Verified: 'Conferido com PDF oficial',
};

export const RULE_SYSTEM_LABEL: Record<string, string> = {
  Cyberpunk2020: 'Cyberpunk 2020',
  CyberpunkRed: 'Cyberpunk RED',
};

export function optionLabel(field: FieldDescriptor | undefined, value: string): string {
  return field?.options?.find((o) => o.value === value)?.label ?? value;
}

export interface FormattedValue {
  text: string;
  unit?: string;
  empty: boolean;
}

const WEAPON_GROUPS: { label: string; classes: string[] }[] = [
  { label: 'Pistolas', classes: ['MediumPistol', 'HeavyPistol', 'VeryHeavyPistol'] },
  { label: 'SMGs', classes: ['Smg', 'HeavySmg'] },
  { label: 'Escopetas', classes: ['Shotgun'] },
  { label: 'Rifles de Assalto', classes: ['AssaultRifle'] },
  { label: 'Rifles de Precisão', classes: ['SniperRifle'] },
  { label: 'Metralhadoras', classes: ['MachineGun'] },
  { label: 'Arcos e Bestas', classes: ['Bow', 'Crossbow'] },
  { label: 'Lançadores', classes: ['GrenadeLauncher', 'RocketLauncher'] },
  { label: 'Corpo a corpo', classes: ['LightMelee', 'MediumMelee', 'HeavyMelee', 'VeryHeavyMelee'] },
];
const RANGED_CLASSES = WEAPON_GROUPS.filter((g) => g.label !== 'Corpo a corpo').flatMap((g) => g.classes);

/**
 * Compact labels for a set of weapon classes ("Pistolas", "Todas de longo alcance"...). A group is named when
 * every class of it is present; leftover classes keep their own label.
 */
export function weaponClassGroups(values: string[], field?: FieldDescriptor): string[] {
  const set = new Set(values);
  const has = (classes: string[]) => classes.every((c) => set.has(c));
  const melee = WEAPON_GROUPS[WEAPON_GROUPS.length - 1].classes;
  if (has(RANGED_CLASSES) && has(melee)) return ['Qualquer arma'];
  const labels: string[] = [];
  const covered = new Set<string>();
  if (has(RANGED_CLASSES)) {
    labels.push('Todas de longo alcance');
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
      return { text: value ? 'Sim' : 'Não', empty: false };
    case 'enum':
      return { text: optionLabel(field, String(value)), empty: false };
    case 'set':
      if (field.key === 'compatibleClasses') return { text: weaponClassGroups(value as string[], field).join(', '), empty: false };
      return { text: (value as string[]).map((v) => optionLabel(field, v)).join(', '), empty: false };
    case 'number': {
      const n = Number(value);
      if (field.unit === 'penalty') return { text: n === 0 ? '0' : `−${n}`, empty: false };
      if (field.unit === 'd6') return { text: `${n}d6`, empty: false };
      if (field.key === 'maxRange' && n === 0) return { text: 'Contato', empty: false };
      return { text: formatNumber(n), unit: field.unit ?? undefined, empty: false };
    }
    default:
      return { text: String(value), empty: false };
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
  damage: 'Dano',
  damageDice: 'Dano',
  autofire: 'Autofire',
  hands: 'Mãos',
  minBody: 'BODY mín.',
  calibers: 'Munição',
  quality: 'Qualidade',
  skill: 'Perícia',
  requiresItem: 'Requer',
  foundational: 'Peça base',
  requiresPair: 'Exige par',
  hasPenalty: 'Penalidade',
  hasSkillBonus: 'Dá bônus',
  hasFunctionalEffect: 'Funcional',
  exotic: 'Exótica',
  armored: 'Blindado',
  requirements: 'Requisitos',
  kind: 'Tipo',
  rof: 'ROF',
  maxRange: 'Alcance',
  concealable: 'Conceal',
  magazine: 'Pente',
  quantityPerPack: 'Qtd.',
  resistDv: 'DV',
  area: 'Área',
  effectType: 'Efeito',
  gearClass: 'Classe',
  skillBonus: 'Bônus',
  weightKg: 'Peso',
  gearType: 'Tipo',
  sp: 'SP',
  shieldHp: 'HP',
  bodyValue: 'BODY',
  hitPoints: 'HP',
  refPenalty: 'REF',
  dexPenalty: 'DEX',
  movePenalty: 'MOVE',
  humanityLoss: 'HL',
  clothingSlot: 'Peça',
  clothingStyle: 'Estilo',
  installLocation: 'Instalação',
  slotsRequired: 'Slots',
  slotsProvided: 'Oferece',
  bodyLocation: 'Local',
  compatibleClasses: 'Compatível',
  eligibleWeapons: 'Restrições',
  netType: 'Tipo',
  programClass: 'Classe',
  atk: 'ATK',
  def: 'DEF',
  rez: 'REZ',
  per: 'PER',
  spd: 'SPD',
  totalSlots: 'Slots do deck',
  sharedSlots: 'Livres',
  programSlots: 'Programa',
  hardwareSlots: 'Hardware',
  motoRank: 'Moto',
  duration: 'Duração',
  addictionRisk: 'Vício',
  speedMove: 'MOVE',
  topSpeedMph: 'Vel. máx.',
  seats: 'Ocup.',
  sdp: 'SDP',
  armorSp: 'SP',
  improvementPoints: 'IP',
  upgradeType: 'Tipo',
  spaceRequired: 'Espaço',
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
