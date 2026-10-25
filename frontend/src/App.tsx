import { lazy, Suspense } from 'react';
import { Link, Navigate, Route, Routes } from 'react-router';
import { AppShell } from './components/layout/AppShell';
import { EmptyState, Spinner } from './components/ui/ui';
import { CatalogPage } from './features/catalog/CatalogPage';
import { DEFAULT_CATEGORY } from './features/catalog/categoryMeta';
import { CompareTray } from './features/compare/CompareTray';
import page from './styles/Page.module.css';

const FavoritesPage = lazy(() => import('./features/favorites/FavoritesPage').then((m) => ({ default: m.FavoritesPage })));
const ListsPage = lazy(() => import('./features/lists/ListsPage').then((m) => ({ default: m.ListsPage })));
const ComparePage = lazy(() => import('./features/compare/ComparePage').then((m) => ({ default: m.ComparePage })));
const QuickRulesPage = lazy(() => import('./features/reference/ReferencePages').then((m) => ({ default: m.QuickRulesPage })));
const TablesPage = lazy(() => import('./features/reference/ReferencePages').then((m) => ({ default: m.TablesPage })));
const GlossaryPage = lazy(() => import('./features/reference/ReferencePages').then((m) => ({ default: m.GlossaryPage })));
const EnemyGeneratorPage = lazy(() => import('./features/enemies/EnemyGeneratorPage').then((m) => ({ default: m.EnemyGeneratorPage })));
const CharactersPage = lazy(() => import('./features/characters/CharactersPage').then((m) => ({ default: m.CharactersPage })));
const CharacterPage = lazy(() => import('./features/characters/CharacterPage').then((m) => ({ default: m.CharacterPage })));
const MartialArtsPage = lazy(() => import('./features/reference/MartialArtsPage').then((m) => ({ default: m.MartialArtsPage })));
const NightMarketPage = lazy(() => import('./features/night-market/NightMarketPage').then((m) => ({ default: m.NightMarketPage })));
const CombatPage = lazy(() => import('./features/combat/CombatPage').then((m) => ({ default: m.CombatPage })));
const NetArchitecturePage = lazy(() => import('./features/net-architecture/NetArchitecturePage').then((m) => ({ default: m.NetArchitecturePage })));

export default function App() {
  return (
    <AppShell>
      <Suspense fallback={<div className={page.page}><Spinner /></div>}>
        <Routes>
          <Route path="/" element={<Navigate to={`/catalog/${DEFAULT_CATEGORY}`} replace />} />
          <Route path="/catalog" element={<Navigate to={`/catalog/${DEFAULT_CATEGORY}`} replace />} />
          <Route path="/catalog/:category" element={<CatalogPage />} />
          <Route path="/favorites" element={<FavoritesPage />} />
          <Route path="/lists" element={<ListsPage />} />
          <Route path="/lists/:id" element={<ListsPage />} />
          <Route path="/compare" element={<ComparePage />} />
          <Route path="/rules" element={<QuickRulesPage />} />
          <Route path="/tables" element={<TablesPage />} />
          <Route path="/glossary" element={<GlossaryPage />} />
          <Route path="/martial-arts" element={<MartialArtsPage />} />
          <Route path="/enemies" element={<EnemyGeneratorPage />} />
          <Route path="/night-market" element={<NightMarketPage />} />
          <Route path="/night-market/:id" element={<NightMarketPage />} />
          <Route path="/combat" element={<CombatPage />} />
          <Route path="/combat/:id" element={<CombatPage />} />
          <Route path="/net-architecture" element={<NetArchitecturePage />} />
          <Route path="/net-architecture/:id" element={<NetArchitecturePage />} />
          <Route path="/characters" element={<CharactersPage />} />
          <Route path="/characters/:id" element={<CharacterPage />} />
          <Route path="/characters/:id/:tab" element={<CharacterPage />} />
          <Route path="*" element={
            <div className={page.page}>
              <EmptyState title="Sinal perdido" action={<Link to="/" className={page.link}>Voltar ao catálogo</Link>}>
                Esta rota não existe na rede de Night City.
              </EmptyState>
            </div>
          } />
        </Routes>
      </Suspense>
      <CompareTray />
    </AppShell>
  );
}
