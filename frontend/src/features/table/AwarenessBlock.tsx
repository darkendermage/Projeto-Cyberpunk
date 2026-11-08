import { useState } from 'react';
import { Crosshair } from 'lucide-react';
import { problemMessage, useCharacter, useCharacterCommand, useCharacterRules } from '../../api/characters';
import { Button } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import { t } from '../../i18n';
import { RoleSplitDialog } from '../characters/RoleSplitDialog';
import { awarenessEffects, splitTotal } from '../characters/roleSplit';
import styles from './Table.module.css';

const isSolo = (role: string) => role.toLowerCase() === 'solo';

// Solo only. Shows the points, what they do, which once-a-Round ones are used, and "Redistribute". In a running
// fight the server logs it as an Action.
export function AwarenessBlock({ characterId, roundFlags }: { characterId: number; roundFlags?: readonly string[] | null }) {
  const toast = useToast();
  const detail = useCharacter(characterId);
  const rules = useCharacterRules();
  const command = useCharacterCommand(characterId);
  const [open, setOpen] = useState(false);
  const role = detail.data?.sheet.roles.find((r) => isSolo(r.role));
  const rule = rules.data?.roles.find((r) => isSolo(r.id))?.abilityInfo.split;
  if (!detail.data || !role || !rule) return null;
  const split = role.split ?? {};
  const used = Object.values(split).reduce((a, b) => a + b, 0);
  const effects = awarenessEffects(split, roundFlags ?? []);

  return (
    <div className={styles.awareness}>
      <div className={styles.rowBetween}>
        <span className={styles.label}><Crosshair size={13} aria-hidden /> Combat Awareness · {used}/{splitTotal(rule, role.rank)}</span>
        <Button variant="ghost" disabled={command.isPending} onClick={() => setOpen(true)}
          title={t('Fora de combate é livre; em combate gasta uma Ação (CP:R p.146).', 'Free outside a fight; in a fight it spends an Action (CP:R p.146).')}>
          {t('Redistribuir', 'Redistribute')}
        </Button>
      </div>
      {effects.length === 0 && <span className={styles.muted}>{t('Nenhum ponto distribuído.', 'No points divided.')}</span>}
      <ul className={styles.awarenessList}>
        {effects.map((e) => (
          <li key={e.key} className={e.used ? styles.awarenessUsed : undefined}>
            <strong>{e.label} {e.value}</strong> <span className={styles.muted}>{e.detail}</span>
            {e.used != null && <span className={styles.stripTag}>{e.used ? t('usado nesta rodada', 'used this Round') : t('disponível', 'ready')}</span>}
          </li>
        ))}
      </ul>
      {open && (
        <RoleSplitDialog open onClose={() => setOpen(false)} role={role.role} rank={role.rank} rule={rule} split={split} busy={command.isPending}
          onSave={(next) => command.mutate({ path: 'role-split', body: { role: role.role, split: next } }, {
            onSuccess: () => { setOpen(false); toast.notify(t('Combat Awareness redistribuído.', 'Combat Awareness redistributed.')); },
            onError: (e) => toast.notify(problemMessage(e), 'error'),
          })} />
      )}
    </div>
  );
}
