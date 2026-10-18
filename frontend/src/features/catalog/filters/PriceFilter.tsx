import { useEffect, useState } from 'react';
import type { FieldDescriptor } from '../../../api/types';
import { type Criterion, getCriterion, setCriterion } from '../../../lib/filters';
import { formatEb } from '../../../lib/format';
import styles from './Filters.module.css';

interface Props {
  field: FieldDescriptor;
  criteria: Criterion[];
  onChange: (criteria: Criterion[]) => void;
}

/**
 * Price range with a log-scaled dual slider (prices go from 10 to 60.000+, a linear track would be useless)
 * plus exact numeric inputs. The criterion is committed on release / blur, not while dragging.
 */
export function PriceFilter({ field, criteria, onChange }: Props) {
  const min = field.min ?? 0;
  const max = field.max ?? 0;
  const criterion = getCriterion(criteria, field.key);
  const current = criterion?.op === 'between' ? criterion.values : ['', ''];

  const [low, setLow] = useState(current[0]);
  const [high, setHigh] = useState(current[1]);
  const signature = current.join('|');
  useEffect(() => { setLow(current[0]); setHigh(current[1]); }, [signature]); // eslint-disable-line react-hooks/exhaustive-deps

  if (max <= min) return null;

  const logMin = Math.log10(Math.max(min, 1));
  const logMax = Math.log10(Math.max(max, 2));
  const toPos = (v: number) => Math.round(((Math.log10(Math.max(v, 1)) - logMin) / (logMax - logMin)) * 1000);
  const fromPos = (p: number) => {
    const raw = 10 ** (logMin + (p / 1000) * (logMax - logMin));
    const step = raw < 100 ? 5 : raw < 1000 ? 10 : raw < 10000 ? 100 : 1000;
    return Math.min(max, Math.max(min, Math.round(raw / step) * step));
  };

  const lowValue = low === '' ? min : Number(low);
  const highValue = high === '' ? max : Number(high);

  const commit = (l: string, h: string) => {
    const nl = l !== '' && Number(l) > min ? l : '';
    const nh = h !== '' && Number(h) < max ? h : '';
    const next = nl || nh ? { field: field.key, op: 'between' as const, values: [nl, nh] } : null;
    const nextSig = next ? next.values.join('|') : '|';
    if (nextSig !== (criterion?.op === 'between' ? signature : '|')) onChange(setCriterion(criteria, field.key, next));
  };

  const lowPct = toPos(lowValue) / 10;
  const highPct = toPos(highValue) / 10;

  return (
    <div className={styles.price}>
      <div className={styles.slider} style={{ '--low': `${lowPct}%`, '--high': `${highPct}%` } as React.CSSProperties}>
        <div className={styles.sliderTrack} />
        <input type="range" min={0} max={1000} value={toPos(lowValue)} aria-label="Preço mínimo"
          onChange={(e) => setLow(String(Math.min(fromPos(Number(e.target.value)), highValue)))}
          onPointerUp={() => commit(low, high)} onKeyUp={() => commit(low, high)} />
        <input type="range" min={0} max={1000} value={toPos(highValue)} aria-label="Preço máximo"
          onChange={(e) => setHigh(String(Math.max(fromPos(Number(e.target.value)), lowValue)))}
          onPointerUp={() => commit(low, high)} onKeyUp={() => commit(low, high)} />
      </div>
      <div className={styles.priceInputs}>
        <input type="number" min={min} max={max} value={low} placeholder={formatEb(min)} aria-label="Preço mínimo (eddies)"
          onChange={(e) => setLow(e.target.value)} onBlur={() => commit(low, high)}
          onKeyDown={(e) => e.key === 'Enter' && commit(low, high)} />
        <span className={styles.until}>—</span>
        <input type="number" min={min} max={max} value={high} placeholder={formatEb(max)} aria-label="Preço máximo (eddies)"
          onChange={(e) => setHigh(e.target.value)} onBlur={() => commit(low, high)}
          onKeyDown={(e) => e.key === 'Enter' && commit(low, high)} />
      </div>
    </div>
  );
}
