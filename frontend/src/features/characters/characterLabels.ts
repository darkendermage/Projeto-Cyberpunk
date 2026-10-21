// Labels and pure helpers of the character screens. Rule values never live here: they come from the API.
import type {
  CharacterRules, CharacterSkillLine, CharacterStatus, CreationMethod, InjuryStatus, ItemState, WoundState,
} from '../../api/characterTypes';

/**
 * Native-language suggestions: the languages of the chosen Cultural Origin (CP:R p.45) or, before one is chosen,
 * every language the table lists. The field still takes any language typed by hand.
 */
export function languagesFor(rules: Pick<CharacterRules, 'culturalOrigins'>, region: string | null | undefined): string[] {
  const origin = region ? rules.culturalOrigins.find((o) => o.region === region) : undefined;
  if (origin) return [...origin.languages];
  return [...new Set(rules.culturalOrigins.flatMap((o) => o.languages))].sort((a, b) => a.localeCompare(b));
}

export const METHOD_LABEL: Record<CreationMethod, string> = {
  Streetrat: 'Streetrat',
  Edgerunner: 'Edgerunner',
  CompletePackage: 'Complete Package',
};

export const METHOD_HINT: Record<CreationMethod, string> = {
  Streetrat: 'Templates prontos da Role. O jeito mais rápido de jogar.',
  Edgerunner: 'STATs rolados um a um; você distribui os pontos de perícia.',
  CompletePackage: 'Compra de STATs, perícias e todo o equipamento. Controle total.',
};

export const STATUS_LABEL: Record<CharacterStatus, string> = {
  Creating: 'Em criação',
  Complete: 'Concluído',
  NeedsAttention: 'Pendências',
};

export const WOUND_LABEL: Record<WoundState, string> = {
  Unhurt: 'Sem ferimentos',
  Lightly: 'Lightly Wounded',
  Seriously: 'Seriously Wounded',
  Mortally: 'Mortally Wounded',
};

export const INJURY_STATUS_LABEL: Record<InjuryStatus, string> = {
  Active: 'Ativa',
  QuickFixed: 'Quick Fix (efeito suspenso hoje)',
  Healed: 'Tratada',
};

export const ITEM_STATE_LABEL: Record<ItemState, string> = {
  Carried: 'Carregado',
  Equipped: 'Equipado',
  Stored: 'Guardado',
};

/** Catalog categories as the store lists them. */
export const CATEGORY_LABEL: Record<string, string> = {
  Weapon: 'Armas', Ammunition: 'Munições', WeaponAttachment: 'Upgrades de armas', Gear: 'Itens avulsos', Armor: 'Armaduras e roupas',
  Cyberware: 'Cyberware', Netrunning: 'Netrunning', Drug: 'Drogas', Vehicle: 'Veículos', VehicleUpgrade: 'Upgrades veiculares', HqUpgrade: 'QG',
};

export const STAT_LABEL: Record<string, string> = {
  INT: 'Inteligência', REF: 'Reflexos', DEX: 'Destreza', TECH: 'Técnica', COOL: 'Frieza',
  WILL: 'Vontade', LUCK: 'Sorte', MOVE: 'Movimento', BODY: 'Corpo', EMP: 'Empatia',
};

/** Skill categories in sheet order, with the Portuguese headings of the reference sheet. */
export const SKILL_CATEGORIES: { key: string; label: string }[] = [
  { key: 'Awareness', label: 'Perícias de Atenção' },
  { key: 'Body', label: 'Perícias Corporais' },
  { key: 'Control', label: 'Perícias de Condução' },
  { key: 'Education', label: 'Perícias de Educação' },
  { key: 'Fighting', label: 'Perícias de Luta' },
  { key: 'Performance', label: 'Perícias de Performance' },
  { key: 'Ranged Weapon', label: 'Perícias de Armas à Distância' },
  { key: 'Social', label: 'Perícias Sociais' },
  { key: 'Technique', label: 'Perícias Técnicas' },
];

/** "Idioma (Streetslang)", with the specialization; the English book name goes to the tooltip. */
export function skillLabel(line: Pick<CharacterSkillLine, 'name' | 'namePt' | 'specialization'>): string {
  const base = line.namePt ?? line.name;
  return line.specialization ? `${base} (${line.specialization})` : base;
}

export type SkillFilter = 'all' | 'trained' | 'role' | 'combat' | 'basic';

export const SKILL_FILTERS: { value: SkillFilter; label: string }[] = [
  { value: 'all', label: 'Todas' },
  { value: 'trained', label: 'Com nível' },
  { value: 'role', label: 'Da Role' },
  { value: 'basic', label: 'Básicas' },
  { value: 'combat', label: 'Combate' },
];

const normalize = (s: string) => s.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();

/** Search matches the Portuguese label, the English book name and the specialization. */
export function filterSkills(lines: CharacterSkillLine[], query: string, filter: SkillFilter): CharacterSkillLine[] {
  const q = normalize(query.trim());
  return lines.filter((l) => {
    if (filter === 'trained' && l.level === 0) return false;
    if (filter === 'role' && !l.roleSkill) return false;
    if (filter === 'basic' && !l.basic) return false;
    if (filter === 'combat' && !l.combat) return false;
    if (!q) return true;
    return [l.name, l.namePt ?? '', l.specialization ?? '', l.stat].some((t) => normalize(t).includes(q));
  });
}

export function groupSkills(lines: CharacterSkillLine[]): { key: string; label: string; lines: CharacterSkillLine[] }[] {
  return SKILL_CATEGORIES
    .map((c) => ({ ...c, lines: lines.filter((l) => l.category === c.key) }))
    .filter((g) => g.lines.length > 0);
}

export function signed(value: number): string {
  return value > 0 ? `+${value}` : `${value}`;
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}
