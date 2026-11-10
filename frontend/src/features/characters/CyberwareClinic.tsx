// Ripperdoc clinic: the cyberware catalog, filtered by the selected region unless the player asks to see all.
import { useMemo, useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Search, Stethoscope } from 'lucide-react';
import { characterApi } from '../../api/characters';
import type { CharacterDetail, StoreItem } from '../../api/characterTypes';
import { Button, Select, Spinner, Tooltip } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { useDebouncedValue } from '../../lib/hooks';
import { CYBER_TYPE_LABEL, fitsRegion, humanityLossLabel, type RegionKey } from './anatomy';
import { SheetPanel } from './CharacterUi';
import { CyberTypeIcon } from './CyberIcons';
import type { Selection } from './CyberwareDetails';
import { paginate } from './gearLayout';
import { Pager } from './Pager';
import { sortRows } from './sorting';
import base from './Characters.module.css';
import styles from './Cyberware.module.css';
import { t, tv } from '../../i18n';

type ClinicOrder = 'name' | 'priceAsc' | 'priceDesc' | 'type';
const CLINIC_ORDER: Record<ClinicOrder, { label: string; sort: { key: 'name' | 'price' | 'type'; dir: 'asc' | 'desc' } }> = {
  name: { label: t('Nome (A–Z)', 'Name (A–Z)'), sort: { key: 'name', dir: 'asc' } },
  priceAsc: { label: t('Preço crescente', 'Price, low to high'), sort: { key: 'price', dir: 'asc' } },
  priceDesc: { label: t('Preço decrescente', 'Price, high to low'), sort: { key: 'price', dir: 'desc' } },
  type: { label: t('Categoria', 'Category'), sort: { key: 'type', dir: 'asc' } },
};

/** Catalog rows per page: short pages keep the clinic as tall as the columns beside it. */
const CLINIC_PAGE = 6;

export function Clinic({ detail, region, regionLabel, showAll, onShowAll, replacingName, selection, onPick }: {
  detail: CharacterDetail; region: RegionKey; regionLabel: string | null; showAll: boolean; onShowAll: (all: boolean) => void;
  replacingName: string | null; selection: Selection; onPick: (item: StoreItem) => void;
}) {
  const [query, setQuery] = useState('');
  const [type, setType] = useState('');
  const [order, setOrder] = useState<ClinicOrder>('name');
  const q = useDebouncedValue(query, 250);
  const catalog = useQuery({
    queryKey: ['cyber-catalog', q],
    queryFn: ({ signal }) => characterApi.store(q, 'Cyberware', 1, 1000, signal),
    placeholderData: keepPreviousData,
    select: (page) => page.items,
  });
  const filterRegion = showAll ? null : region;
  const installed = new Set(detail.report.cyberware.map((c) => c.itemId));
  const budget = detail.validation.gear ? detail.validation.gear.remaining : detail.sheet.wallet.balance;
  const creating = !detail.sheet.creation.finalized;
  // The category list follows the region filter, so it never offers categories the region can't take.
  const regionItems = useMemo(() => (catalog.data ?? []).filter((i) => fitsRegion(i, filterRegion)), [catalog.data, filterRegion]);
  const types = useMemo(() => [...new Set(regionItems.map((i) => i.cyberType).filter(Boolean))] as string[], [regionItems]);
  const activeType = types.includes(type) ? type : '';
  const filtered = sortRows(regionItems.filter((i) => !activeType || i.cyberType === activeType), CLINIC_ORDER[order].sort,
    (i, k) => k === 'price' ? i.price : k === 'type' ? CYBER_TYPE_LABEL[i.cyberType ?? ''] ?? i.cyberType : tv(i.name));
  // A new search, filter or order goes back to the first page.
  const filter = `${q}|${activeType}|${filterRegion}|${order}`;
  const [paging, setPaging] = useState({ filter, page: 0 });
  const shown = paginate(filtered, paging.filter === filter ? paging.page : 0, CLINIC_PAGE);

  return (
    <SheetPanel title={t('Clínica de ripperdoc', 'Ripperdoc clinic')} icon={<Stethoscope size={16} />} className={styles.clinic}>
      <label className={`${base.search} ${styles.clinicSearch}`}>
        <Search size={15} aria-hidden />
        <span className="visually-hidden">{t('Buscar cyberware', 'Search cyberware')}</span>
        <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t('Buscar cyberware…', 'Search cyberware…')} />
      </label>
      <div className={styles.clinicToolbar}>
        <Select value={activeType} onChange={(e) => setType(e.target.value)} aria-label={t('Categoria', 'Category')} highlighted={!showAll}>
          <option value="">{!showAll && regionLabel ? t(`Todas de ${regionLabel}`, `All for ${regionLabel}`) : t('Todas as categorias', 'All categories')}</option>
          {types.map((entry) => <option key={entry} value={entry}>{CYBER_TYPE_LABEL[entry] ?? entry}</option>)}
        </Select>
        <Select value={order} onChange={(e) => setOrder(e.target.value as ClinicOrder)} aria-label={t('Ordenar por', 'Sort by')}>
          {(Object.keys(CLINIC_ORDER) as ClinicOrder[]).map((k) => <option key={k} value={k}>{CLINIC_ORDER[k].label}</option>)}
        </Select>
      </div>
      <div className={base.rowBetween}>
        <span className={base.small}>
          {showAll || !regionLabel ? t('Todo o catálogo', 'Whole catalog') : t(`Cabe em ${regionLabel}`, `Fits ${regionLabel}`)}
        </span>
        <Button variant="ghost" onClick={() => onShowAll(!showAll)}>{showAll ? t('Filtrar pela região', 'Filter by region') : t('Ver tudo', 'See all')}</Button>
      </div>
      {replacingName && <p className={`${base.small} ${base.amber}`}>{t(`Escolha o substituto de ${tv(replacingName)}.`, `Pick the replacement for ${tv(replacingName)}.`)}</p>}
      {catalog.isLoading && <Spinner />}
      <div className={styles.catalogList} role="listbox" aria-label={t('Catálogo de cyberware', 'Cyberware catalog')}>
        {shown.rows.map((i) => {
          const active = selection?.kind === 'catalog' && selection.item.id === i.id;
          const pricey = (i.price ?? 0) > budget;
          return (
            <Tooltip key={i.id} content={i.effect ?? i.summary}>
              <button type="button" role="option" aria-selected={active}
                className={`${styles.catalogItem} ${active ? styles.catalogItemActive : ''}`} onClick={() => onPick(i)}>
                <span className={styles.catalogIcon}><CyberTypeIcon type={i.cyberType} /></span>
                <span className={styles.catalogName}>{tv(i.name)}</span>
                <span className={styles.price}>{formatEb(i.price)} eb</span>
                <span className={styles.catalogMeta}>
                  <span className={base.tag}>{CYBER_TYPE_LABEL[i.cyberType ?? ''] ?? i.cyberType}</span>
                  <span>{humanityLossLabel(i.humanityLoss, i.humanityLossDice, creating)}</span>
                  {installed.has(i.id) && <span className={`${base.tag} ${base.tagRole}`}>{t('instalado', 'installed')}</span>}
                  {pricey && <span className={base.tag} style={{ color: 'var(--amber)' }}>{t('acima do saldo', 'over the balance')}</span>}
                </span>
              </button>
            </Tooltip>
          );
        })}
        {catalog.data && shown.rows.length === 0 && <p className={base.muted}>{t('Nada com esse filtro.', 'Nothing with this filter.')}</p>}
      </div>
      {catalog.data && (
        <Pager page={shown.page} pages={shown.pages} rows={shown.rows.length} total={filtered.length} noun={t('itens', 'items')}
          onPage={(page) => setPaging({ filter, page })} size={CLINIC_PAGE} />
      )}
    </SheetPanel>
  );
}
