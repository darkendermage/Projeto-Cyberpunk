import { useState } from 'react';
import { useMarketIndex } from '../../api/nightMarket';
import type { NightMarketRules, PriceCategory } from '../../api/nightMarketTypes';
import { Badge, Button, Select, Spinner } from '../../components/ui/ui';
import { PRICE_CATEGORY_LABEL } from '../../lib/format';
import { useDebouncedValue } from '../../lib/hooks';
import page from '../../styles/Page.module.css';
import { ItemDetailModal } from '../catalog/ItemDetailModal';
import styles from './NightMarket.module.css';
import { t, tv } from '../../i18n';

const PAGE_SIZE = 50;

/** The Night Market Index (v1.1): every item with its cost and sources, linked to the catalog sheet when there is one. */
export function IndexView({ rules }: { rules: NightMarketRules }) {
  const [q, setQ] = useState('');
  const [section, setSection] = useState('');
  const [category, setCategory] = useState('');
  const [catalogued, setCatalogued] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [detail, setDetail] = useState<number | null>(null);
  const debounced = useDebouncedValue(q, 250);
  const index = useMarketIndex({
    q: debounced, section: section || null, category: (category || null) as PriceCategory | null,
    catalogued: catalogued === '' ? null : catalogued === 'yes', page: pageNumber, pageSize: PAGE_SIZE,
  });
  const pages = index.data ? Math.max(1, Math.ceil(index.data.total / PAGE_SIZE)) : 1;
  const reset = () => setPageNumber(1);

  return (
    <section className={page.section}>
      <p className={page.lead}>
        {t('O índice da R. Talsorian (Night Market Index v1.1, jan. 2025) lista o custo e a página de cada item. Os que têm ficha no catálogo abrem a ficha; os outros podem ser postos à venda num mercado como listagem só do índice.', 'R. Talsorian\'s index (Night Market Index v1.1, Jan. 2025) lists each item\'s cost and page. Items with a catalog page open it; the others can be put on sale in a market as an index-only listing.')}
      </p>
      <div className={styles.row} style={{ margin: '12px 0' }}>
        <input className={styles.search} value={q} onChange={(e) => { setQ(e.target.value); reset(); }} placeholder={t('Buscar no índice', 'Search the index')} aria-label={t('Buscar', 'Search')} />
        <Select value={section} onChange={(e) => { setSection(e.target.value); reset(); }} aria-label={t('Seção', 'Section')}>
          <option value="">{t('Todas as seções', 'All sections')}</option>
          {rules.indexSections.map((s) => <option key={s.id} value={s.id}>{tv(s.label)} ({s.rows})</option>)}
        </Select>
        <Select value={category} onChange={(e) => { setCategory(e.target.value); reset(); }} aria-label={t('Categoria de preço', 'Price category')}>
          <option value="">{t('Todas as categorias', 'All categories')}</option>
          {Object.entries(PRICE_CATEGORY_LABEL).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </Select>
        <Select value={catalogued} onChange={(e) => { setCatalogued(e.target.value); reset(); }} aria-label={t('No catálogo', 'In the catalog')}>
          <option value="">{t('Catalogados ou não', 'Catalogued or not')}</option>
          <option value="yes">{t('Com ficha no catálogo', 'With a catalog page')}</option>
          <option value="no">{t('Só no índice', 'Index only')}</option>
        </Select>
      </div>
      {index.isLoading ? <Spinner /> : (
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead>
              <tr><th>Item</th><th>{t('Grupo', 'Group')}</th><th>{t('Custo', 'Cost')}</th><th>{t('Fonte', 'Source')}</th><th>{t('Catálogo', 'Catalog')}</th></tr>
            </thead>
            <tbody>
              {(index.data?.rows ?? []).map((r) => (
                <tr key={r.row}>
                  <td>{r.itemId ? <button type="button" className={page.link} onClick={() => setDetail(r.itemId!)}>{tv(r.name)}</button> : tv(r.name)}</td>
                  <td className={styles.muted}>{tv(r.group)}</td>
                  <td>{r.cost}</td>
                  <td className={styles.source}>{r.sources.map((s) => `${s.book}${s.page ? ` ${s.page}` : ''}`).join(', ')}</td>
                  <td>{r.itemId ? <Badge tone="cyan">{t('ficha', 'page')}</Badge> : <Badge>{t('só índice', 'index only')}</Badge>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className={styles.pager} style={{ marginTop: 10 }}>
        <span className={styles.source}>{index.data?.total ?? 0} {t('linhas', 'rows')}</span>
        <Button disabled={pageNumber <= 1} onClick={() => setPageNumber(pageNumber - 1)}>{t('Anterior', 'Previous')}</Button>
        <span className={styles.source}>{pageNumber} / {pages}</span>
        <Button disabled={pageNumber >= pages} onClick={() => setPageNumber(pageNumber + 1)}>{t('Próxima', 'Next')}</Button>
      </div>
      <ItemDetailModal itemId={detail} onClose={() => setDetail(null)} />
    </section>
  );
}
