import { useEffect, useState } from 'react';
import { t } from '../../i18n';
import styles from './Table.module.css';

/**
 * The color of one's own dice (E3): everyone sees the dice of whoever rolled in that person's color. The choice is
 * saved when the picker closes (not on every drag of the color wheel).
 */
export function DiceColorPicker({ value, onChange }: { value: string | null; onChange: (color: string | null) => void }) {
  const [draft, setDraft] = useState(value ?? '#3de0ff');
  useEffect(() => { if (value) setDraft(value); }, [value]);
  return (
    <label className={styles.dicePicker} title={t('Cor dos seus dados (todos veem nessa cor)', 'Color of your dice (everyone sees them in it)')}>
      <input type="color" aria-label={t('Cor dos seus dados', 'Color of your dice')} value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={() => { if (draft !== value) onChange(draft); }} />
    </label>
  );
}
