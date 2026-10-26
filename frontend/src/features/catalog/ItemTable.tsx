import { useEffect, useRef } from 'react';
import clsx from 'clsx';
import { ArrowDown, ArrowUp, ArrowUpDown, BadgeCheck, ExternalLink } from 'lucide-react';
import type { CategorySchema, FieldDescriptor, Item } from '../../api/types';
import { ItemArt } from '../../components/art/ItemArt';
import { formatAttribute, formatEb, optionLabel, shortLabel, weaponClassGroups } from '../../lib/format';
import { MAX_COMPARE, useCompare } from '../../state/CompareContext';
import { useToast } from '../../state/ToastContext';
import { FavoriteButton } from './ItemActions';
import styles from './ItemTable.module.css';
import { t, tv } from '../../i18n';

interface Props {
  items: Item[];
  schema: CategorySchema;
  sort: string;
  dir: 'asc' | 'desc';
  onSort: (sort: string, dir: 'asc' | 'desc') => void;
  onOpen: (item: Item) => void;
}

/** Fields rendered as small "pills" (ammunition type, effect...). */
const PILL_KINDS = new Set(['set', 'enum']);

/** Table view: one row per item, attribute columns declared by the category schema (tableFields). */
export function ItemTable({ items, schema, sort, dir, onSort, onOpen }: Props) {
  const compare = useCompare();
  const toast = useToast();
  const fields = new Map(schema.fields.map((f) => [f.key, f]));
  const columns = schema.tableFields.map((k) => fields.get(k)).filter((f): f is FieldDescriptor => Boolean(f));

  const selectedHere = items.filter((i) => compare.has(i.id)).length;
  const headerBox = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (headerBox.current) headerBox.current.indeterminate = selectedHere > 0 && selectedHere < Math.min(items.length, MAX_COMPARE);
  }, [selectedHere, items.length]);

  const toggleAll = () => {
    if (selectedHere > 0) {
      items.filter((i) => compare.has(i.id)).forEach((i) => compare.remove(i.id));
      return;
    }
    let added = 0;
    for (const item of items) {
      if (compare.toggle(item) !== 'added') break;
      added += 1;
    }
    if (added < items.length) toast.notify(t(`A comparação aceita até ${MAX_COMPARE} itens.`, `The comparison takes up to ${MAX_COMPARE} items.`), 'info');
  };

  const toggleOne = (item: Item) => {
    if (compare.toggle(item) === 'full') toast.notify(t(`A comparação aceita até ${MAX_COMPARE} itens.`, `The comparison takes up to ${MAX_COMPARE} items.`), 'info');
  };

  const sortHeader = (field: string, label: string, align?: 'right') => {
    const active = sort === field;
    return (
      <th key={field} scope="col" className={clsx(align === 'right' && styles.right)} aria-sort={active ? (dir === 'asc' ? 'ascending' : 'descending') : 'none'}>
        <button type="button" className={clsx(styles.sortButton, active && styles.sortActive)}
          onClick={() => onSort(field, active && dir === 'asc' ? 'desc' : 'asc')}>
          {label}
          {active ? (dir === 'asc' ? <ArrowUp size={13} /> : <ArrowDown size={13} />) : <ArrowUpDown size={13} />}
        </button>
      </th>
    );
  };

  return (
    <div className={styles.wrap}>
      <table className={styles.table}>
        <thead>
          <tr>
            <th scope="col" className={styles.check}>
              <input ref={headerBox} type="checkbox" checked={selectedHere > 0 && selectedHere === Math.min(items.length, MAX_COMPARE)}
                onChange={toggleAll} aria-label={selectedHere > 0 ? t('Limpar seleção para comparar', 'Clear comparison selection') : t('Selecionar para comparar', 'Select to compare')} />
            </th>
            {sortHeader('name', t('Nome', 'Name'))}
            <th scope="col">{t('Tipo', 'Type')}</th>
            <th scope="col" className={styles.colMaker}>{t('Fabricante', 'Manufacturer')}</th>
            {columns.map((f) => {
              // Dice columns ("3d6") sort through their numeric twin ("damageDice").
              const sortKey = f.sortable ? f.key : f.kind === 'dice' && fields.get(`${f.key}Dice`)?.sortable ? `${f.key}Dice` : null;
              return sortKey
                ? sortHeader(sortKey, shortLabel(f))
                : <th key={f.key} scope="col" title={tv(f.label)}>{shortLabel(f)}</th>;
            })}
            {sortHeader('price', t('Custo', 'Cost'), 'right')}
            <th scope="col" className={styles.colSource}>{t('Fonte', 'Source')}</th>
            <th scope="col" className={styles.actionsHead}><span className="visually-hidden">{t('Ações', 'Actions')}</span></th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => {
            const selected = compare.has(item.id);
            return (
              <tr key={item.id} className={clsx(selected && styles.selected)}
                onClick={(e) => { if (!(e.target as HTMLElement).closest('button, input, a')) onOpen(item); }}>
                <td className={styles.check}>
                  <input type="checkbox" checked={selected} onChange={() => toggleOne(item)} aria-label={t(`Comparar ${tv(item.name)}`, `Compare ${tv(item.name)}`)} />
                </td>
                <td>
                  <div className={styles.nameCell}>
                    <ItemArt item={item} size="thumb" />
                    <div className={styles.nameText}>
                      <button type="button" className={styles.name} onClick={() => onOpen(item)}>{tv(item.name)}</button>
                      {/* Shown only when the table is too narrow for the Fabricante/Fonte columns. */}
                      <span className={styles.nameSub}>
                        {[tv(item.manufacturer?.name), tv(item.source?.name)].filter(Boolean).join(' · ')}
                      </span>
                    </div>
                    {item.dataOrigin === 'Demo' && <span className={styles.demo} title={t('Dados de demonstração', 'Demo data')}>DEMO</span>}
                    {item.dataOrigin === 'Verified' && (
                      <span className={styles.verified} role="img" aria-label={t('Conferido com PDF oficial', 'Checked against the official PDF')} title={t('Conferido com PDF oficial', 'Checked against the official PDF')}>
                        <BadgeCheck size={16} aria-hidden />
                      </span>
                    )}
                  </div>
                </td>
                <td className={clsx(styles.muted, styles.wrapCell)}>{tv(item.subtype?.label)}</td>
                <td className={clsx(styles.muted, styles.wrapCell, styles.colMaker)}>{tv(item.manufacturer?.name) ?? '—'}</td>
                {columns.map((f) => <AttributeCell key={f.key} field={f} item={item} />)}
                <td className={clsx(styles.right, styles.price)}>
                  {item.price != null ? formatEb(item.price) : <span className={styles.faint}>{item.priceNote ?? '—'}</span>}
                </td>
                <td className={styles.colSource}>
                  {(item.source || item.page) && (
                    <span className={styles.source} title={tv(item.source?.name)}>
                      {tv(item.source?.name) ?? 'p.'}{item.page ? ` p.${item.page}` : ''}
                    </span>
                  )}
                </td>
                <td className={styles.actions}>
                  <FavoriteButton item={item} />
                  <button type="button" className={styles.openButton} onClick={() => onOpen(item)} aria-label={t(`Abrir ficha de ${tv(item.name)}`, `Open ${tv(item.name)}`)} title={t('Abrir ficha', 'Open')}>
                    <ExternalLink size={15} />
                  </button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function AttributeCell({ field, item }: { field: FieldDescriptor; item: Item }) {
  const subtype = item.subtype?.value;
  if (field.appliesTo?.length && subtype && !field.appliesTo.includes(subtype)) return <td className={styles.faint}>—</td>;

  const raw = item.attributes[field.key];
  if (field.kind === 'boolean') {
    return <td className={raw ? styles.yes : styles.no}>{raw ? t('Sim', 'Yes') : t('Não', 'No')}</td>;
  }
  if (PILL_KINDS.has(field.kind) && raw !== null && raw !== undefined && raw !== '') {
    const values = Array.isArray(raw) ? raw : [String(raw)];
    if (values.length === 0) return <td className={styles.faint}>—</td>;
    if (field.key === 'compatibleClasses') {
      const groups = weaponClassGroups(values.map(String), field);
      return (
        <td title={values.map((v) => optionLabel(field, String(v))).join(', ')}>
          {groups.slice(0, 3).map((g) => <span key={g} className={styles.pill}>{g}</span>)}
          {groups.length > 3 && <span className={styles.more}>+{groups.length - 3}</span>}
        </td>
      );
    }
    const labels = values.map((v) => optionLabel(field, v));
    return (
      <td title={labels.join(', ')}>
        <span className={styles.pill}>{labels[0]}</span>
        {labels.length > 1 && <span className={styles.more}>+{labels.length - 1}</span>}
      </td>
    );
  }
  const value = formatAttribute(field, raw);
  return (
    <td className={clsx(value.empty && styles.faint, field.kind === 'text' && styles.text)} title={field.kind === 'text' ? tv(value.text) : undefined}>
      {tv(value.text)}{value.unit && !value.empty && <small> {value.unit}</small>}
    </td>
  );
}
