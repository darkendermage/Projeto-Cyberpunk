// Humanity changed by hand (a GM ruling, a traumatic scene, a house rule). Recorded with its reason in the
// Humanity history; it may go below 0, where Cyberpsychosis takes over (CP:R p.230).
import { useState } from 'react';
import { SlidersHorizontal } from 'lucide-react';
import type { CharacterDetail } from '../../api/characterTypes';
import { Button, IconButton, Modal, NumberInput, Segmented } from '../../components/ui/ui';
import type { Run } from './CombatPanels';
import styles from './Characters.module.css';
import { t } from '../../i18n';

type Direction = 'lose' | 'gain';

export function HumanityButton({ detail, run, busy }: { detail: CharacterDetail; run: Run; busy: boolean }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <IconButton label={t('Ajustar Humanidade à mão', 'Adjust Humanity by hand')} disabled={busy} onClick={() => setOpen(true)}><SlidersHorizontal size={15} /></IconButton>
      {open && <HumanityDialog detail={detail} run={run} busy={busy} onClose={() => setOpen(false)} />}
    </>
  );
}

function HumanityDialog({ detail, run, busy, onClose }: { detail: CharacterDetail; run: Run; busy: boolean; onClose: () => void }) {
  const [direction, setDirection] = useState<Direction>('lose');
  const [amount, setAmount] = useState<number | null>(null);
  const [reason, setReason] = useState('');
  const current = detail.report.humanity.current;
  const signed = (amount ?? 0) * (direction === 'gain' ? 1 : -1);
  const after = current + signed;
  const ready = (amount ?? 0) > 0 && reason.trim().length > 0;

  const submit = async () => {
    if (!ready) return;
    const ok = await run('humanity', { amount: signed, reason: reason.trim() },
      direction === 'gain' ? t('Humanidade recuperada.', 'Humanity recovered.') : t('Humanidade perdida.', 'Humanity lost.'));
    if (ok) onClose();
  };

  return (
    <Modal open onClose={onClose} title={t('Ajustar Humanidade', 'Adjust Humanity')}>
      <form className={styles.panel} style={{ clipPath: 'none', border: 'none' }} onSubmit={(e) => { e.preventDefault(); void submit(); }}>
        <Segmented ariaLabel={t('Perder ou recuperar', 'Lose or recover')} value={direction} onChange={setDirection}
          options={[{ value: 'lose', label: t('Perder', 'Lose') }, { value: 'gain', label: t('Recuperar', 'Recover') }]} />
        <div className={styles.formGrid}>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>{t('Pontos', 'Points')}</span>
            <NumberInput className={styles.input} min={1} max={200} allowEmpty value={amount} data-autofocus onChange={setAmount} />
          </label>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>{t('Motivo', 'Reason')}</span>
            <input className={styles.input} value={reason} placeholder={direction === 'lose' ? t('Cena traumática, decisão do mestre…', 'Traumatic scene, GM ruling…') : t('Decisão do mestre…', 'GM ruling…')}
              onChange={(e) => setReason(e.target.value)} />
          </label>
        </div>
        <dl className={styles.kv}>
          <dt>{t('Humanidade atual', 'Current Humanity')}</dt><dd>{current}</dd>
          <dt>{t('Depois', 'After')}</dt><dd className={after <= 0 ? styles.red : signed < 0 ? styles.amber : styles.green}>{after}</dd>
        </dl>
        {after <= 0 && <p className={`${styles.small} ${styles.red}`}>{t('Abaixo de 1 o personagem entra em Cyberpsicose e vira NPC do mestre (CP:R p.230).', 'Below 1 the character falls into Cyberpsychosis and becomes the GM\'s NPC (CP:R p.230).')}</p>}
        <p className={styles.source}>{t('Fica no histórico de Humanidade com o motivo; não muda o teto da terapia.', 'Stays in the Humanity history with the reason; it doesn\'t change the therapy cap.')}</p>
        <div className={styles.row}>
          <Button type="submit" variant="primary" disabled={busy || !ready}>{direction === 'lose' ? t('Perder', 'Lose') : t('Recuperar', 'Recover')}{amount ? ` ${amount}` : ''}</Button>
          <Button variant="ghost" onClick={onClose}>{t('Cancelar', 'Cancel')}</Button>
        </div>
      </form>
    </Modal>
  );
}
