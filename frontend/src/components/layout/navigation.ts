import { t } from '../../i18n';

export type NavIcon =
  | 'items' | 'collection' | 'lists' | 'favorites' | 'compare' | 'characters' | 'gm' | 'enemies' | 'npcs' | 'combat'
  | 'market' | 'net' | 'table' | 'other' | 'weather' | 'rules' | 'tables' | 'martial' | 'glossary';

export interface NavItem {
  to: string;
  label: string;
  icon: NavIcon;
  /** Path prefix that marks it as the open page; defaults to `to`. */
  match?: string;
  count?: 'favorites' | 'compare';
}

export type NavEntry = ({ kind: 'link' } & NavItem) | { kind: 'group'; id: string; label: string; icon: NavIcon; items: NavItem[] };

/** The main menu: a few pages on their own, the rest in groups that open a side panel. */
export const NAV: NavEntry[] = [
  { kind: 'link', to: '/catalog/weapons', match: '/catalog', label: t('Itens', 'Items'), icon: 'items' },
  {
    kind: 'group', id: 'collection', label: t('Minha coleção', 'My collection'), icon: 'collection', items: [
      { to: '/lists', label: t('Minhas Listas', 'My Lists'), icon: 'lists' },
      { to: '/favorites', label: t('Favoritos', 'Favorites'), icon: 'favorites', count: 'favorites' },
      { to: '/compare', label: t('Comparar', 'Compare'), icon: 'compare', count: 'compare' },
    ],
  },
  { kind: 'link', to: '/characters', label: t('Personagens', 'Characters'), icon: 'characters' },
  {
    kind: 'group', id: 'gm', label: t('Ferramentas de mestre', 'GM tools'), icon: 'gm', items: [
      { to: '/enemies', label: t('Gerador de Inimigos', 'Enemy Generator'), icon: 'enemies' },
      { to: '/npcs', label: t('Compêndio de NPCs', 'NPC Compendium'), icon: 'npcs' },
      { to: '/combat', label: 'Friday Night Firefight', icon: 'combat' },
      { to: '/night-market', label: 'Night Market', icon: 'market' },
      { to: '/net-architecture', label: t('Arquiteturas de NET', 'NET Architectures'), icon: 'net' },
    ],
  },
  { kind: 'link', to: '/mesa', label: t('Mesa Virtual', 'Virtual Table'), icon: 'table' },
  {
    kind: 'group', id: 'other', label: t('Outros', 'Other'), icon: 'other', items: [
      { to: '/weather', label: t('Clima de Night City', 'Night City Weather'), icon: 'weather' },
      { to: '/rules', label: t('Regras Rápidas', 'Quick Rules'), icon: 'rules' },
      { to: '/tables', label: t('Tabelas', 'Tables'), icon: 'tables' },
      { to: '/martial-arts', label: t('Artes Marciais', 'Martial Arts'), icon: 'martial' },
      { to: '/glossary', label: t('Glossário', 'Glossary'), icon: 'glossary' },
    ],
  },
];

/** The page at `pathname` is `to` (or `match`) or one under it. */
export function isActive(pathname: string, to: string, match?: string): boolean {
  const prefix = match ?? to;
  return pathname === prefix || pathname.startsWith(`${prefix}/`);
}

/** The group holding the open page, to mark it in the menu. */
export function groupOf(pathname: string): string | null {
  for (const e of NAV) if (e.kind === 'group' && e.items.some((i) => isActive(pathname, i.to, i.match))) return e.id;
  return null;
}
