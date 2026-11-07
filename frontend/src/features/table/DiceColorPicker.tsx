import { useEffect, useRef } from 'react';
import { t } from '../../i18n';
import styles from './Table.module.css';

/**
 * The color of one's own dice (E3): everyone sees the dice of whoever rolled in that person's color. The choice is
 * saved when the picker closes (the native "change" event), not on every drag of the color wheel.
 */
export function DiceColorPicker({ value, onChange }: { value: string | null; onChange: (color: string | null) => void }) {
  const input = useRef<HTMLInputElement>(null);
  const latest = useRef({ value, onChange });
  useEffect(() => { latest.current = { value, onChange }; });
  // The saved color comes back from the server: the picker shows it without being rebuilt.
  useEffect(() => { if (input.current && value) input.current.value = value; }, [value]);
  useEffect(() => {
    const el = input.current;
    if (!el) return;
    const commit = () => { if (el.value !== latest.current.value) latest.current.onChange(el.value); };
    el.addEventListener('change', commit);
    return () => el.removeEventListener('change', commit);
  }, []);
  return (
    <label className={styles.dicePicker} title={t('Cor dos seus dados (todos veem nessa cor)', 'Color of your dice (everyone sees them in it)')}>
      <input ref={input} type="color" aria-label={t('Cor dos seus dados', 'Color of your dice')} defaultValue={value ?? '#3de0ff'} />
    </label>
  );
}
