import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { problemMessage, useCharacters } from '../../api/characters';
import { tableApi } from '../../api/table';
import type { Seat } from '../../api/tableTypes';
import { Button, Checkbox, Modal, NumberInput } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import { t } from '../../i18n';
import styles from './Table.module.css';

const MONEY_CHIPS = [500, 1000, 2000, 5000];
const IP_CHIPS = [5, 10, 20, 40];

// End of session: the same eb and IP to each ticked character. The invited players' characters come ticked;
// the others (played at this machine, say) can be added from the list below.
export function RewardDialog({ open, seats, onClose }: { open: boolean; seats: Seat[]; onClose: () => void }) {
  const characters = useCharacters(open);
  const client = useQueryClient();
  const toast = useToast();
  const seated = new Set(seats.filter((s) => !s.revoked && s.characterId != null).map((s) => s.characterId!));
  const ready = (characters.data ?? []).filter((c) => c.status !== 'Creating');
  const party = ready.filter((c) => seated.has(c.id));
  const others = ready.filter((c) => !seated.has(c.id));
  const [picked, setPicked] = useState<number[] | null>(null);
  const chosen = picked ?? party.map((c) => c.id);
  const [showOthers, setShowOthers] = useState(false);
  const [money, setMoney] = useState<number | null>(null);
  const [ip, setIp] = useState<number | null>(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const valid = chosen.length > 0 && ((money ?? 0) > 0 || (ip ?? 0) > 0) && reason.trim().length > 0;

  const toggle = (id: number, on: boolean) => setPicked(on ? [...chosen, id] : chosen.filter((x) => x !== id));
  const close = () => { setPicked(null); setMoney(null); setIp(null); setReason(''); setShowOthers(false); onClose(); };
  const submit = async () => {
    if (!valid) return;
    setBusy(true);
    try {
      const r = await tableApi.reward({ characterIds: chosen, money: money ?? 0, ip: ip ?? 0, reason: reason.trim() });
      void client.invalidateQueries({ queryKey: ['characters'] });
      toast.notify(t(`Recompensa entregue a ${r.names.join(', ')}.`, `Reward given to ${r.names.join(', ')}.`));
      close();
    } catch (e) {
      toast.notify(problemMessage(e), 'error');
    } finally {
      setBusy(false);
    }
  };
  const row = (c: (typeof ready)[number]) => (
    <Checkbox key={c.id} checked={chosen.includes(c.id)} onChange={(on) => toggle(c.id, on)} label={c.handle ?? c.name} />
  );

  return (
    <Modal open={open} onClose={close} title={t('Recompensa da sessão', 'Session reward')}>
      <form className={styles.stack} onSubmit={(e) => { e.preventDefault(); void submit(); }}>
        <span className={styles.muted}>{t('Cada personagem marcado recebe o valor inteiro. Fica no histórico de cada ficha e o chat avisa a mesa.', 'Each ticked character gets the whole amount. It goes in each sheet\'s history and the chat tells the table.')}</span>

        <div className={styles.section}>
          <span className={styles.label}>{t('Quem recebe', 'Who gets it')}</span>
          {characters.isLoading && <span className={styles.muted}>{t('Carregando…', 'Loading…')}</span>}
          {party.length === 0 && !characters.isLoading && <span className={styles.muted}>{t('Nenhum convite com personagem pronto: escolha abaixo.', 'No invite with a finished character: pick below.')}</span>}
          {party.map(row)}
          {others.length > 0 && (
            <Button variant="ghost" onClick={() => setShowOthers(!showOthers)}>
              {showOthers ? t('Esconder os outros personagens', 'Hide the other characters') : t(`Outros personagens (${others.length})`, `Other characters (${others.length})`)}
            </Button>
          )}
          {showOthers && others.map(row)}
        </div>

        <div className={styles.section}>
          <span className={styles.label}>{t('Eurodólares por personagem', 'Eurodollars per character')}</span>
          <div className={styles.row}>
            <NumberInput aria-label={t('Eurodólares', 'Eurodollars')} min={0} max={10_000_000} allowEmpty value={money} onChange={setMoney} data-autofocus />
            {MONEY_CHIPS.map((n) => <Button key={n} variant={money === n ? 'primary' : 'ghost'} onClick={() => setMoney(n)}>{formatEb(n)}</Button>)}
          </div>
        </div>
        <div className={styles.section}>
          <span className={styles.label}>{t('IP por personagem', 'IP per character')}</span>
          <div className={styles.row}>
            <NumberInput aria-label="IP" min={0} max={9999} allowEmpty value={ip} onChange={setIp} />
            {IP_CHIPS.map((n) => <Button key={n} variant={ip === n ? 'primary' : 'ghost'} onClick={() => setIp(n)}>{n} IP</Button>)}
          </div>
        </div>
        <input aria-label={t('Motivo', 'Reason')} value={reason} maxLength={200} placeholder={t('Sessão 12, trampo do Rogue…', 'Session 12, the job for Rogue…')}
          onChange={(e) => setReason(e.target.value)} />

        <div className={styles.row}>
          <Button type="submit" variant="primary" disabled={busy || !valid}>
            {t(`Entregar a ${chosen.length}`, `Give to ${chosen.length}`)}
          </Button>
          <Button variant="ghost" onClick={close}>{t('Cancelar', 'Cancel')}</Button>
        </div>
      </form>
    </Modal>
  );
}
