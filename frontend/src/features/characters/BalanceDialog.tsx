// Adding or taking money by hand (pay from a job, an expense, a GM adjustment). Recorded as a manual ledger
// entry with its reason; only after creation, when the money stops coming from the method's budget.
import { useState } from 'react';
import { Wallet } from 'lucide-react';
import type { CharacterDetail } from '../../api/characterTypes';
import { Button, IconButton, Modal, NumberInput, Segmented } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import type { Run } from './CombatPanels';
import styles from './Characters.module.css';
import { t } from '../../i18n';

type Direction = 'add' | 'take';

export function BalanceButton({ detail, run, busy, compact }: { detail: CharacterDetail; run: Run; busy: boolean; compact?: boolean }) {
  const [open, setOpen] = useState(false);
  const finalized = detail.sheet.creation.finalized;
  const title = finalized
    ? t('Adicionar ou retirar dinheiro do saldo', 'Add or take money from the balance')
    : t('Durante a criação o dinheiro vem do orçamento do método', 'During creation the money comes from the method\'s budget');
  return (
    <>
      {compact
        ? <IconButton label={title} disabled={!finalized || busy} onClick={() => setOpen(true)}><Wallet size={15} /></IconButton>
        : <Button icon={<Wallet size={14} />} disabled={!finalized || busy} title={title} onClick={() => setOpen(true)}>{t('Adicionar dinheiro', 'Add money')}</Button>}
      {open && <BalanceDialog detail={detail} run={run} busy={busy} onClose={() => setOpen(false)} />}
    </>
  );
}

function BalanceDialog({ detail, run, busy, onClose }: { detail: CharacterDetail; run: Run; busy: boolean; onClose: () => void }) {
  const [direction, setDirection] = useState<Direction>('add');
  const [amount, setAmount] = useState<number | null>(null);
  const [reason, setReason] = useState('');
  const balance = detail.sheet.wallet.balance;
  const signed = (amount ?? 0) * (direction === 'add' ? 1 : -1);
  const ready = (amount ?? 0) > 0 && reason.trim().length > 0;

  const submit = async () => {
    if (!ready) return;
    const ok = await run('balance', { amount: signed, reason: reason.trim() },
      direction === 'add' ? t('Dinheiro adicionado.', 'Money added.') : t('Dinheiro retirado.', 'Money taken.'));
    if (ok) onClose();
  };

  return (
    <Modal open onClose={onClose} title={t('Ajustar saldo', 'Adjust balance')}>
      <form className={styles.panel} style={{ clipPath: 'none', border: 'none' }} onSubmit={(e) => { e.preventDefault(); void submit(); }}>
        <Segmented ariaLabel={t('Adicionar ou retirar', 'Add or take')} value={direction} onChange={setDirection}
          options={[{ value: 'add', label: t('Adicionar', 'Add') }, { value: 'take', label: t('Retirar', 'Take') }]} />
        <div className={styles.formGrid}>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>{t('Valor (eb)', 'Amount (eb)')}</span>
            <NumberInput className={styles.input} min={1} max={10_000_000} allowEmpty value={amount} data-autofocus onChange={setAmount} />
          </label>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>{t('Motivo', 'Reason')}</span>
            <input className={styles.input} value={reason} placeholder={direction === 'add' ? t('Pagamento do trampo, venda…', 'Job pay, a sale…') : t('Despesa, suborno…', 'Expense, bribe…')}
              onChange={(e) => setReason(e.target.value)} />
          </label>
        </div>
        <dl className={styles.kv}>
          <dt>{t('Saldo atual', 'Current balance')}</dt><dd>{formatEb(balance)}</dd>
          <dt>{t('Depois', 'After')}</dt><dd className={signed < 0 ? styles.red : styles.green}>{formatEb(balance + signed)}</dd>
        </dl>
        <p className={styles.source}>{t('Fica no histórico financeiro como lançamento manual, com o motivo.', 'Stays in the money history as a manual entry, with the reason.')}</p>
        <div className={styles.row}>
          <Button type="submit" variant="primary" disabled={busy || !ready}>
            {direction === 'add' ? t('Adicionar', 'Add') : t('Retirar', 'Take')}{amount ? ` ${formatEb(amount)} eb` : ''}
          </Button>
          <Button variant="ghost" onClick={onClose}>{t('Cancelar', 'Cancel')}</Button>
        </div>
      </form>
    </Modal>
  );
}
