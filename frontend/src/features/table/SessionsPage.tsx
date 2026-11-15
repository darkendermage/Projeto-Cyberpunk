import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { BookOpen, Copy, ExternalLink, FolderOpen, Map as MapIcon, Pencil, Play, Plus, Trash2 } from 'lucide-react';
import { problemMessage } from '../../api/characters';
import { tableApi, tableKeys } from '../../api/table';
import type { TableMessage, TableSession } from '../../api/tableTypes';
import { Button, EmptyState, ErrorState, IconButton, Modal, Spinner } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import page from '../../styles/Page.module.css';
import { t, tv } from '../../i18n';
import styles from './Table.module.css';

const day = (iso: string) => new Date(iso).toLocaleDateString();

// A past session's scenes: each one can be copied into the open session (map, grid, tokens, without the fight).
function SessionScenes({ session, onClose }: { session: TableSession; onClose: () => void }) {
  const toast = useToast();
  const client = useQueryClient();
  const scenes = useQuery({ queryKey: ['table', 'session-scenes', session.id], queryFn: () => tableApi.sessionScenes(session.id) });
  const [copied, setCopied] = useState<number[]>([]);
  const copy = (id: number, name: string) => tableApi.copySceneToCurrent(id).then(() => {
    setCopied((c) => [...c, id]);
    void client.invalidateQueries({ queryKey: tableKeys.scenes });
    toast.notify(t(`"${name}" copiada para a sessão atual.`, `"${name}" copied into the current session.`));
  }, (e) => toast.notify(problemMessage(e), 'error'));
  return (
    <Modal open onClose={onClose} title={t(`Cenas de ${tv(session.name)}`, `Scenes of ${tv(session.name)}`)}>
      {scenes.isLoading && <Spinner />}
      {scenes.data?.length === 0 && <span className={styles.muted}>{t('Esta sessão não tem cenas.', 'This session has no scenes.')}</span>}
      <ul className={styles.stack}>
        {(scenes.data ?? []).map((s) => (
          <li key={s.id} className={styles.rowBetween}>
            <span><strong>{s.name}</strong> <span className={styles.muted}>· {t(`${s.tokens} tokens`, `${s.tokens} tokens`)}</span></span>
            <Button variant="ghost" icon={<Copy size={14} />} disabled={copied.includes(s.id)} onClick={() => void copy(s.id, s.name)}>
              {copied.includes(s.id) ? t('Copiada', 'Copied') : t('Copiar para a sessão atual', 'Copy into the current session')}
            </Button>
          </li>
        ))}
      </ul>
    </Modal>
  );
}

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

/** An old session's chat and fights, read-only. */
function SessionArchive({ session, onClose }: { session: TableSession; onClose: () => void }) {
  const archive = useQuery({ queryKey: ['table', 'session', session.id], queryFn: () => tableApi.sessionMessages(session.id) });
  return (
    <Modal open onClose={onClose} title={tv(session.name)} wide>
      <div className={styles.stack}>
        {session.fights.length > 0 && (
          <div className={styles.row}>
            <span className={styles.label}>{t('Combates', 'Fights')}</span>
            {session.fights.map((f) => <Link key={f.id} to={`/combat/${f.id}`} target="_blank" className={styles.link}>{tv(f.name)} <ExternalLink size={12} /></Link>)}
          </div>
        )}
        {archive.isLoading ? <Spinner /> : (
          (archive.data ?? []).length === 0
            ? <span className={styles.muted}>{t('Nenhuma mensagem nesta sessão.', 'No messages in this session.')}</span>
            : <ol className={styles.archive}>{(archive.data ?? []).map((m) => <ArchivedLine key={m.id} m={m} />)}</ol>
        )}
      </div>
    </Modal>
  );
}

// The table's entry page. "New session" names the new one, closes the open one and goes to the table; "Reopen"
// brings a past one back. Invites and seats are shared; a session keeps one night's scenes, chat and fights.
export function SessionsPage() {
  const client = useQueryClient();
  const toast = useToast();
  const navigate = useNavigate();
  const sessions = useQuery({ queryKey: tableKeys.sessions, queryFn: tableApi.sessions });
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [reading, setReading] = useState<TableSession | null>(null);
  const [scenesOf, setScenesOf] = useState<TableSession | null>(null);
  const fail = (e: unknown) => toast.notify(problemMessage(e), 'error');
  const refresh = () => {
    void client.invalidateQueries({ queryKey: tableKeys.sessions });
    void client.invalidateQueries({ queryKey: tableKeys.messages });
  };
  const open = sessions.data?.find((s) => s.open) ?? null;
  const past = (sessions.data ?? []).filter((s) => !s.open);

  const start = () => {
    setBusy(true);
    tableApi.newSession({ name: name.trim() || null })
      .then(() => { refresh(); navigate('/mesa/jogar'); }, fail)
      .finally(() => setBusy(false));
  };
  const rename = (s: TableSession) => {
    const next = window.prompt(t('Novo nome da sessão', 'New session name'), s.name);
    if (next?.trim()) tableApi.renameSession(s.id, { name: next.trim() }).then(refresh, fail);
  };
  // the past session becomes the open one: its scenes, chat and fights come back to the table to edit and keep playing
  const reopen = (s: TableSession) => {
    const msg = open
      ? t(`Reabrir "${s.name}" para editar? "${open.name}" é encerrada e fica guardada aqui; a mesa volta para as cenas e o chat de "${s.name}".`,
        `Reopen "${s.name}" to edit it? "${open.name}" ends and stays here; the table goes back to the scenes and chat of "${s.name}".`)
      : t(`Reabrir "${s.name}" para editar?`, `Reopen "${s.name}" to edit it?`);
    if (!window.confirm(msg)) return;
    setBusy(true);
    tableApi.reopenSession(s.id)
      .then(() => { refresh(); void client.invalidateQueries({ queryKey: tableKeys.scenes }); navigate('/mesa/jogar'); }, fail)
      .finally(() => setBusy(false));
  };
  const remove = (s: TableSession) => {
    if (window.confirm(t(`Apagar "${s.name}" e o chat dela? Os combates continuam na tela de combate.`, `Delete "${s.name}" and its chat? The fights stay on the combat page.`)))
      tableApi.deleteSession(s.id).then(refresh, fail);
  };
  const facts = (s: TableSession) => (
    <span className={styles.muted}>
      {day(s.startedAt)}{s.endedAt ? ` – ${day(s.endedAt)}` : ''}
      {' · '}{t(`${s.messages} mensagens`, `${s.messages} messages`)}{' · '}{t(`${s.fights.length} combates`, `${s.fights.length} fights`)}
    </span>
  );

  return (
    <div className={page.page}>
      <header className={styles.rowBetween}>
        <div>
          <h1 className="hud-title">{t('Mesa Virtual', 'Virtual Table')}</h1>
          <p className={page.lead}>{t('Escolha a sessão: continue a de hoje, comece uma nova ou reabra uma anterior para editar. Convites e lugares valem para todas; cada sessão tem suas cenas.', "Pick the session: continue today's, start a new one or reopen a past one to edit it. Invites and seats belong to all of them; each session has its own scenes.")}</p>
        </div>
        <Button variant="primary" icon={<Plus size={16} />} onClick={() => setCreating(true)}>{t('Nova sessão', 'New session')}</Button>
      </header>

      {sessions.isLoading && <Spinner />}
      {sessions.error && <ErrorState error={sessions.error} onRetry={() => sessions.refetch()} />}

      {open && (
        <section className={styles.sessionOpen} aria-label={t('Sessão aberta', 'Open session')}>
          <div className={styles.stack} style={{ gap: 2 }}>
            <span className={styles.label}>{t('Sessão aberta', 'Open session')}</span>
            <strong className={styles.sessionName}>{tv(open.name)}</strong>
            {facts(open)}
          </div>
          <div className={styles.row}>
            <IconButton label={t('Renomear', 'Rename')} onClick={() => rename(open)}><Pencil size={14} /></IconButton>
            <Button variant="primary" icon={<Play size={15} />} onClick={() => navigate('/mesa/jogar')}>{t('Continuar', 'Continue')}</Button>
          </div>
        </section>
      )}

      {sessions.data && past.length === 0 && !open && (
        <EmptyState title={t('Nenhuma sessão ainda', 'No sessions yet')} icon={<BookOpen size={28} />}
          action={<Button variant="primary" icon={<Plus size={16} />} onClick={() => setCreating(true)}>{t('Começar a primeira', 'Start the first one')}</Button>}>
          {t('Cada sessão guarda o chat e os combates de uma noite de jogo.', "Each session keeps one game night's chat and fights.")}
        </EmptyState>
      )}

      {past.length > 0 && (
        <>
          <h2 className={styles.title}>{t('Sessões anteriores', 'Previous sessions')}</h2>
          <ul className={styles.sessionCards}>
            {past.map((s) => (
              <li key={s.id} className={styles.sessionCard}>
                <strong className={styles.sessionName}>{tv(s.name)}</strong>
                {facts(s)}
                <div className={styles.row}>
                  <Button variant="ghost" icon={<BookOpen size={14} />} onClick={() => setReading(s)}>{t('Ler', 'Read')}</Button>
                  <Button variant="ghost" icon={<MapIcon size={14} />} onClick={() => setScenesOf(s)}>{t('Cenas', 'Scenes')}</Button>
                  <Button variant="ghost" icon={<FolderOpen size={14} />} disabled={busy} onClick={() => reopen(s)}
                    title={t('Volta a ser a sessão aberta: cenas, chat e combates dela voltam para a mesa', 'Becomes the open session again: its scenes, chat and fights come back to the table')}>
                    {t('Reabrir para editar', 'Reopen to edit')}
                  </Button>
                  <IconButton label={t('Renomear', 'Rename')} onClick={() => rename(s)}><Pencil size={14} /></IconButton>
                  <IconButton label={t('Apagar sessão, seu chat e suas cenas', 'Delete the session, its chat and its scenes')} onClick={() => remove(s)}><Trash2 size={14} /></IconButton>
                </div>
              </li>
            ))}
          </ul>
        </>
      )}

      <Modal open={creating} onClose={() => setCreating(false)} title={t('Nova sessão', 'New session')}>
        <div className={styles.stack}>
          <input autoFocus aria-label={t('Nome da nova sessão', 'Name of the new session')} placeholder={t('Ex.: Sessão 5 · O roubo no Afterlife', 'E.g. Session 5 · The Afterlife heist')}
            value={name} maxLength={80} onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter' && !busy) start(); }} />
          <span className={styles.muted}>
            {open
              ? t(`"${open.name}" será encerrada e fica guardada aqui, com o chat e os combates.`, `"${open.name}" ends and stays here, with its chat and fights.`) + " " + t("A nova começa sem cenas: copie as que quiser em Cenas, na sessão antiga.", "The new one starts with no scenes: copy the ones you want from Scenes, on the old session.")
              : t('Sem nome, ela vira "Sessão N".', 'Without a name it becomes "Session N".')}
          </span>
          <div className={styles.rowBetween}>
            <Button variant="ghost" onClick={() => setCreating(false)}>{t('Cancelar', 'Cancel')}</Button>
            <Button variant="primary" icon={<Play size={15} />} disabled={busy} onClick={start}>{t('Começar e abrir a mesa', 'Start and open the table')}</Button>
          </div>
        </div>
      </Modal>

      {reading && <SessionArchive session={reading} onClose={() => setReading(null)} />}
      {scenesOf && <SessionScenes session={scenesOf} onClose={() => setScenesOf(null)} />}
    </div>
  );
}
