import { useState } from 'react';
import type { RoleSplitRule } from '../../api/characterTypes';
import { Button, Modal } from '../../components/ui/ui';
import { t } from '../../i18n';
import { NumField } from '../table/NumField';
import { splitTotal } from './roleSplit';
import styles from './Characters.module.css';


// Tech specialties (two per Rank, CP:R p.147) or Solo Combat Awareness (p.146). The server checks the limits again.
export function RoleSplitDialog({ open, onClose, role, rank, rule, split, busy, onSave }: {
  open: boolean;
  onClose: () => void;
  role: string;
  rank: number;
  rule: RoleSplitRule;
  split: Record<string, number>;
  busy: boolean;
  onSave: (split: Record<string, number>) => void;
}) {
  const [draft, setDraft] = useState<Record<string, number>>(split);
  const total = splitTotal(rule, rank);
  const used = Object.values(draft).reduce((a, b) => a + b, 0);
  const perKey = rule.perKeyMax === 'rank' ? rank : total;
  return (
    <Modal open={open} onClose={onClose} title={t(`Distribuir Rank · ${role}`, `Divide Rank · ${role}`)}>
      <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
        <p className={styles.small}>
          {rule.max === '2xrank'
            ? t(`Cada Rank vai para duas especialidades diferentes: ${total} pontos, no máximo ${rank} em cada.`, `Each Rank goes to two different specialties: ${total} points, at most ${rank} in each.`)
            : t(`${total} pontos para dividir.`, `${total} points to divide.`)}
        </p>
        <dl className={styles.kv}>
          {rule.keys.map((k) => {
            const allowed = rule.allowed[k.id];
            return (
              <div key={k.id} style={{ display: 'contents' }}>
                <dt>{t(k.labelPt ?? k.label, k.label)}{allowed ? <span className={styles.muted}> ({allowed.join(t(' ou ', ' or '))})</span> : null}</dt>
                <dd>
                  <NumField label={k.label} min={0} max={allowed ? Math.max(...allowed) : perKey} value={draft[k.id] ?? 0}
                    onChange={(n) => setDraft({ ...draft, [k.id]: Math.max(0, Math.round(n)) })} />
                </dd>
              </div>
            );
          })}
        </dl>
        <p className={styles.muted} style={used > total ? { color: 'var(--red)' } : undefined}>{t(`Usados ${used} de ${total}.`, `Used ${used} of ${total}.`)}</p>
        {role === 'Solo' && <p className={styles.source}>{t('Em combate, redistribuir custa uma Ação (CP:R p.146).', 'In combat, redistributing costs an Action (CP:R p.146).')}</p>}
        <p className={styles.source}>{rule.source}</p>
        <div className={styles.row}>
          <Button variant="primary" disabled={busy || used > total} onClick={() => onSave(draft)}>{t('Salvar', 'Save')}</Button>
          <Button variant="ghost" onClick={onClose}>{t('Cancelar', 'Cancel')}</Button>
        </div>
      </div>
    </Modal>
  );
}
