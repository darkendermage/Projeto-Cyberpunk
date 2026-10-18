import { useMemo, useState } from 'react';
import type { FacetValue, FieldDescriptor, FilterOp } from '../../../api/types';
import { Checkbox, Segmented } from '../../../components/ui/ui';
import { type Criterion, getCriterion, setCriterion, setOperator, toggleValue } from '../../../lib/filters';
import styles from './Filters.module.css';

interface Props {
  field: FieldDescriptor;
  facets: FacetValue[] | undefined;
  criteria: Criterion[];
  onChange: (criteria: Criterion[]) => void;
  columns?: 1 | 2;
  limit?: number;
}

const MODE_LABELS: Partial<Record<FilterOp, string>> = { in: 'Qualquer', all: 'Todos', nin: 'Excluir' };

/**
 * Multi-select with live counts. Only values present in the current result set are listed
 * (plus anything already selected), so users never pick an option that yields nothing.
 */
export function EnumFilter({ field, facets, criteria, onChange, columns = 1, limit = 8 }: Props) {
  const [expanded, setExpanded] = useState(false);
  const criterion = getCriterion(criteria, field.key);
  const selected = useMemo(() => criterion?.values ?? [], [criterion]);
  const defaultOp = (field.defaultOp ?? 'in') as FilterOp;

  const options = useMemo(() => {
    const byValue = new Map<string, { value: string; label: string; count?: number }>();
    if (facets) for (const f of facets) byValue.set(f.value, f);
    else for (const o of field.options ?? []) byValue.set(o.value, o);
    for (const v of selected) {
      if (!byValue.has(v)) byValue.set(v, { value: v, label: field.options?.find((o) => o.value === v)?.label ?? v, count: 0 });
    }
    return [...byValue.values()];
  }, [facets, field.options, selected]);

  if (options.length === 0) return <p className={styles.noOptions}>Sem opções para os filtros atuais.</p>;

  const modes = field.kind === 'set' ? (['all', 'in', 'nin'] as FilterOp[]) : (['in', 'nin'] as FilterOp[]);
  const visible = expanded ? options : options.slice(0, limit);
  const op = criterion?.op ?? defaultOp;

  return (
    <div>
      {selected.length > 0 && (
        <div className={styles.modeRow}>
          <Segmented size="sm" ariaLabel={`Modo do filtro ${field.label}`} value={op}
            onChange={(next) => onChange(setOperator(criteria, field.key, next))}
            options={modes.map((m) => ({ value: m, label: MODE_LABELS[m] }))} />
          <button type="button" className={styles.linkButton} onClick={() => onChange(setCriterion(criteria, field.key, null))}>limpar</button>
        </div>
      )}
      <div className={columns === 2 ? styles.twoCols : styles.oneCol}>
        {visible.map((o) => (
          <Checkbox key={o.value} label={o.label} count={o.count}
            checked={selected.includes(o.value)} muted={o.count === 0}
            onChange={() => onChange(toggleValue(criteria, field.key, o.value, defaultOp))} />
        ))}
      </div>
      {options.length > limit && (
        <button type="button" className={styles.linkButton} onClick={() => setExpanded((e) => !e)}>
          {expanded ? 'mostrar menos' : `mostrar mais (${options.length - limit})`}
        </button>
      )}
    </div>
  );
}
