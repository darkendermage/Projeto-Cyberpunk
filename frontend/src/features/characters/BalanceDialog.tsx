// Adding or taking money by hand (pay from a job, an expense, a GM adjustment). Recorded as a manual ledger
// entry with its reason; only after creation, when the money stops coming from the method's budget.
import { useState } from 'react';
import { Minus, Plus } from 'lucide-react';
import type { CharacterDetail } from '../../api/characterTypes';
import { Button, IconButton, Modal, NumberInput } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import type { Run } from './CombatPanels';
import styles from './Characters.module.css';
import { t } from '../../i18n';

export type Direction = 'add' | 'take';

const MONEY_CHIPS = [100, 500, 1000, 5000];

/** − and + next to the balance: each opens the window already set to take or add. */
export function BalanceButtons({ detail, run, busy }: { detail: CharacterDetail; run: Run; busy: boolean }) {
  const [open, setOpen] = useState<Direction | null>(null);
  const finalized = detail.sheet.creation.finalized;
  const why = finalized ? null : t('Durante a criação o dinheiro vem do orçamento do método', 'During creation the money comes from the method\'s budget');
  return (
    <span className={styles.row} style={{ gap: 4, flexWrap: 'nowrap' }}>
      <IconButton label={why ?? t('Gastar dinheiro', 'Spend money')} disabled={!finalized || busy || detail.sheet.wallet.balance <= 0} onClick={() => setOpen('take')}><Minus size={15} /></IconButton>
      <IconButton label={why ?? t('Receber dinheiro', 'Receive money')} disabled={!finalized || busy} onClick={() => setOpen('add')}><Plus size={15} /></IconButton>
      {open && <AmountDialog direction={open} detail={detail} run={run} busy={busy} onClose={() => setOpen(null)} />}
    </span>
  );
}

function AmountDialog({ direction, detail, run, busy, onClose }: { direction: Direction; detail: CharacterDetail; run: Run; busy: boolean; onClose: () => void }) {
  const [amount, setAmount] = useState<number | null>(null);
  const [reason, setReason] = useState('');
  const balance = detail.sheet.wallet.balance;
  const add = direction === 'add';
  const signed = (amount ?? 0) * (add ? 1 : -1);
  const ready = (amount ?? 0) > 0 && reason.trim().length > 0 && balance + signed >= 0;

  const submit = async () => {
    if (!ready) return;
    const ok = await run('balance', { amount: signed, reason: reason.trim() },
      add ? t('Dinheiro recebido.', 'Money received.') : t('Dinheiro gasto.', 'Money spent.'));
    if (ok) onClose();
  };

  return (
    <Modal open onClose={onClose} title={add ? t('Receber dinheiro', 'Receive money') : t('Gastar dinheiro', 'Spend money')}>
      <form className={styles.panel} style={{ clipPath: 'none', border: 'none' }} onSubmit={(e) => { e.preventDefault(); void submit(); }}>
        <div className={styles.formGrid}>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>{t('Valor (eb)', 'Amount (eb)')}</span>
            <NumberInput className={styles.input} min={1} max={add ? 10_000_000 : Math.max(1, balance)} allowEmpty value={amount} data-autofocus onChange={setAmount} />
          </label>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>{t('Motivo', 'Reason')}</span>
            <input className={styles.input} value={reason} placeholder={add ? t('Pagamento do trampo, venda…', 'Job pay, a sale…') : t('Despesa, suborno…', 'Expense, bribe…')}
              onChange={(e) => setReason(e.target.value)} />
          </label>
        </div>
        <div className={styles.row} role="group" aria-label={t('Valores rápidos', 'Quick amounts')}>
          {MONEY_CHIPS.filter((n) => add || n <= balance).map((n) => (
            <Button key={n} variant={amount === n ? 'primary' : 'ghost'} onClick={() => setAmount(n)}>{formatEb(n)}</Button>
          ))}
          {!add && balance > 0 && <Button variant={amount === balance ? 'primary' : 'ghost'} onClick={() => setAmount(balance)}>{t('Tudo', 'All')}</Button>}
        </div>
        <dl className={styles.kv}>
          <dt>{t('Saldo atual', 'Current balance')}</dt><dd>{formatEb(balance)}</dd>
          <dt>{t('Depois', 'After')}</dt><dd className={signed < 0 ? styles.red : styles.green}>{formatEb(balance + signed)}</dd>
        </dl>
        {balance + signed < 0 && <p className={`${styles.small} ${styles.red}`}>{t('O saldo não pode ficar negativo.', "The balance can't go negative.")}</p>}
        <p className={styles.source}>{t('Fica no histórico financeiro como lançamento manual, com o motivo.', 'Stays in the money history as a manual entry, with the reason.')}</p>
        <div className={styles.row}>
          <Button type="submit" variant="primary" disabled={busy || !ready}>
            {add ? t('Receber', 'Receive') : t('Gastar', 'Spend')}{amount ? ` ${formatEb(amount)} eb` : ''}
          </Button>
          <Button variant="ghost" onClick={onClose}>{t('Cancelar', 'Cancel')}</Button>
        </div>
      </form>
    </Modal>
  );
}
