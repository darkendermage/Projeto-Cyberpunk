import type { FacetValue, FieldDescriptor } from '../../../api/types';
import { Select } from '../../../components/ui/ui';
import { type Criterion, getCriterion, setCriterion } from '../../../lib/filters';
import { formatAttribute, optionLabel } from '../../../lib/format';
import styles from './Filters.module.css';

interface Props {
  field: FieldDescriptor;
  criteria: Criterion[];
  onChange: (criteria: Criterion[]) => void;
}

const CUSTOM = '__custom';

/** Single-choice picker ("Todos" + values with counts) for enums and sets such as manufacturer or ammo type. */
export function SelectFilter({ field, criteria, onChange, facets }: Props & { facets: FacetValue[] | undefined }) {
  const criterion = getCriterion(criteria, field.key);
  const single = criterion && criterion.values.length === 1 && ['in', 'eq', 'all'].includes(criterion.op);
  const value = !criterion ? '' : single ? criterion.values[0] : CUSTOM;

  const options: { value: string; label: string; count?: number }[] = facets
    ? facets.map((f) => ({ value: f.value, label: f.label, count: f.count }))
    : (field.options ?? []).map((o) => ({ value: o.value, label: o.label }));
  if (single && !options.some((o) => o.value === value)) options.push({ value, label: optionLabel(field, value), count: 0 });

  return (
    <Select value={value} highlighted={value !== ''} aria-label={field.label}
      onChange={(e) => {
        const v = e.target.value;
        if (v === CUSTOM) return;
        onChange(setCriterion(criteria, field.key, v ? { field: field.key, op: 'in', values: [v] } : null));
      }}>
      <option value="">Todos</option>
      {value === CUSTOM && (
        <option value={CUSTOM}>
          {criterion!.op === 'nin' ? 'Exceto ' : ''}{criterion!.values.map((v) => optionLabel(field, v)).join(', ')}
        </option>
      )}
      {options.map((o) => (
        <option key={o.value} value={o.value}>{o.label}{o.count !== undefined ? ` (${o.count})` : ''}</option>
      ))}
    </Select>
  );
}

/** Todos / Sim / Não for boolean attributes. */
export function BooleanSelect({ field, criteria, onChange }: Props) {
  const criterion = getCriterion(criteria, field.key);
  const value = criterion?.values[0] ?? '';
  return (
    <Select value={value} highlighted={value !== ''} aria-label={field.label}
      onChange={(e) => onChange(setCriterion(criteria, field.key,
        e.target.value ? { field: field.key, op: 'eq', values: [e.target.value] } : null))}>
      <option value="">Todos</option>
      <option value="true">Sim</option>
      <option value="false">Não</option>
    </Select>
  );
}

/** Reads a numeric criterion as a min/max pair; null when it uses an operator a range cannot show (≠, sem valor). */
export function asRange(criterion: Criterion | undefined): [string, string] | null {
  if (!criterion) return ['', ''];
  const [a = '', b = ''] = criterion.values;
  switch (criterion.op) {
    case 'between': return [a, b];
    case 'eq': return [a, a];
    case 'gte': return [a, ''];
    case 'lte': return ['', a];
    default: return null;
  }
}

/**
 * "Qualquer ▾  Qualquer ▾" — minimum and maximum. Small domains (ROF, dice, range bands) list their
 * real values; larger ones (magazine, weight) use number inputs.
 */
export function RangeFilter({ field, criteria, onChange }: Props) {
  const criterion = getCriterion(criteria, field.key);
  const range = asRange(criterion) ?? ['', ''];
  const [low, high] = range;

  const set = (lo: string, hi: string) => {
    let next: Criterion | null = null;
    if (lo && hi && lo === hi) next = { field: field.key, op: 'eq', values: [lo] };
    else if (lo || hi) next = { field: field.key, op: 'between', values: [lo, hi] };
    onChange(setCriterion(criteria, field.key, next));
  };

  if (field.values && field.values.length > 0) {
    const label = (v: number) => {
      const f = formatAttribute(field, v);
      return f.unit ? `${f.text} ${f.unit}` : f.text;
    };
    return (
      <div className={styles.rangePair}>
        <Select value={low} highlighted={low !== ''} aria-label={`${field.label}: mínimo`} onChange={(e) => set(e.target.value, high)}>
          <option value="">Qualquer</option>
          {field.values.filter((v) => !high || v <= Number(high)).map((v) => <option key={v} value={String(v)}>{`≥ ${label(v)}`}</option>)}
        </Select>
        <Select value={high} highlighted={high !== ''} aria-label={`${field.label}: máximo`} onChange={(e) => set(low, e.target.value)}>
          <option value="">Qualquer</option>
          {field.values.filter((v) => !low || v >= Number(low)).map((v) => <option key={v} value={String(v)}>{`≤ ${label(v)}`}</option>)}
        </Select>
      </div>
    );
  }

  return (
    <div className={styles.rangePair}>
      <input type="number" inputMode="decimal" value={low} placeholder={field.min != null ? `mín. ${field.min}` : 'mín.'}
        aria-label={`${field.label}: mínimo`} onChange={(e) => set(e.target.value, high)} />
      <input type="number" inputMode="decimal" value={high} placeholder={field.max != null ? `máx. ${field.max}` : 'máx.'}
        aria-label={`${field.label}: máximo`} onChange={(e) => set(low, e.target.value)} />
    </div>
  );
}

