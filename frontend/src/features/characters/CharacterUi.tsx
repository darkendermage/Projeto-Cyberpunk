import { useRef, useState, type ReactNode } from 'react';
import { AlertTriangle, CheckCircle2, CircleDot, Cpu, ImagePlus, Info, Loader2, Minus, Plus, Trash2, UserRound, XCircle } from 'lucide-react';
import { characterApi, problemMessage, useStoreCharacter } from '../../api/characters';
import type { CharacterDetail, CharacterIssue, CharacterRules, CreationBudget } from '../../api/characterTypes';
import { IconButton, Modal } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import type { SaveStatus } from './useSheetEditor';
import styles from './Characters.module.css';

/** A Role Ability name that opens what the ability does (summary, main mechanics and pages). */
export function RoleAbilityButton({ rules, role, className }: { rules: CharacterRules; role: string; className?: string }) {
  const [open, setOpen] = useState(false);
  const info = rules.roles.find((r) => r.id === role)?.abilityInfo;
  if (!info) return null;
  return (
    <>
      <button type="button" className={`${styles.linkButton} ${styles.abilityLink} ${className ?? ''}`} onClick={() => setOpen(true)}
        title={`O que ${info.name} faz`}>
        {info.name}
      </button>
      <Modal open={open} onClose={() => setOpen(false)} title={`${info.name} · ${role}`}>
        <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
          <p>{info.summary}</p>
          <ul className={styles.abilityPoints}>
            {info.points.map((p) => <li key={p}>{p}</li>)}
          </ul>
          <p className={styles.source}>Resumo de {info.source}. Em caso de dúvida, vale o livro.</p>
        </div>
      </Modal>
    </>
  );
}

/** What the installed cyberware changes on the sheet: STAT and skill bonuses (already applied) and game effects. */
export function CyberEffectsPanel({ detail }: { detail: CharacterDetail }) {
  const effects = detail.report.cyberwareEffects;
  if (effects.length === 0) return null;
  return (
    <SheetPanel title="Efeitos do cyberware" icon={<Cpu size={16} />}>
      <ul className={styles.list}>
        {effects.map((e) => (
          <li key={`${e.item}-${e.description}`} className={styles.listItem}>
            <span className={styles.small}><strong>{e.item}</strong>: {e.description} <span className={styles.source}>{e.source}</span></span>
          </li>
        ))}
      </ul>
    </SheetPanel>
  );
}

export function SheetPanel({ title, icon, actions, children, className }: {
  title: ReactNode; icon?: ReactNode; actions?: ReactNode; children: ReactNode; className?: string;
}) {
  return (
    <section className={`${styles.panel} ${className ?? ''}`}>
      <header className={styles.panelHeader}>
        <h3 className={styles.panelTitle}>{icon}{title}</h3>
        {actions && <div className={styles.panelActions}>{actions}</div>}
      </header>
      {children}
    </section>
  );
}

const SAVE_TEXT: Record<SaveStatus, string> = {
  saved: 'Salvo',
  pending: 'Alterações pendentes',
  saving: 'Salvando…',
  error: 'Falha ao salvar',
};

/** Save state with icon and text (never color alone). Ctrl+S saves at once. */
export function SaveIndicator({ status }: { status: SaveStatus }) {
  const icon = status === 'saving' ? <Loader2 size={14} style={{ animation: 'spin 1s linear infinite' }} />
    : status === 'saved' ? <CheckCircle2 size={14} />
      : status === 'error' ? <XCircle size={14} />
        : <CircleDot size={14} />;
  return (
    <span className={styles.saveState} data-state={status} title="Ctrl+S salva na hora" role="status" aria-live="polite">
      {icon}{SAVE_TEXT[status]}
    </span>
  );
}

export function BudgetBar({ label, budget, money }: { label: string; budget: CreationBudget; money?: boolean }) {
  const pct = budget.total > 0 ? Math.min(100, (budget.used / budget.total) * 100) : 0;
  const show = (v: number) => (money ? formatEb(v) : String(v));
  return (
    <div className={styles.budget} data-exceeded={budget.exceeded} title={budget.source}>
      <div className={styles.budgetHead}>
        <span>{label}{budget.campaignVariant && <span className={styles.tag}>variante</span>}</span>
        <strong>{show(budget.used)} / {show(budget.total)}</strong>
      </div>
      <div className={styles.budgetBar}><div className={styles.budgetFill} style={{ width: `${pct}%` }} /></div>
      <span className={styles.source}>
        {budget.exceeded ? `Excedido em ${show(-budget.remaining)}` : `Restam ${show(budget.remaining)}`} · {budget.source}
      </span>
    </div>
  );
}

const ISSUE_ICON = {
  Error: <XCircle size={15} aria-label="Erro" />,
  Warning: <AlertTriangle size={15} aria-label="Aviso" />,
  Info: <Info size={15} aria-label="Informação" />,
};
const ISSUE_TONE = { Error: 'bad', Warning: 'warn', Info: 'ok' } as const;

export function IssueList({ issues, empty = 'Nenhuma pendência.' }: { issues: CharacterIssue[]; empty?: string }) {
  if (issues.length === 0) return <p className={`${styles.muted} ${styles.small}`}>{empty}</p>;
  return (
    <ul className={styles.list}>
      {issues.map((i, n) => (
        <li key={`${i.code}-${n}`} className={styles.listItem} data-tone={ISSUE_TONE[i.severity]}>
          <span className={ISSUE_TONE[i.severity] === 'bad' ? styles.red : ISSUE_TONE[i.severity] === 'warn' ? styles.amber : styles.cyan}>
            {ISSUE_ICON[i.severity]}
          </span>
          <div className={styles.grow}>
            <div className={styles.small}>{i.message}</div>
            {i.suggestion && <div className={`${styles.small} ${styles.muted}`}>{i.suggestion}</div>}
            {i.source && <div className={styles.source}>{i.source}</div>}
          </div>
        </li>
      ))}
    </ul>
  );
}

export function NumberStepper({ label, value, min, max, onChange, title }: {
  label: ReactNode; value: number; min: number; max: number; onChange: (v: number) => void; title?: string;
}) {
  return (
    <div className={styles.stepper} title={title}>
      <span className={styles.statKey}>{label}</span>
      <IconButton label="Diminuir" onClick={() => onChange(Math.max(min, value - 1))} disabled={value <= min}><Minus size={14} /></IconButton>
      <strong>{value}</strong>
      <IconButton label="Aumentar" onClick={() => onChange(Math.min(max, value + 1))} disabled={value >= max}><Plus size={14} /></IconButton>
    </div>
  );
}

export function StateTag({ tone, children, title }: { tone: 'ok' | 'warn' | 'bad'; children: ReactNode; title?: string }) {
  const icon = tone === 'ok' ? <CheckCircle2 size={13} /> : tone === 'warn' ? <AlertTriangle size={13} /> : <XCircle size={13} />;
  return <span className={styles.state} data-tone={tone} title={title}>{icon}{children}</span>;
}

/** Portrait with upload and removal; the image is stored with the character. */
export function Portrait({ detail, compact }: { detail: CharacterDetail; compact?: boolean }) {
  const input = useRef<HTMLInputElement>(null);
  const toast = useToast();
  const store = useStoreCharacter();
  const [busy, setBusy] = useState(false);
  const refresh = async () => store(await characterApi.get(detail.id));

  const upload = async (file: File | undefined) => {
    if (!file) return;
    setBusy(true);
    try {
      await characterApi.uploadPortrait(detail.id, file);
      await refresh();
    } catch (error) {
      toast.notify(problemMessage(error), 'error');
    } finally {
      setBusy(false);
      if (input.current) input.current.value = '';
    }
  };
  const remove = async () => {
    setBusy(true);
    try {
      await characterApi.deletePortrait(detail.id);
      await refresh();
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className={styles.portrait} style={compact ? { aspectRatio: '1' } : undefined}>
      {detail.hasPortrait
        ? <img src={characterApi.portraitUrl(detail.id, detail.updatedAt)} alt={`Retrato de ${detail.sheet.identity.name}`} />
        : <div className={styles.portraitEmpty}><UserRound size={compact ? 32 : 72} /></div>}
      <div className={styles.portraitActions}>
        <input ref={input} type="file" accept="image/png,image/jpeg,image/webp,image/gif" hidden onChange={(e) => upload(e.target.files?.[0])} />
        <IconButton label="Trocar retrato (até 2 MB)" onClick={() => input.current?.click()} disabled={busy}><ImagePlus size={16} /></IconButton>
        {detail.hasPortrait && <IconButton label="Remover retrato" onClick={remove} disabled={busy}><Trash2 size={16} /></IconButton>}
      </div>
    </div>
  );
}
