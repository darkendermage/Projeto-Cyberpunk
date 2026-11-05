import { useState } from 'react';
import { parseDecimal } from '../../lib/numbers';
import styles from './Table.module.css';

/**
 * A number field that keeps decimals and lets the person type freely: while typing, the text is theirs (it may be
 * empty or half a number); every valid value goes out at once, and leaving the field puts back the value in force.
 */
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
