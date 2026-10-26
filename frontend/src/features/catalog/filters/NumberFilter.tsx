import { useEffect, useState } from 'react';
import type { FieldDescriptor, FilterOp } from '../../../api/types';
import { Select, Segmented } from '../../../components/ui/ui';
import { type Criterion, getCriterion, OP_LABELS, setCriterion } from '../../../lib/filters';
import { useDebouncedValue } from '../../../lib/hooks';
import styles from './Filters.module.css';
import { t, tv } from '../../../i18n';
interface Props {
  field: FieldDescriptor;
  criteria: Criterion[];
  onChange: (criteria: Criterion[]) => void;
}

const NUMBER_OPS: FilterOp[] = ['eq', 'neq', 'gt', 'gte', 'lt', 'lte', 'between', 'isnull', 'notnull'];

/** "ROF [≥] [2]" — operator + value(s). Typing is debounced so each keystroke does not hit the API. */
export function NumberFilter({ field, criteria, onChange }: Props) {
  const criterion = getCriterion(criteria, field.key);
  const [op, setOp] = useState<FilterOp | ''>(criterion?.op ?? '');
  const [a, setA] = useState(criterion?.values[0] ?? '');
  const [b, setB] = useState(criterion?.values[1] ?? '');

  // Keep local inputs in sync when the criterion changes elsewhere (chip removed, "clear all", back button).
  const signature = criterion ? `${criterion.op}:${criterion.values.join('|')}` : '';
  useEffect(() => {
    setOp(criterion?.op ?? '');
    setA(criterion?.values[0] ?? '');
    setB(criterion?.values[1] ?? '');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [signature]);

  const debounced = useDebouncedValue(`${op}|${a}|${b}`, 450);
  useEffect(() => {
    const [dOp, dA, dB] = debounced.split('|');
    const next = buildCriterion(field.key, dOp as FilterOp | '', dA, dB);
    const nextSig = next ? `${next.op}:${next.values.join('|')}` : '';
    if (nextSig !== signature) onChange(setCriterion(criteria, field.key, next));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debounced]);

  const ops = NUMBER_OPS.filter((o) => field.operators.includes(o));
  const placeholderMin = field.min != null ? String(field.min) : t('mín.', 'min.');
  const placeholderMax = field.max != null ? String(field.max) : t('máx.', 'max.');
  const needsValue = op !== '' && op !== 'isnull' && op !== 'notnull';
  const unit = field.unit && field.unit !== 'penalty' ? field.unit : undefined;

  return (
    <div className={styles.numberRow}>
      <span className={styles.numberLabel} title={tv(field.description) ?? tv(field.label)}>
        {tv(field.label)}{unit && <small> ({unit})</small>}
      </span>
      <Select value={op} highlighted={op !== ''} aria-label={t(`Operador para ${tv(field.label)}`, `Operator for ${tv(field.label)}`)}
        onChange={(e) => setOp(e.target.value as FilterOp | '')}>
        <option value="">{t('Qualquer', 'Any')}</option>
        {ops.map((o) => <option key={o} value={o}>{o === 'between' ? 'entre' : OP_LABELS[o]}</option>)}
      </Select>
      {needsValue && (
        <div className={styles.numberInputs}>
          <input type="number" inputMode="decimal" value={a} placeholder={op === 'between' ? placeholderMin : placeholderMin}
            min={field.min ?? undefined} max={field.max ?? undefined} aria-label={t(`${tv(field.label)}: valor${op === 'between' ? ' mínimo' : ''}`, `${tv(field.label)}: ${op === 'between' ? 'minimum ' : ''}value`)}
            onChange={(e) => setA(e.target.value)} />
          {op === 'between' && (
            <>
              <span className={styles.until}>{t('até', 'to')}</span>
              <input type="number" inputMode="decimal" value={b} placeholder={placeholderMax}
                min={field.min ?? undefined} max={field.max ?? undefined} aria-label={t(`${tv(field.label)}: valor máximo`, `${tv(field.label)}: maximum value`)}
                onChange={(e) => setB(e.target.value)} />
            </>
          )}
        </div>
      )}
    </div>
  );
}

function buildCriterion(field: string, op: FilterOp | '', a: string, b: string): Criterion | null {
  if (!op) return null;
  if (op === 'isnull' || op === 'notnull') return { field, op, values: [] };
  if (op === 'between') return a.trim() || b.trim() ? { field, op, values: [a.trim(), b.trim()] } : null;
  return a.trim() ? { field, op, values: [a.trim()] } : null;
}

/** Tri-state toggle for booleans: any / yes / no. */
export function BooleanFilter({ field, criteria, onChange }: Props) {
  const criterion = getCriterion(criteria, field.key);
  const value = criterion ? criterion.values[0] : 'any';
  return (
    <div className={styles.boolRow}>
      <span>{tv(field.label)}</span>
      <Segmented size="sm" ariaLabel={field.label} value={value as 'any' | 'true' | 'false'}
        onChange={(v) => onChange(setCriterion(criteria, field.key, v === 'any' ? null : { field: field.key, op: 'eq', values: [v] }))}
        options={[{ value: 'any', label: t('Todos', 'All') }, { value: 'true', label: t('Sim', 'Yes') }, { value: 'false', label: t('Não', 'No') }]} />
    </div>
  );
}
