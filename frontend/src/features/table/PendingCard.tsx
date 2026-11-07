import { Dices, ShieldOff, Shield, X } from 'lucide-react';
import type { PendingView } from '../../api/encounterTypes';
import { Button } from '../../components/ui/ui';
import { t, tv } from '../../i18n';
import { pendingPrompt } from './pending';
import styles from './Table.module.css';

/**
 * The decision a pending attack waits on: the target's dodge (CP:R p.172) or the Critical Injury roll (CP:R p.187).
 * Whoever must answer gets the buttons; everyone else sees that the fight waits.
 */
export function PendingCard({ pending, mine, gm, busy, onDodge, onCritical, onCancel }: {
  pending: PendingView;
  mine: boolean;
  gm: boolean;
  busy: boolean;
  onDodge: (dodge: boolean) => void;
  onCritical: () => void;
  onCancel?: () => void;
}) {
  const prompt = pendingPrompt(pending, mine, gm);
  if (!prompt) {
    return (
      <div className={styles.pendingCard} role="status">
        <span className={styles.muted}>
          {pending.step === 'Defense'
            ? t(`Aguardando ${tv(pending.targetName)} decidir se esquiva…`, `Waiting for ${tv(pending.targetName)} to choose whether to dodge…`)
            : t(`Aguardando a rolagem da Critical Injury em ${tv(pending.targetName)}…`, `Waiting for the Critical Injury roll on ${tv(pending.targetName)}…`)}
        </span>
      </div>
    );
  }
  return (
    <div className={`${styles.pendingCard} ${mine ? styles.pendingMine : ''}`} role="alertdialog" aria-label={prompt.title}>
      <strong>{tv(prompt.title)}</strong>
      {pending.step === 'Defense' && (
        <span className={styles.muted}>
          {t('REF 8+: esquivar troca o DV pela sua rolagem de Evasion (CP:R p.172).', 'REF 8+: dodging replaces the DV with your Evasion roll (CP:R p.172).')}
        </span>
      )}
      <div className={styles.row}>
        {prompt.actions.includes('dodge') && (
          <Button variant="primary" icon={<Shield size={14} />} disabled={busy} onClick={() => onDodge(true)}>
            {t(`Esquivar (Evasion +${pending.evasion ?? '?'} + d10)`, `Dodge (Evasion +${pending.evasion ?? '?'} + d10)`)}
          </Button>
        )}
        {prompt.actions.includes('noDodge') && (
          <Button icon={<ShieldOff size={14} />} disabled={busy} onClick={() => onDodge(false)}>
            {pending.rangeDv != null ? t(`Não esquivar (DV ${pending.rangeDv})`, `Don't dodge (DV ${pending.rangeDv})`) : t('Não esquivar', "Don't dodge")}
          </Button>
        )}
        {prompt.actions.includes('critical') && (
          <Button variant="danger" icon={<Dices size={14} />} disabled={busy} onClick={onCritical}>{t('Rolar Critical Injury (2d6)', 'Roll Critical Injury (2d6)')}</Button>
        )}
        {prompt.actions.includes('cancel') && onCancel && (
          <Button variant="ghost" icon={<X size={14} />} disabled={busy} onClick={onCancel}>{t('Cancelar ataque', 'Cancel attack')}</Button>
        )}
      </div>
    </div>
  );
}
