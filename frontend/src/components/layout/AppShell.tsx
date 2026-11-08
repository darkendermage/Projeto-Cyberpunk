import { useEffect, useRef, useState, type ReactNode } from 'react';
import { NavLink, useLocation } from 'react-router';
import clsx from 'clsx';
import {
  BookOpen, BookUser, ChevronRight, CloudRain, Crosshair, Dices, FileText, FolderHeart, GitCompareArrows, Heart, LayoutGrid, Library, ListChecks,
  Map as MapIcon, Menu, Network, Search, Settings, Skull, Store, Swords, Table2, Users, X,
} from 'lucide-react';
import { useCompare } from '../../state/CompareContext';
import { useEffectsSettings } from '../../state/EffectsContext';
import { useFavoriteIds } from '../../api/hooks';
import { CommandPalette } from '../../features/search/CommandPalette';
import { SettingsPanel } from '../../features/settings/SettingsPanel';
import { CyberpunkLogo } from '../brand/CyberpunkLogo';
import { CitySkyline } from './CitySkyline';
import { UpdateBanner } from './UpdateBanner';
import { groupOf, isActive, NAV, type NavIcon, type NavItem } from './navigation';
import styles from './AppShell.module.css';
import { t, tv } from '../../i18n';

export function AppShell({ children }: { children: ReactNode }) {
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [navOpen, setNavOpen] = useState(false);
  const { settings } = useEffectsSettings();
  const location = useLocation();

  useEffect(() => setNavOpen(false), [location.pathname]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setPaletteOpen(true);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  return (
    <div className={styles.shell}>
      <a href="#main" className={styles.skip}>{t('Pular para o conteúdo', 'Skip to content')}</a>

      <header className={styles.header}>
        {settings.cityArt && <CitySkyline />}
        <div className={styles.headerShade} aria-hidden />

        <button type="button" className={styles.menuButton} onClick={() => setNavOpen(true)} aria-label={t('Abrir navegação', 'Open navigation')}>
          <Menu size={20} />
        </button>

        <NavLink to="/catalog/weapons" className={styles.brand} aria-label={t('Night City Item Database — início', 'Night City Item Database — home')}>
          <CyberpunkLogo className={styles.logo} />
          <span className={styles.brandText}>
            <span>Night City</span>
            <span>Item Database</span>
          </span>
        </NavLink>

        <div className={styles.headerRight}>
          <span className={styles.tagline} aria-hidden>The city<br />never forgets</span>
          <div className={styles.headerActions}>
            <button type="button" className={styles.searchTrigger} onClick={() => setPaletteOpen(true)}>
              <Search size={17} />
              <span className={styles.searchPlaceholder}>{t('Buscar itens...', 'Search items...')}</span>
              <kbd>Ctrl</kbd><kbd>K</kbd>
            </button>
            <button type="button" className={styles.settingsButton} onClick={() => setSettingsOpen(true)} aria-label={t('Configurações', 'Settings')}>
              <Settings size={18} />
            </button>
          </div>
        </div>
      </header>

      <UpdateBanner />

      <div className={styles.body}>
        <Sidebar open={navOpen} onClose={() => setNavOpen(false)} />
        <main id="main" className={styles.main}>{children}</main>
      </div>

      <CommandPalette open={paletteOpen} onClose={() => setPaletteOpen(false)} />
      <SettingsPanel open={settingsOpen} onClose={() => setSettingsOpen(false)} />
    </div>
  );
}

const ICONS: Record<NavIcon, ReactNode> = {
  items: <LayoutGrid size={19} />, collection: <FolderHeart size={19} />, lists: <ListChecks size={19} />, favorites: <Heart size={19} />,
  compare: <GitCompareArrows size={19} />, characters: <Users size={19} />, gm: <Dices size={19} />, enemies: <Skull size={19} />,
  npcs: <BookUser size={19} />, combat: <Crosshair size={19} />, market: <Store size={19} />, net: <Network size={19} />,
  table: <MapIcon size={19} />, other: <Library size={19} />, weather: <CloudRain size={19} />, rules: <FileText size={19} />,
  tables: <Table2 size={19} />, martial: <Swords size={19} />, glossary: <BookOpen size={19} />,
};

// Items, Characters and the Table on their own; the rest are groups that open a side panel (inline in the
// phone drawer). Scrolls with the page.
function Sidebar({ open, onClose }: { open: boolean; onClose: () => void }) {
  const compare = useCompare();
  const favorites = useFavoriteIds();
  const { pathname } = useLocation();
  // The open group remembers the page it was opened on: moving to another page closes it.
  const [opened, setOpened] = useState<{ id: string; path: string } | null>(null);
  const group = opened?.path === pathname ? opened.id : null;
  const setGroup = (id: string | null) => setOpened(id ? { id, path: pathname } : null);
  const ref = useRef<HTMLElement>(null);
  const counts = { favorites: favorites.data?.length ?? 0, compare: compare.entries.length };
  const current = groupOf(pathname);

  useEffect(() => {
    if (!group) return;
    const onDown = (e: PointerEvent) => { if (!ref.current?.contains(e.target as Node)) setOpened(null); };
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Escape') return;
      ref.current?.querySelector<HTMLButtonElement>(`[data-group="${group}"]`)?.focus();
      setOpened(null);
    };
    document.addEventListener('pointerdown', onDown);
    document.addEventListener('keydown', onKey);
    return () => { document.removeEventListener('pointerdown', onDown); document.removeEventListener('keydown', onKey); };
  }, [group]);

  const renderLink = (l: NavItem) => {
    const count = l.count ? counts[l.count] : 0;
    return (
      <NavLink key={l.to} to={l.to} className={clsx(styles.navLink, isActive(pathname, l.to, l.match) && styles.navActive)}>
        {ICONS[l.icon]}
        <span>{tv(l.label)}</span>
        {count ? <span className={styles.navCount}>{count}</span> : null}
      </NavLink>
    );
  };

  return (
    <>
      <div className={clsx(styles.scrim, open && styles.scrimOpen)} onClick={onClose} aria-hidden />
      <aside ref={ref} className={clsx(styles.sidebar, open && styles.sidebarOpen)} aria-label={t('Navegação principal', 'Main navigation')}>
        <button type="button" className={styles.sidebarClose} onClick={onClose} aria-label={t('Fechar navegação', 'Close navigation')}>
          <X size={18} />
        </button>
        <nav>
          {NAV.map((e) => {
            if (e.kind === 'link') return renderLink(e);
            const expanded = group === e.id;
            const badge = e.items.reduce((n, i) => n + (i.count ? counts[i.count] : 0), 0);
            return (
              <div key={e.id} className={styles.navGroup}>
                <button type="button" data-group={e.id} aria-expanded={expanded} aria-controls={`nav-${e.id}`}
                  className={clsx(styles.navLink, styles.navGroupButton, current === e.id && styles.navActive, expanded && styles.navGroupOpen)}
                  onClick={() => setGroup(expanded ? null : e.id)}>
                  {ICONS[e.icon]}
                  <span>{e.label}</span>
                  {badge ? <span className={styles.navCount}>{badge}</span> : null}
                  <ChevronRight size={16} className={styles.navChevron} />
                </button>
                {expanded && (
                  <div id={`nav-${e.id}`} className={styles.flyout} role="group" aria-label={e.label}>
                    <p className={styles.navHeading}>{e.label}</p>
                    {e.items.map(renderLink)}
                  </div>
                )}
              </div>
            );
          })}
        </nav>

        <div className={styles.sidebarArt} aria-hidden>
          <div className={styles.artRings} />
          <span className={styles.katakana}>ナイトシティ</span>
          <span className={styles.motto}>Night city<br />still has<br />a future</span>
          <div className={styles.edition}>
            <span>2020 / RED</span>
            <span className={styles.barcode} />
          </div>
        </div>
      </aside>
    </>
  );
}
