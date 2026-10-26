import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useParams } from 'react-router';
import { ArrowDownWideNarrow, ArrowUpNarrowWide, FilterX, LayoutGrid, SlidersHorizontal, Table2, Undo2 } from 'lucide-react';
import type { Item } from '../../api/types';
import { useCatalogSearch, useCategories, useSchema } from '../../api/hooks';
import { Button, EmptyState, ErrorState, IconButton, Modal, Select, Spinner } from '../../components/ui/ui';
import { type Criterion, toApiCriteria } from '../../lib/filters';
import { formatNumber } from '../../lib/format';
import { useMediaQuery } from '../../lib/hooks';
import { ActiveFilters } from './ActiveFilters';
import { CategoryTabs } from './CategoryTabs';
import { DEFAULT_CATEGORY } from './categoryMeta';
import { FilterPanel } from './filters/FilterPanel';
import { ItemCard } from './ItemCard';
import { ItemDetailModal } from './ItemDetailModal';
import { ItemTable } from './ItemTable';
import { Pagination } from './Pagination';
import { useCatalogState } from './useCatalogState';
import styles from './CatalogPage.module.css';
import { t, tv } from '../../i18n';

export function CatalogPage() {
  const { category = DEFAULT_CATEGORY } = useParams();
  const categories = useCategories();
  const schemaQuery = useSchema(category);
  const schema = schemaQuery.data;
  const state = useCatalogState();
  const { update } = state;
  const wide = useMediaQuery('(min-width: 1201px)');
  const [filtersOpen, setFiltersOpen] = useState(false);
  const topRef = useRef<HTMLElement>(null);

  useEffect(() => { if (wide) setFiltersOpen(false); }, [wide]);

  const apiFilters = useMemo(() => toApiCriteria(state.criteria, schema), [state.criteria, schema]);
  const sortKey = state.sort ?? schema?.defaultSort ?? 'name';
  const search = useCatalogSearch(category, {
    q: state.q, filters: apiFilters, sort: state.sort, dir: state.dir, page: state.page, pageSize: state.per,
  }, Boolean(schema));

  const result = search.data;
  const items = result?.items ?? [];
  const total = result?.total;
  const facets = result?.facets;

  // A page beyond the end (e.g. after narrowing the filters) falls back to the last one.
  useEffect(() => {
    if (result && result.totalPages > 0 && state.page > result.totalPages) update({ page: result.totalPages });
  }, [result, state.page, update]);

  const setCriteria = useCallback((criteria: Criterion[]) => update({ criteria }), [update]);
  const setQuery = useCallback((q: string) => update({ q }), [update]);
  const clearAll = useCallback(() => update({ criteria: [], q: '' }), [update]);
  const openItem = useCallback((item: Item) => update({ item: item.id }, { push: true }), [update]);
  const closeItem = useCallback(() => update({ item: null }), [update]);
  const setSort = useCallback((sort: string, dir: 'asc' | 'desc') => update({ sort, dir }), [update]);
  const goToPage = (page: number) => {
    update({ page }, { push: true });
    topRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  if (schemaQuery.isError) {
    return (
      <div className={styles.page}>
        {categories.data && <CategoryTabs categories={categories.data} view={state.view} />}
        <div className={styles.errorWrap}>
          <ErrorState error={schemaQuery.error} onRetry={() => schemaQuery.refetch()} />
          <Link to={`/catalog/${DEFAULT_CATEGORY}`} className={styles.backLink}>{t('Voltar para Armas', 'Back to Weapons')}</Link>
        </div>
      </div>
    );
  }

  const filterPanel = schema && (
    <FilterPanel schema={schema} facets={facets} criteria={state.criteria} q={state.q}
      onCriteria={setCriteria} onQuery={setQuery} onClear={clearAll} onApplied={() => setFiltersOpen(false)} />
  );
  const activeCount = state.criteria.length + (state.q ? 1 : 0);
  const busy = search.isFetching;

  return (
    <div className={styles.page}>
      {categories.data && <CategoryTabs categories={categories.data} view={state.view} />}

      <div className={styles.layout}>
        <section ref={topRef} className={styles.results} aria-busy={busy}>
          <header className={styles.heading}>
            <div className={styles.headingText}>
              <h1 className="hud-title" data-glitch={schema?.label ?? ''}>{tv(schema?.label) ?? '...'}</h1>
              {schema && <p className={styles.description}>{tv(schema.description)}</p>}
            </div>
            <div className={styles.headingControls}>
              <div className={styles.viewToggle} role="group" aria-label={t('Visualização', 'View')}>
                <Button active={state.view === 'table'} icon={<Table2 size={16} />} aria-pressed={state.view === 'table'}
                  onClick={() => update({ view: 'table' })}>{t('Tabela', 'Table')}</Button>
                <Button active={state.view === 'grid'} icon={<LayoutGrid size={16} />} aria-pressed={state.view === 'grid'}
                  onClick={() => update({ view: 'grid' })}>{t('Grade', 'Grid')}</Button>
              </div>
              {!wide && (
                <Button icon={<SlidersHorizontal size={16} />} onClick={() => setFiltersOpen(true)} active={activeCount > 0}>
                  {t('Filtros', 'Filters')}{activeCount > 0 ? ` (${activeCount})` : ''}
                </Button>
              )}
              <span className={styles.count} aria-live="polite">
                {busy && <Spinner label={t('Atualizando resultados', 'Updating results')} />}
                {total !== undefined && <><strong>{formatNumber(total)}</strong> {total === 1 ? t('item encontrado', 'item found') : t('itens encontrados', 'items found')}</>}
              </span>
            </div>
          </header>

          {schema && state.view === 'grid' && (
            <div className={styles.sortRow}>
              <label className={styles.sortLabel} htmlFor="sort-select">{t('Ordenar por', 'Sort by')}</label>
              <Select id="sort-select" value={sortKey} onChange={(e) => setSort(e.target.value, state.dir)} className={styles.sortSelect}>
                {schema.sortOptions.map((o) => <option key={o.key} value={o.key}>{tv(o.label)}</option>)}
              </Select>
              <IconButton label={state.dir === 'asc' ? t('Ordem crescente (clique para inverter)', 'Ascending order (click to reverse)') : t('Ordem decrescente (clique para inverter)', 'Descending order (click to reverse)')}
                onClick={() => setSort(sortKey, state.dir === 'asc' ? 'desc' : 'asc')}>
                {state.dir === 'asc' ? <ArrowUpNarrowWide size={17} /> : <ArrowDownWideNarrow size={17} />}
              </IconButton>
            </div>
          )}

          {schema && (
            <ActiveFilters schema={schema} facets={facets} criteria={state.criteria} q={state.q}
              onCriteria={setCriteria} onQuery={setQuery} onClear={clearAll} />
          )}

          {search.isError && <ErrorState error={search.error} onRetry={() => search.refetch()} />}
          {!search.isError && !result && <div className={styles.loading}><Spinner /></div>}

          {!search.isError && result && items.length === 0 && (
            <EmptyState title={t('Nenhum item encontrado', 'No items found')}
              action={
                <div className={styles.emptyActions}>
                  {state.criteria.length > 0 && (
                    <Button icon={<Undo2 size={15} />} onClick={() => setCriteria(state.criteria.slice(0, -1))}>{t('Desfazer último filtro', 'Undo last filter')}</Button>
                  )}
                  <Button variant="primary" icon={<FilterX size={15} />} onClick={clearAll}>{t('Limpar filtros', 'Clear filters')}</Button>
                </div>
              }>
              {t('Nenhum registro de ', 'No ')}<strong>{tv(schema?.label)}</strong> {t('atende a todos os critérios ao mesmo tempo. Os filtros são combinados com ', 'record matches every criterion at once. Filters are combined with ')}<strong>E</strong>{t(': remova algum critério ou amplie os intervalos.', ': remove a criterion or widen the ranges.')}
            </EmptyState>
          )}

          {schema && items.length > 0 && (
            <div className={busy ? styles.stale : undefined}>
              {state.view === 'table' ? (
                <ItemTable items={items} schema={schema} sort={sortKey} dir={state.dir} onSort={setSort} onOpen={openItem} />
              ) : (
                <div className={styles.grid}>
                  {items.map((item) => <ItemCard key={item.id} item={item} schema={schema} onOpen={openItem} />)}
                </div>
              )}
            </div>
          )}

          {result && result.total > 0 && (
            <Pagination page={result.page} totalPages={result.totalPages} pageSize={state.per}
              onPage={goToPage} onPageSize={(per) => update({ per })} />
          )}

          <p className={styles.dataNote}>
            {t('Catálogo carregado a partir do compêndio fornecido. Itens com o selo verde foram conferidos com os PDFs oficiais; os demais ainda não foram verificados contra os livros. Cada ficha informa a origem do registro.', 'Catalog loaded from the provided compendium. Items with the green seal were checked against the official PDFs; the rest haven\'t been verified against the books yet. Each item page shows where its record came from.')}
          </p>
        </section>

        {wide && <aside className={styles.filters} aria-label={t('Filtros', 'Filters')}>{filterPanel}</aside>}
      </div>

      {!wide && (
        <Modal open={filtersOpen} onClose={() => setFiltersOpen(false)} variant="drawer-right">
          {filterPanel}
        </Modal>
      )}

      <ItemDetailModal itemId={state.itemId} onClose={closeItem} />
    </div>
  );
}
