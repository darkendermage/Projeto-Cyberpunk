import { HardHat, Minus, Plus, Shield } from 'lucide-react';
import type { ArmorLocation } from '../../api/tableTypes';
import { IconButton } from '../../components/ui/ui';
import { t } from '../../i18n';
import styles from './Table.module.css';

export interface ArmorValues { spHead: number | null; maxSpHead: number | null; spBody: number | null; maxSpBody: number | null }

/**
 * SP of the head and the body as two icons with numbers; with <paramref name="onAdjust"/>, − ablates and + repairs one
 * point (never past the armor's full SP: the server holds the limit).
 */
export function ArmorControl({ armor, onAdjust, busy, compact }: {
  armor: ArmorValues;
  onAdjust?: (location: ArmorLocation, delta: number) => void;
  busy?: boolean;
  compact?: boolean;
}) {
  const slot = (location: ArmorLocation, sp: number | null, max: number | null) => {
    const label = location === 'Head' ? t('Cabeça', 'Head') : t('Corpo', 'Body');
    return (
      <span className={styles.armorSlot} title={`SP ${label}`}>
        {location === 'Head' ? <HardHat size={compact ? 13 : 15} aria-hidden /> : <Shield size={compact ? 13 : 15} aria-hidden />}
        {!compact && <span className={styles.label}>{label}</span>}
        <span className={styles.stat}>{sp ?? '—'}{max != null && <span className={styles.muted}>/{max}</span>}</span>
        {onAdjust && sp != null && (
          <>
            <IconButton label={t(`Ablar 1 SP (${label})`, `Ablate 1 SP (${label})`)} disabled={busy || sp <= 0} onClick={() => onAdjust(location, -1)}><Minus size={13} /></IconButton>
            <IconButton label={t(`Reparar 1 SP (${label})`, `Repair 1 SP (${label})`)} disabled={busy || max == null || sp >= max} onClick={() => onAdjust(location, 1)}><Plus size={13} /></IconButton>
          </>
        )}
      </span>
    );
  };
  return (
    <span className={styles.armor}>
      {slot('Head', armor.spHead, armor.maxSpHead)}
      {slot('Body', armor.spBody, armor.maxSpBody)}
    </span>
  );
}
