import { useState } from 'react';
import { Link } from 'react-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ExternalLink, Pencil, Trash2 } from 'lucide-react';
import { problemMessage } from '../../api/characters';
import { tableApi, tableKeys } from '../../api/table';
import type { TableMessage, TableSession } from '../../api/tableTypes';
import { Button, IconButton, Modal, Spinner } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import { t, tv } from '../../i18n';
import styles from './Table.module.css';

const day = (iso: string) => new Date(iso).toLocaleDateString();

/** One archived line, read-only: author, text, and the roll's or the attack's result. */
function ArchivedLine({ m }: { m: TableMessage }) {
  const result = m.roll
    ? ` → ${m.roll.total}`
    : m.attack
      ? ` → ${m.attack.attackTotal} (${m.attack.targets.map((x) => `${x.name}: ${x.hit ? x.damage ?? t('acertou', 'hit') : t('errou', 'missed')}`).join(', ')})`
      : '';
  return (
    <li className={styles.archivedLine}>
      <span className={styles.muted}>{new Date(m.at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>{' '}
      <strong style={{ color: m.authorColor ?? undefined }}>{m.authorName}</strong>: {tv(m.text)}{result}
    </li>
  );
}

/**
 * The table's sessions (E2): "New session" closes the open one with a name and starts an empty chat; old sessions can
 * be read (their chat and their fights), renamed or deleted.
 */
export function SessionsDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const client = useQueryClient();
  const toast = useToast();
  const sessions = useQuery({ queryKey: tableKeys.sessions, queryFn: tableApi.sessions, enabled: open });
  const [reading, setReading] = useState<TableSession | null>(null);
  const archive = useQuery({ queryKey: ['table', 'session', reading?.id ?? 0], queryFn: () => tableApi.sessionMessages(reading!.id), enabled: reading != null });
  const [name, setName] = useState(() => t(`Sessão de ${new Date().toLocaleDateString()}`, `Session of ${new Date().toLocaleDateString()}`));
  const refresh = () => {
    void client.invalidateQueries({ queryKey: tableKeys.sessions });
    void client.invalidateQueries({ queryKey: tableKeys.messages });
  };
  const fail = (e: unknown) => toast.notify(problemMessage(e), 'error');

  const startNew = () => {
    if (!window.confirm(t('Encerrar a sessão atual e começar uma nova? O chat atual fica guardado nas sessões anteriores.',
      'End the current session and start a new one? The current chat is kept with the previous sessions.'))) return;
    tableApi.newSession({ name }).then(() => { refresh(); toast.notify(t('Nova sessão começou.', 'A new session started.')); }, fail);
  };
  const rename = (s: TableSession) => {
    const next = window.prompt(t('Novo nome da sessão', 'New session name'), s.name);
    if (next) tableApi.renameSession(s.id, { name: next }).then(refresh, fail);
  };
  const remove = (s: TableSession) => {
    if (window.confirm(t(`Apagar "${s.name}" e o chat dela? Os combates continuam na tela de combate.`, `Delete "${s.name}" and its chat? The fights stay on the combat page.`)))
      tableApi.deleteSession(s.id).then(refresh, fail);
  };

  return (
    <Modal open={open} onClose={() => { setReading(null); onClose(); }} title={reading ? tv(reading.name) : t('Sessões', 'Sessions')} wide>
      {reading ? (
        <div className={styles.stack}>
          <Button variant="ghost" onClick={() => setReading(null)}>{t('← Voltar às sessões', '← Back to the sessions')}</Button>
          {reading.fights.length > 0 && (
            <div className={styles.row}>
              <span className={styles.label}>{t('Combates', 'Fights')}</span>
              {reading.fights.map((f) => <Link key={f.id} to={`/combat/${f.id}`} target="_blank" className={styles.link}>{tv(f.name)} <ExternalLink size={12} /></Link>)}
            </div>
          )}
          {archive.isLoading ? <Spinner /> : (
            <ol className={styles.archive}>{(archive.data ?? []).map((m) => <ArchivedLine key={m.id} m={m} />)}</ol>
          )}
        </div>
      ) : (
        <div className={styles.stack}>
          <div className={styles.row}>
            <input aria-label={t('Nome da sessão que termina', 'Name of the session that ends')} value={name} onChange={(e) => setName(e.target.value)} />
            <Button variant="primary" onClick={startNew}>{t('Nova sessão', 'New session')}</Button>
          </div>
          {sessions.isLoading ? <Spinner /> : (
            <ul className={styles.sessionList}>
              {(sessions.data ?? []).map((s) => (
                <li key={s.id} className={styles.rowBetween}>
                  <span>
                    <strong>{tv(s.name)}</strong>{s.open && <span className={styles.stripTag}>{t('aberta', 'open')}</span>}
                    <span className={styles.muted}>
                      {' · '}{day(s.startedAt)}{s.endedAt ? ` – ${day(s.endedAt)}` : ''}
                      {' · '}{t(`${s.messages} mensagens`, `${s.messages} messages`)}{' · '}{t(`${s.fights.length} combates`, `${s.fights.length} fights`)}
                    </span>
                  </span>
                  <span className={styles.row}>
                    {!s.open && <Button variant="ghost" onClick={() => setReading(s)}>{t('Ler', 'Read')}</Button>}
                    <IconButton label={t('Renomear', 'Rename')} onClick={() => rename(s)}><Pencil size={14} /></IconButton>
                    {!s.open && <IconButton label={t('Apagar sessão e seu chat', 'Delete the session and its chat')} onClick={() => remove(s)}><Trash2 size={14} /></IconButton>}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Modal>
  );
}
