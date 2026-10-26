import { useEffect, useState, type ReactNode } from 'react';
import { NavLink, useLocation } from 'react-router';
import clsx from 'clsx';
import {
  BookOpen, Crosshair, FileText, GitCompareArrows, Heart, LayoutGrid, ListChecks, Menu, Network, Search, Settings, Skull, Store, Swords, Table2, Users, X,
} from 'lucide-react';
import { useCompare } from '../../state/CompareContext';
import { useEffectsSettings } from '../../state/EffectsContext';
import { useFavoriteIds } from '../../api/hooks';
import { CommandPalette } from '../../features/search/CommandPalette';
import { SettingsPanel } from '../../features/settings/SettingsPanel';
import { CyberpunkLogo } from '../brand/CyberpunkLogo';
import { CitySkyline } from './CitySkyline';
import { UpdateBanner } from './UpdateBanner';
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

function Sidebar({ open, onClose }: { open: boolean; onClose: () => void }) {
  const compare = useCompare();
  const favorites = useFavoriteIds();
  const location = useLocation();

  const nav = [
    { to: '/catalog/weapons', label: t('Itens', 'Items'), icon: <LayoutGrid size={19} />, active: location.pathname.startsWith('/catalog') },
    { to: '/lists', label: t('Minhas Listas', 'My Lists'), icon: <ListChecks size={19} /> },
    { to: '/favorites', label: t('Favoritos', 'Favorites'), icon: <Heart size={19} />, count: favorites.data?.length },
    { to: '/compare', label: t('Comparar', 'Compare'), icon: <GitCompareArrows size={19} />, count: compare.entries.length },
    { to: '/characters', label: t('Personagens', 'Characters'), icon: <Users size={19} />, active: location.pathname.startsWith('/characters') },
  ];
  const others = [
    { to: '/enemies', label: t('Gerador de Inimigos', 'Enemy Generator'), icon: <Skull size={19} /> },
    { to: '/combat', label: 'Friday Night Firefight', icon: <Crosshair size={19} />, active: location.pathname.startsWith('/combat') },
    { to: '/night-market', label: 'Night Market', icon: <Store size={19} />, active: location.pathname.startsWith('/night-market') },
    { to: '/net-architecture', label: t('Arquiteturas de NET', 'NET Architectures'), icon: <Network size={19} />, active: location.pathname.startsWith('/net-architecture') },
    { to: '/rules', label: t('Regras Rápidas', 'Quick Rules'), icon: <FileText size={19} /> },
    { to: '/tables', label: t('Tabelas', 'Tables'), icon: <Table2 size={19} /> },
    { to: '/martial-arts', label: t('Artes Marciais', 'Martial Arts'), icon: <Swords size={19} /> },
    { to: '/glossary', label: t('Glossário', 'Glossary'), icon: <BookOpen size={19} /> },
  ];

  const renderLink = (l: { to: string; label: string; icon: ReactNode; count?: number; active?: boolean }) => (
    <NavLink key={l.to} to={l.to}
      className={({ isActive }) => clsx(styles.navLink, (l.active ?? isActive) && styles.navActive)}>
      {l.icon}
      <span>{tv(l.label)}</span>
      {l.count ? <span className={styles.navCount}>{l.count}</span> : null}
    </NavLink>
  );

  return (
    <>
      <div className={clsx(styles.scrim, open && styles.scrimOpen)} onClick={onClose} aria-hidden />
      <aside className={clsx(styles.sidebar, open && styles.sidebarOpen)} aria-label={t('Navegação principal', 'Main navigation')}>
        <button type="button" className={styles.sidebarClose} onClick={onClose} aria-label={t('Fechar navegação', 'Close navigation')}>
          <X size={18} />
        </button>
        <nav>
          <p className={styles.navHeading}>{t('Navegação', 'Navigation')}</p>
          {nav.map(renderLink)}
          <p className={styles.navHeading}>{t('Outros', 'Other')}</p>
          {others.map(renderLink)}
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
