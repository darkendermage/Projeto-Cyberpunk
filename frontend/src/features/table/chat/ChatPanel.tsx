import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Dices, EyeOff, Lock, MessageSquareLock, Send } from 'lucide-react';
import type { RollKind, RollResult, TableMessage, TableVisibility } from '../../../api/tableTypes';
import { Badge, Button, Select } from '../../../components/ui/ui';
import { t } from '../../../i18n';
import styles from '../Table.module.css';

export interface ChatTarget { id: number; label: string }

const VISIBILITY_LABEL: Record<TableVisibility, string> = {
  Public: t('Pública', 'Public'),
  Private: t('Privada (eu e o mestre)', 'Private (me and the GM)'),
  Secret: t('Secreta (só o mestre vê)', 'Secret (only the GM sees)'),
  Whisper: t('Sussurro', 'Whisper'),
};

const KIND_LABEL: Record<RollKind, string> = {
  Plain: t('Rolagem', 'Roll'),
  Check: t('Teste (d10 com crítico)', 'Check (d10 with criticals)'),
  Damage: t('Dano', 'Damage'),
};

const QUICK = ['1d10', '1d6', '2d6', '3d6', '4d6', '5d6', '1d100'];

/**
 * The table chat with its dice. The GM may whisper to a seat and apply a damage roll to a combatant; players choose
 * between public, private (with the GM) and secret rolls.
 */
export function ChatPanel({ messages, gm, targets, onSend, onRoll, renderActions }: {
  messages: TableMessage[];
  gm: boolean;
  /** Seats the GM can whisper to. */
  targets?: ChatTarget[];
  onSend: (text: string, visibility: TableVisibility, target: number | null) => Promise<unknown>;
  onRoll: (formula: string, kind: RollKind, visibility: TableVisibility, target: number | null) => Promise<unknown>;
  /** Extra controls under a roll card (the GM's "apply damage"). */
  renderActions?: (message: TableMessage) => ReactNode;
}) {
  const [text, setText] = useState('');
  const [formula, setFormula] = useState('1d10');
  const [kind, setKind] = useState<RollKind>('Check');
  const [visibility, setVisibility] = useState<TableVisibility>('Public');
  const [target, setTarget] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const list = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const el = list.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [messages.length]);

  const visibilities: TableVisibility[] = gm ? ['Public', 'Private', 'Whisper'] : ['Public', 'Private', 'Secret'];
  const whisperTarget = visibility === 'Whisper' ? target : null;
  const blocked = visibility === 'Whisper' && target == null;

  const run = async (action: () => Promise<unknown>) => {
    setError(null);
    try { await action(); } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
  };

  return (
    <div className={styles.chat}>
      <div ref={list} className={styles.messages} aria-live="polite">
        {messages.length === 0 && <span className={styles.muted}>{t('Nada por aqui ainda. Role um dado!', 'Nothing here yet. Roll some dice!')}</span>}
        {messages.map((m) => <MessageCard key={m.id} message={m} targets={targets} actions={renderActions?.(m)} />)}
      </div>

      <div className={styles.composer}>
        <div className={styles.row}>
          <Select aria-label={t('Quem vê', 'Who sees it')} value={visibility} onChange={(e) => setVisibility(e.target.value as TableVisibility)}>
            {visibilities.map((v) => <option key={v} value={v}>{VISIBILITY_LABEL[v]}</option>)}
          </Select>
          {visibility === 'Whisper' && (
            <Select aria-label={t('Sussurrar para', 'Whisper to')} value={target ?? ''} onChange={(e) => setTarget(e.target.value ? Number(e.target.value) : null)}>
              <option value="">{t('Para quem?', 'To whom?')}</option>
              {(targets ?? []).map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}
            </Select>
          )}
        </div>

        <div className={styles.quickDice}>
          {QUICK.map((q) => (
            <Button key={q} variant="ghost" disabled={blocked}
              onClick={() => run(() => onRoll(q, q === '1d10' ? 'Check' : q.endsWith('d6') ? 'Damage' : 'Plain', visibility, whisperTarget))}>{q}</Button>
          ))}
        </div>
        <form className={styles.row} onSubmit={(e) => { e.preventDefault(); if (formula.trim()) run(() => onRoll(formula.trim(), kind, visibility, whisperTarget)); }}>
          <input style={{ flex: '1 1 100%' }} aria-label={t('Fórmula', 'Formula')} value={formula} maxLength={80} onChange={(e) => setFormula(e.target.value)} placeholder="1d10+12" />
          <Select className={styles.grow} aria-label={t('Tipo de rolagem', 'Roll type')} value={kind} onChange={(e) => setKind(e.target.value as RollKind)}>
            {(['Check', 'Damage', 'Plain'] as RollKind[]).map((k) => <option key={k} value={k}>{KIND_LABEL[k]}</option>)}
          </Select>
          <Button type="submit" variant="primary" icon={<Dices size={15} />} disabled={blocked}>{t('Rolar', 'Roll')}</Button>
        </form>

        <form className={styles.stack} onSubmit={(e) => {
          e.preventDefault();
          if (text.trim()) run(async () => { await onSend(text.trim(), visibility === 'Secret' ? 'Private' : visibility, whisperTarget); setText(''); });
        }}>
          <textarea aria-label={t('Mensagem', 'Message')} value={text} maxLength={1000} placeholder={t('Escreva para a mesa…', 'Write to the table…')}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); e.currentTarget.form?.requestSubmit(); } }} />
          <div className={styles.rowBetween}>
            {error ? <span className={styles.error}>{error}</span> : <span className={styles.source}>{t('Enter envia · Shift+Enter quebra a linha', 'Enter sends · Shift+Enter breaks the line')}</span>}
            <Button type="submit" icon={<Send size={15} />} disabled={blocked || !text.trim()}>{t('Enviar', 'Send')}</Button>
          </div>
        </form>
      </div>
    </div>
  );
}

function MessageCard({ message: m, targets, actions }: { message: TableMessage; targets?: ChatTarget[]; actions?: ReactNode }) {
  const time = new Date(m.at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  const target = m.targetSeatId != null ? targets?.find((s) => s.id === m.targetSeatId)?.label : null;
  return (
    <div className={`${styles.message} ${m.visibility !== 'Public' ? styles.messageHidden : ''}`}>
      <div className={styles.rowBetween}>
        <span className={styles.author} style={{ color: m.authorColor ?? 'var(--amber)' }}>{m.authorName}</span>
        <span className={styles.row}>
          {m.visibility === 'Private' && <Lock size={12} aria-label={VISIBILITY_LABEL.Private} />}
          {m.visibility === 'Secret' && <EyeOff size={12} aria-label={VISIBILITY_LABEL.Secret} />}
          {m.visibility === 'Whisper' && <span className={styles.source}><MessageSquareLock size={12} /> {target ?? t('sussurro', 'whisper')}</span>}
          <span className={styles.source}>{time}</span>
        </span>
      </div>
      {m.kind === 'Roll'
        ? m.concealed
          ? <span className={styles.muted}>{t(`Rolou em segredo: ${m.text}`, `Rolled in secret: ${m.text}`)}</span>
          : <RollView label={m.text} roll={m.roll} />
        : <span className={styles.messageText}>{m.text}</span>}
      {actions}
    </div>
  );
}

function RollView({ label, roll }: { label: string; roll: RollResult | null }) {
  if (!roll) return <span className={styles.muted}>{label}</span>;
  return (
    <div className={styles.roll}>
      <div className={styles.rowBetween}>
        <span className={styles.messageText}>{label !== roll.formula ? `${label} · ` : ''}<span className={`${styles.source} ${styles.mono}`}>{roll.formula}</span></span>
        <span className={styles.rollTotal}>{roll.total}</span>
      </div>
      <div className={styles.dice}>
        {roll.terms.filter((term) => term.dice.length > 0).flatMap((term, i) => [
          ...term.dice.map((d, j) => {
            const sides = Number(term.expression.split('d')[1]);
            return <span key={`${i}-${j}`} className={`${styles.die} ${d === sides ? styles.dieHigh : d === 1 ? styles.dieLow : ''}`}>{term.sign < 0 ? `−${d}` : d}</span>;
          }),
          term.extra != null ? <span key={`${i}-x`} className={`${styles.die} ${term.extra > 0 ? styles.dieHigh : styles.dieLow}`}>{term.extra > 0 ? `+${term.extra}` : `−${-term.extra}`}</span> : null,
        ])}
      </div>
      <div className={styles.row}>
        {roll.critical === 'Success' && <Badge tone="cyan">{t('Sucesso crítico! (+1d10)', 'Critical success! (+1d10)')}</Badge>}
        {roll.critical === 'Failure' && <Badge tone="red">{t('Falha crítica! (−1d10)', 'Critical failure! (−1d10)')}</Badge>}
        {roll.criticalInjury && <Badge tone="amber">{t('Ferimento Crítico! (dois 6: +5 de dano direto, CP:R p.187)', 'Critical Injury! (two 6s: +5 damage straight to HP, CP:R p.187)')}</Badge>}
      </div>
    </div>
  );
}
