import { t } from '../../i18n';
import styles from './Table.module.css';

/**
 * The color of one's own dice (E3): everyone sees the dice of whoever rolled in that person's color. The choice is
 * saved when the picker closes (not on every drag of the color wheel).
 */
export function DiceColorPicker({ value, onChange }: { value: string | null; onChange: (color: string | null) => void }) {
  return (
    <label className={styles.dicePicker} title={t('Cor dos seus dados (todos veem nessa cor)', 'Color of your dice (everyone sees them in it)')}>
      <input key={value ?? 'none'} type="color" aria-label={t('Cor dos seus dados', 'Color of your dice')} defaultValue={value ?? '#3de0ff'}
        onBlur={(e) => { if (e.target.value !== value) onChange(e.target.value); }} />
    </label>
  );
}
