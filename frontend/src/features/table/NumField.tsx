import { useState } from 'react';
import { parseDecimal } from '../../lib/numbers';
import styles from './Table.module.css';

// Keeps decimals and lets you type freely (empty or half a number is fine); valid values go out right away
// and blur puts back the current value.
export function NumField({ value, onChange, min, max, step = 1, label, className }: {
  value: number; onChange: (n: number) => void; min: number; max: number; step?: number; label: string; className?: string;
}) {
  const [text, setText] = useState<string | null>(null);
  return (
    <input type="text" inputMode="decimal" aria-label={label} className={className ?? styles.narrow} data-min={min} data-max={max} data-step={step}
      value={text ?? String(value)}
      onFocus={(e) => { setText(String(value)); e.target.select(); }}
      onChange={(e) => {
        setText(e.target.value);
        const n = parseDecimal(e.target.value, min, max);
        if (n !== null) onChange(n);
      }}
      onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); }}
      onBlur={() => setText(null)} />
  );
}
