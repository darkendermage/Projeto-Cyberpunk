import { useEffect, useMemo } from 'react';
import { Link, useSearchParams } from 'react-router';
import { GitCompareArrows, X } from 'lucide-react';
import clsx from 'clsx';
import type { FieldDescriptor, Item } from '../../api/types';
import { useItems } from '../../api/hooks';
import { useSchemas } from '../../api/useSchemas';
import { ItemArt } from '../../components/art/ItemArt';
import { Button, EmptyState, ErrorState, IconButton, Spinner } from '../../components/ui/ui';
import { COMPARE_DIRECTION, DATA_ORIGIN_LABEL, formatAttribute, formatEb } from '../../lib/format';
import { useCompare } from '../../state/CompareContext';
import page from '../../styles/Page.module.css';
import styles from './ComparePage.module.css';
import { t, tv } from '../../i18n';

interface Row {
  key: string;
  label: string;
  cells: { text: string; numeric: number | null; applies: boolean }[];
}

export function ComparePage() {
  const compare = useCompare();
  const [params, setParams] = useSearchParams();
  const urlIds = useMemo(() => (params.get('ids') ?? '').split(',').map(Number).filter((n) => Number.isInteger(n) && n > 0), [params]);
  const ids = urlIds.length > 0 ? urlIds : compare.entries.map((e) => e.id);

  // Keep the URL in sync with the tray so the comparison can be shared as a link.
  useEffect(() => {
    if (urlIds.length === 0 && compare.entries.length > 0) setParams({ ids: compare.entries.map((e) => e.id).join(',') }, { replace: true });
  }, [urlIds.length, compare.entries, setParams]);

  const items = useItems(ids);
  const schemas = useSchemas(items.data?.map((i) => i.category) ?? []);

  const removeItem = (id: number) => {
    compare.remove(id);
    const next = ids.filter((x) => x !== id);
    setParams(next.length ? { ids: next.join(',') } : {}, { replace: true });
  };

  const rows = useMemo(() => buildRows(items.data ?? [], schemas), [items.data, schemas]);
  const categories = new Set(items.data?.map((i) => i.category));

  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch={t('Comparar', 'Compare')}>{t('Comparar', 'Compare')}</h1>
        <p className={page.lead}>{t('Até 4 itens lado a lado. Os melhores valores numéricos de cada linha ficam destacados.', 'Up to 4 items side by side. The best numeric values of each row are highlighted.')}</p>
      </header>

      {ids.length === 0 && (
        <EmptyState title={t('Nada para comparar', 'Nothing to compare')} icon={<GitCompareArrows size={28} />}
          action={<Link to="/catalog/weapons" className={page.link}>{t('Escolher itens no catálogo', 'Pick items in the catalog')}</Link>}>
          {t('Use o botão de comparação nos cards ou na ficha de um item.', 'Use the compare button on the cards or on an item\'s page.')}
        </EmptyState>
      )}
      {items.isLoading && <Spinner />}
      {items.isError && <ErrorState error={items.error} onRetry={() => items.refetch()} />}

      {items.data && items.data.length > 0 && (
        <>
          {categories.size > 1 && (
            <p className={page.note}>{t('Itens de categorias diferentes: atributos que não se aplicam a um item aparecem como “n/a”.', 'Items from different categories: attributes that don\'t apply to an item show as “n/a”.')}</p>
          )}
          <div className={styles.wrap}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th scope="col" className={styles.corner}>
                    <Button onClick={() => { compare.clear(); setParams({}, { replace: true }); }}>{t('Limpar', 'Clear')}</Button>
                  </th>
                  {items.data.map((item) => (
                    <th key={item.id} scope="col" className={styles.itemHead}>
                      <ItemArt item={item} size="card" />
                      <div className={styles.itemTitle}>
                        <strong>{tv(item.name)}</strong>
                        <IconButton label={t(`Remover ${tv(item.name)}`, `Remove ${tv(item.name)}`)} onClick={() => removeItem(item.id)}><X size={14} /></IconButton>
                      </div>
                      <span className={styles.itemSub}>{tv(item.subtype?.label)}</span>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => {
                  const best = bestIndexes(row);
                  return (
                    <tr key={row.key}>
                      <th scope="row">{tv(row.label)}</th>
                      {row.cells.map((cell, i) => (
                        <td key={i} className={clsx(!cell.applies && styles.na, best.has(i) && styles.best)}>{tv(cell.text)}</td>
                      ))}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}

function buildRows(items: Item[], schemas: Map<string, import('../../api/types').CategorySchema>): Row[] {
  const rows: Row[] = [
    {
      key: 'price', label: t('Preço (eb)', 'Price (eb)'),
      cells: items.map((i) => ({ text: i.price != null ? formatEb(i.price) : i.priceNote ?? '—', numeric: i.price ?? null, applies: true })),
    },
    { key: 'manufacturer', label: t('Fabricante', 'Manufacturer'), cells: items.map((i) => ({ text: i.manufacturer?.name ?? '—', numeric: null, applies: true })) },
    { key: 'source', label: t('Fonte', 'Source'), cells: items.map((i) => ({ text: i.source?.name ?? '—', numeric: null, applies: true })) },
  ];

  const fields: FieldDescriptor[] = [];
  const seen = new Set<string>();
  for (const item of items) {
    for (const f of schemas.get(item.category)?.fields ?? []) {
      if (f.base || f.kind === 'longtext' || seen.has(f.key)) continue;
      seen.add(f.key);
      fields.push(f);
    }
  }

  for (const field of fields) {
    const cells = items.map((item) => {
      const own = schemas.get(item.category)?.fields.find((f) => f.key === field.key);
      const subtype = item.subtype?.value;
      const applies = Boolean(own) && (!own!.appliesTo?.length || !subtype || own!.appliesTo.includes(subtype));
      if (!applies) return { text: 'n/a', numeric: null, applies: false };
      const value = formatAttribute(own, item.attributes[field.key]);
      const raw = item.attributes[field.key];
      return { text: value.unit && !value.empty ? `${value.text} ${value.unit}` : value.text, numeric: typeof raw === 'number' ? raw : null, applies: true };
    });
    if (cells.every((c) => c.text === '—' || c.text === 'n/a')) continue;
    rows.push({ key: field.key, label: field.label, cells });
  }

  rows.push({ key: 'origin', label: t('Origem dos dados', 'Data origin'), cells: items.map((i) => ({ text: DATA_ORIGIN_LABEL[i.dataOrigin], numeric: null, applies: true })) });
  return rows;
}

function bestIndexes(row: Row): Set<number> {
  const direction = COMPARE_DIRECTION[row.key];
  const numbers = row.cells.map((c) => c.numeric).filter((n): n is number => n !== null);
  if (!direction || numbers.length < 2 || new Set(numbers).size < 2) return new Set();
  const target = direction === 'high' ? Math.max(...numbers) : Math.min(...numbers);
  return new Set(row.cells.flatMap((c, i) => (c.numeric === target ? [i] : [])));
}
