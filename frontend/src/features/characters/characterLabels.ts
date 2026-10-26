// Labels and pure helpers of the character screens. Rule values never live here: they come from the API.
import type {
  CharacterRules, CharacterSkillLine, CharacterStatus, CreationMethod, InjuryStatus, ItemState, WoundState,
} from '../../api/characterTypes';
import { locale, skillName, t } from '../../i18n';

/**
 * Native-language suggestions: the languages of the chosen Cultural Origin (CP:R p.45) or, before one is chosen,
 * every language the table lists. The field still takes any language typed by hand.
 */
export function languagesFor(rules: Pick<CharacterRules, 'culturalOrigins'>, region: string | null | undefined): string[] {
  const origin = region ? rules.culturalOrigins.find((o) => o.region === region) : undefined;
  if (origin) return [...origin.languages];
  return [...new Set(rules.culturalOrigins.flatMap((o) => o.languages))].sort((a, b) => a.localeCompare(b));
}

/** The options of a select, plus the saved value when it is not one of them (older sheets kept free text). */
export function withCurrent(options: readonly string[], current: string | null | undefined): string[] {
  return current && !options.includes(current) ? [current, ...options] : [...options];
}

export const METHOD_LABEL: Record<CreationMethod, string> = {
  Streetrat: 'Streetrat',
  Edgerunner: 'Edgerunner',
  CompletePackage: 'Complete Package',
};

export const METHOD_HINT: Record<CreationMethod, string> = {
  Streetrat: t('Templates prontos da Role. O jeito mais rápido de jogar.', 'Ready-made Role templates. The fastest way to play.'),
  Edgerunner: t('STATs rolados um a um; você distribui os pontos de perícia.', 'STATs rolled one by one; you spend the skill points.'),
  CompletePackage: t('Compra de STATs, perícias e todo o equipamento. Controle total.', 'Buy STATs, skills and all the gear. Full control.'),
};

export const STATUS_LABEL: Record<CharacterStatus, string> = {
  Creating: t('Em criação', 'In creation'),
  Complete: t('Concluído', 'Complete'),
  NeedsAttention: t('Pendências', 'Issues'),
};

export const WOUND_LABEL: Record<WoundState, string> = {
  Unhurt: t('Sem ferimentos', 'Unhurt'),
  Lightly: 'Lightly Wounded',
  Seriously: 'Seriously Wounded',
  Mortally: 'Mortally Wounded',
};

export const INJURY_STATUS_LABEL: Record<InjuryStatus, string> = {
  Active: t('Ativa', 'Active'),
  QuickFixed: t('Quick Fix (efeito suspenso hoje)', 'Quick Fix (effect suspended today)'),
  Healed: t('Tratada', 'Treated'),
};

export const ITEM_STATE_LABEL: Record<ItemState, string> = {
  Carried: t('Carregado', 'Carried'),
  Equipped: t('Equipado', 'Equipped'),
  Stored: t('Guardado', 'Stored'),
};

/** Catalog categories as the store lists them. */
export const CATEGORY_LABEL: Record<string, string> = {
  Weapon: t('Armas', 'Weapons'), Ammunition: t('Munições', 'Ammunition'), WeaponAttachment: t('Upgrades de armas', 'Weapon attachments'), Gear: t('Itens avulsos', 'Loose gear'), Armor: t('Armaduras e roupas', 'Armor and clothing'),
  Cyberware: 'Cyberware', Netrunning: 'Netrunning', Drug: t('Drogas', 'Drugs'), Vehicle: t('Veículos', 'Vehicles'), VehicleUpgrade: t('Upgrades veiculares', 'Vehicle upgrades'), HqUpgrade: 'QG',
};

export const STAT_LABEL: Record<string, string> = {
  INT: t('Inteligência', 'Intelligence'), REF: t('Reflexos', 'Reflexes'), DEX: t('Destreza', 'Dexterity'), TECH: t('Técnica', 'Technique'), COOL: t('Frieza', 'Cool'),
  WILL: t('Vontade', 'Willpower'), LUCK: t('Sorte', 'Luck'), MOVE: t('Movimento', 'Move'), BODY: t('Corpo', 'Body'), EMP: t('Empatia', 'Empathy'),
};

/** Skill categories in sheet order, with the Portuguese headings of the reference sheet. */
export const SKILL_CATEGORIES: { key: string; label: string }[] = [
  { key: 'Awareness', label: t('Perícias de Atenção', 'Awareness Skills') },
  { key: 'Body', label: t('Perícias Corporais', 'Body Skills') },
  { key: 'Control', label: t('Perícias de Condução', 'Control Skills') },
  { key: 'Education', label: t('Perícias de Educação', 'Education Skills') },
  { key: 'Fighting', label: t('Perícias de Luta', 'Fighting Skills') },
  { key: 'Performance', label: t('Perícias de Performance', 'Performance Skills') },
  { key: 'Ranged Weapon', label: t('Perícias de Armas à Distância', 'Ranged Weapon Skills') },
  { key: 'Social', label: t('Perícias Sociais', 'Social Skills') },
  { key: 'Technique', label: t('Perícias Técnicas', 'Technique Skills') },
];

/** "Idioma (Streetslang)", with the specialization; the English book name goes to the tooltip. */
export function skillLabel(line: Pick<CharacterSkillLine, 'name' | 'namePt' | 'specialization'>): string {
  const base = skillName(line.name, line.namePt);
  return line.specialization ? `${base} (${line.specialization})` : base;
}

export type SkillFilter = 'all' | 'trained' | 'role' | 'combat' | 'basic';

export const SKILL_FILTERS: { value: SkillFilter; label: string }[] = [
  { value: 'all', label: t('Todas', 'All') },
  { value: 'trained', label: t('Com nível', 'With level') },
  { value: 'role', label: t('Da Role', 'Role skills') },
  { value: 'basic', label: t('Básicas', 'Basic') },
  { value: 'combat', label: t('Combate', 'Combat') },
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
    return [l.name, l.namePt ?? '', l.specialization ?? '', l.stat].some((entry) => normalize(entry).includes(q));
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
  return new Date(iso).toLocaleString(locale, { dateStyle: 'short', timeStyle: 'short' });
}
