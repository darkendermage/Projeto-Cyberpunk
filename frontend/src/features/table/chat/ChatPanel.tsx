import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Dices, EyeOff, Lock, MessageSquareLock, RotateCcw, Send } from 'lucide-react';
import type { RollDebuff, RollKind, RollResult, TableAttack, TableMessage, TableVisibility } from '../../../api/tableTypes';
import { Badge, Button, Checkbox, Select } from '../../../components/ui/ui';
import { t, tv } from '../../../i18n';
import { MODE_LABEL } from '../../combat/combat';
import { NumField } from '../NumField';
import { attackDice } from './attackDice';
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

// GM can whisper and apply damage rolls; players pick public, private (with the GM) or secret.
export function ChatPanel({ messages, gm, targets, onSend, onRoll, renderActions, luckAvailable, mySeatId, onReroll, onRerollAttack }: {
  messages: TableMessage[];
  gm: boolean;
  /** Seats the GM can whisper to. */
  targets?: ChatTarget[];
  onSend: (text: string, visibility: TableVisibility, target: number | null) => Promise<unknown>;
  /** reroll: 1 more LUCK paid now for one free reroll of the Check. */
  onRoll: (formula: string, kind: RollKind, visibility: TableVisibility, target: number | null, luck: number, reroll: boolean) => Promise<unknown>;
  /** Extra controls under a roll card (the GM's "apply damage"). */
  renderActions?: (message: TableMessage) => ReactNode;
  /** A player with a character: LUCK left this session (spent on Checks, +1 a point; 1 more reserves a reroll). */
  luckAvailable?: number;
  mySeatId?: number;
  /** The free reroll of a Check that reserved LUCK (own lines: the seat's, or the GM's). */
  onReroll?: (messageId: number) => Promise<unknown>;
  /** The free reroll of an attack that reserved LUCK (the player's own attacks; the GM, any). */
  onRerollAttack?: (attack: TableAttack) => Promise<unknown>;
}) {
  const [text, setText] = useState('');
  const [formula, setFormula] = useState('1d10');
  const [kind, setKind] = useState<RollKind>('Check');
  const [visibility, setVisibility] = useState<TableVisibility>('Public');
  const [target, setTarget] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [luck, setLuck] = useState(0);
  const [reserve, setReserve] = useState(false);
  const list = useRef<HTMLDivElement>(null);
  const luckOn = luckAvailable != null;
  const spend = Math.min(luck, luckAvailable ?? 0);
  const canReserve = (luckAvailable ?? 0) >= spend + 1;
  // LUCK goes on Checks only, and is spent once.
  const roll = (f: string, k: RollKind) => run(async () => {
    const check = k === 'Check';
    await onRoll(f, k, visibility, whisperTarget, check ? spend : 0, check && reserve && canReserve);
    if (check) { setLuck(0); setReserve(false); }
  });
  const replaced = new Set(messages.map((m) => m.roll?.rerollOf).filter((x): x is number => x != null));
  const rerolledAttacks = new Set(messages.map((m) => (m.attack?.rerollOf != null ? `${m.attack.encounterId}:${m.attack.rerollOf}` : null)).filter((x): x is string => x != null));
  const mine = (m: TableMessage) => (gm ? m.authorSeatId == null : m.authorSeatId === mySeatId);

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
        {messages.map((m) => {
          // already paid with the LUCK reserved before the roll: the button itself costs nothing
          const canReroll = !!onReroll && mine(m) && m.roll?.kind === 'Check' && !!m.roll.rerollReady && !m.concealed && !replaced.has(m.id);
          const a = m.attack;
          const rerolled = !!a && rerolledAttacks.has(`${a.encounterId}:${a.seq}`);
          const canRerollAttack = !!onRerollAttack && !!a?.rerollReady && !a.undone && !rerolled && (gm || m.authorSeatId === mySeatId);
          return (
            <MessageCard key={m.id} message={m} targets={targets} replaced={replaced.has(m.id)} rerolled={rerolled}
              actions={<>
                {renderActions?.(m)}
                {canReroll && <Button variant="ghost" icon={<RotateCcw size={14} />} onClick={() => run(() => onReroll!(m.id))}>{t('Rerrolar (Sorte já paga)', 'Reroll (LUCK already paid)')}</Button>}
                {canRerollAttack && <Button variant="ghost" icon={<RotateCcw size={14} />} onClick={() => run(() => onRerollAttack!(a!))}>{t('Rerrolar ataque (Sorte já paga)', 'Reroll the attack (LUCK already paid)')}</Button>}
              </>} />
          );
        })}
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

        {luckOn && (
          <div className={styles.row}>
            <span className={styles.label}>{t('Sorte no próximo teste', 'LUCK on the next Check')}</span>
            <NumField label={t('Pontos de Sorte a gastar', 'LUCK points to spend')} min={0} max={luckAvailable ?? 0} value={spend} onChange={(n) => setLuck(Math.round(n))} />
            <span className={styles.muted}>{t(`+1 por ponto · restam ${luckAvailable}`, `+1 a point · ${luckAvailable} left`)}</span>
            <Checkbox checked={reserve && canReserve} disabled={!canReserve} onChange={setReserve}
              label={t('Reservar 1 Sorte para rerrolar', 'Reserve 1 LUCK to reroll')} />
          </div>
        )}
        <div className={styles.quickDice}>
          {QUICK.map((q) => (
            <Button key={q} variant="ghost" disabled={blocked}
              onClick={() => roll(q, q === '1d10' ? 'Check' : q.endsWith('d6') ? 'Damage' : 'Plain')}>{q}</Button>
          ))}
        </div>
        <form className={styles.row} onSubmit={(e) => { e.preventDefault(); if (formula.trim()) roll(formula.trim(), kind); }}>
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

function MessageCard({ message: m, targets, actions, replaced, rerolled }: { message: TableMessage; targets?: ChatTarget[]; actions?: ReactNode; replaced?: boolean; rerolled?: boolean }) {
  const time = new Date(m.at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  const target = m.targetSeatId != null ? targets?.find((s) => s.id === m.targetSeatId)?.label : null;
  return (
    <div className={`${styles.message} ${m.visibility !== 'Public' ? styles.messageHidden : ''} ${replaced ? styles.messageReplaced : ''}`}>
      <div className={styles.rowBetween}>
        <span className={styles.author} style={{ color: m.authorColor ?? 'var(--amber)' }}>{m.authorName}</span>
        <span className={styles.row}>
          {m.visibility === 'Private' && <Lock size={12} aria-label={VISIBILITY_LABEL.Private} />}
          {m.visibility === 'Secret' && <EyeOff size={12} aria-label={VISIBILITY_LABEL.Secret} />}
          {m.visibility === 'Whisper' && <span className={styles.source}><MessageSquareLock size={12} /> {target ?? t('sussurro', 'whisper')}</span>}
          <span className={styles.source}>{time}</span>
        </span>
      </div>
      {m.kind === 'Attack' && m.attack
        ? <AttackView attack={m.attack} rerolled={rerolled} />
        : m.kind === 'Roll'
        ? m.concealed
          ? <span className={styles.muted}>{t(`Rolou em segredo: ${m.text}`, `Rolled in secret: ${m.text}`)}</span>
          : <RollView label={m.text} roll={m.roll} />
        : <span className={styles.messageText}>{m.text}</span>}
      {actions}
    </div>
  );
}

/** An attack of the fight: the roll, then each target hit or missed (numbers only where the reader may see them). */
function AttackView({ attack: a, rerolled }: { attack: TableAttack; rerolled?: boolean }) {
  return (
    <div className={`${styles.attackCard} ${a.undone ? styles.attackUndone : ''}`}>
      <div className={styles.rowBetween}>
        <span className={styles.messageText}>{tv(a.weapon)} · <span className={styles.source}>{a.mode === 'Single' ? (a.melee ? t('golpe', 'strike') : t('tiro', 'shot')) : MODE_LABEL[a.mode]}</span></span>
        <span className={styles.rollTotal} title={a.attackRoll != null ? `d10 ${a.attackRoll}` : undefined}>{a.attackTotal}</span>
      </div>
      {/* Every die of the shot, so the roll is plain to the whole table. */}
      {attackDice(a).map((g) => (
        <div key={g.label} className={styles.attackDiceRow}>
          <span className={styles.source}>{g.label}</span>
          <span className={styles.dice}>
            {g.dice.map((d, i) => (
              <span key={i} className={`${styles.die} ${d.extra ? (d.value > 0 ? styles.dieHigh : styles.dieLow) : d.value === d.sides ? styles.dieHigh : d.value === 1 ? styles.dieLow : ''}`}>
                {d.extra && d.value > 0 ? `+${d.value}` : d.value < 0 ? `−${-d.value}` : d.value}
              </span>
            ))}
          </span>
        </div>
      ))}
      {(a.notes ?? []).map((n) => <span key={n} className={styles.attackNote}>{n}</span>)}
      <DebuffBadges debuffs={a.attackerDebuffs} inTotal />
      {a.targets.map((target) => (
        <div key={target.id} className={styles.attackTarget}>
          <Badge tone={target.hit ? 'red' : undefined}>{target.hit ? t('Acertou', 'Hit') : t('Errou', 'Miss')}</Badge>
          <span>{target.name}</span>
          {target.dv != null && <span className={styles.source}>DV {target.dv}{target.defenseRoll != null ? t(` (defesa ${target.defenseRoll})`, ` (defense ${target.defenseRoll})`) : ''}</span>}
          {target.hit && target.damage != null && (
            <span className={styles.mono}>{t(`${target.damage} de dano`, `${target.damage} damage`)}{target.through != null ? t(` · ${target.through} no HP`, ` · ${target.through} to HP`) : ''} · {tv(target.location)}</span>
          )}
          {target.criticalInjury && <Badge tone="amber">{tv(target.criticalInjury)}</Badge>}
          {(target.notes ?? []).map((n) => <span key={n} className={styles.attackNote}>{tv(n)}</span>)}
        </div>
      ))}
      {a.rerollReady && !a.undone && <Badge tone="cyan">{t('1 Sorte reservada para rerrolar', '1 LUCK reserved to reroll')}</Badge>}
      {a.rerollOf != null && <Badge>{t('Rerrolado (Sorte reservada)', 'Rerolled (reserved LUCK)')}</Badge>}
      {a.undone && <Badge>{rerolled ? t('Rerrolado: vale o ataque de baixo', 'Rerolled: the attack below counts') : t('Desfeito pelo mestre', 'Undone by the GM')}</Badge>}
    </div>
  );
}

// Debuffs of the roller, loud on the line: penalties say whether they are already in the total.
function DebuffBadges({ debuffs, inTotal }: { debuffs?: RollDebuff[] | null; inTotal?: boolean }) {
  if (!debuffs?.length) return null;
  return (
    <span className={styles.row} role="list" aria-label={t('Debuffs de quem rolou', 'Roller debuffs')}>
      {debuffs.map((d) => {
        const value = d.value != null ? ` ${d.value > 0 ? '+' : '−'}${Math.abs(d.value)}` : '';
        const where = d.value != null ? (inTotal ? t(' (no total)', ' (in the total)') : t(' (não somado)', ' (not added)')) : '';
        const title = [
          d.detail ? tv(d.detail) : null,
          d.value != null && !inTotal ? t('A fórmula digitada não leva a penalidade: some à mão.', 'A typed formula doesn\'t carry the penalty: add it by hand.') : null,
        ].filter(Boolean).join('\n');
        return (
          <span key={`${d.kind}-${d.label}`} role="listitem">
            <Badge tone={d.kind === 'injury' ? 'red' : 'amber'} title={title || undefined}>
              ⚠ {tv(d.label)}{value}{where}
            </Badge>
          </span>
        );
      })}
    </span>
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
        {!!roll.luck && <Badge tone="cyan">{t(`Sorte +${roll.luck}`, `LUCK +${roll.luck}`)}</Badge>}
        {roll.rerollReady && <Badge tone="cyan">{t('1 Sorte reservada para rerrolar', '1 LUCK reserved to reroll')}</Badge>}
        {roll.rerollOf != null && <Badge>{t('Rerrolada (Sorte reservada)', 'Rerolled (reserved LUCK)')}</Badge>}
        <DebuffBadges debuffs={roll.debuffs} inTotal={roll.penaltyInTotal} />
        {roll.criticalInjury && <Badge tone="amber">{t('Ferimento Crítico! (dois 6: +5 de dano direto, CP:R p.187)', 'Critical Injury! (two 6s: +5 damage straight to HP, CP:R p.187)')}</Badge>}
      </div>
    </div>
  );
}
