import { useState } from 'react';
import { Dices } from 'lucide-react';
import { Button, Checkbox, Modal, NumberInput } from '../../components/ui/ui';
import { t, tv } from '../../i18n';
import { skillFormula } from './sheetRoll';
import styles from './Characters.module.css';

/** reroll: 1 more LUCK paid now for one free reroll of the Check from the chat. */
export interface PreRollChoice { bonus: number; luck: number; labels: string[]; reroll: boolean }

const signed = (n: number) => (n >= 0 ? `+${n}` : `${n}`);

// Before a skill Check: implant bonuses that only count in some situations (ticked by hand, nothing adds unticked)
// and the LUCK to spend, +1 a point (CP:R p.130). The total shows before rolling.
export function PreRollDialog({ title, total, situational, luckAvailable, onRoll, onClose }: {
  title: string;
  total: number;
  situational: { label: string; value: number }[];
  /** null: this roller can't spend LUCK here */
  luckAvailable: number | null;
  onRoll: (choice: PreRollChoice) => void;
  onClose: () => void;
}) {
  const [ticked, setTicked] = useState<string[]>([]);
  const [luck, setLuck] = useState<number | null>(0);
  const [reserve, setReserve] = useState(false);
  const spend = Math.min(Math.max(0, luck ?? 0), luckAvailable ?? 0);
  const canReserve = luckAvailable != null && luckAvailable >= spend + 1;
  const chosen = situational.filter((s) => ticked.includes(s.label));
  const bonus = chosen.reduce((sum, s) => sum + s.value, 0);
  return (
    <Modal open onClose={onClose} title={title}>
      <form className={styles.panel} style={{ clipPath: 'none', border: 'none' }}
        onSubmit={(e) => { e.preventDefault(); onRoll({ bonus, luck: spend, labels: chosen.map((s) => `${tv(s.label)} ${signed(s.value)}`), reroll: reserve && canReserve }); }}>
        {situational.length > 0 && (
          <div className={styles.column} style={{ gap: 6 }}>
            <span className={styles.idLabel}>{t('Bônus de cyberware que só valem em algumas situações', 'Cyberware bonuses that only count in some situations')}</span>
            {situational.map((s) => (
              <Checkbox key={s.label} checked={ticked.includes(s.label)} label={`${tv(s.label)} (${signed(s.value)})`}
                onChange={(on) => setTicked((list) => (on ? [...list, s.label] : list.filter((x) => x !== s.label)))} />
            ))}
          </div>
        )}
        {luckAvailable != null && (
          <label className={styles.tableField}>
            <span className={styles.idLabel}>{t(`Sorte a gastar (+1 cada · restam ${luckAvailable})`, `LUCK to spend (+1 each · ${luckAvailable} left)`)}</span>
            <NumberInput className={styles.input} min={0} max={luckAvailable} allowEmpty value={luck} onChange={setLuck} disabled={luckAvailable <= 0} data-autofocus />
          </label>
        )}
        {luckAvailable != null && (
          <Checkbox checked={reserve && canReserve} disabled={!canReserve} onChange={setReserve}
            label={t('Reservar 1 Sorte para rerrolar (o botão no chat sai de graça)', 'Reserve 1 LUCK to reroll (the chat button is then free)')} />
        )}
        <span className={styles.bigNumbers}><strong>{skillFormula(total + bonus + spend)}</strong></span>
        <div className={styles.row}>
          <Button type="submit" variant="primary" icon={<Dices size={14} />}>{t('Rolar', 'Roll')}</Button>
          <Button variant="ghost" onClick={onClose}>{t('Cancelar', 'Cancel')}</Button>
        </div>
      </form>
    </Modal>
  );
}
