import { useRef, useState, type ReactNode } from 'react';
import { AlertTriangle, CheckCircle2, CircleDot, Cpu, ImagePlus, Info, Loader2, Minus, Plus, Trash2, UserRound, XCircle } from 'lucide-react';
import { characterApi, problemMessage, useStoreCharacter } from '../../api/characters';
import type { CharacterDetail, CharacterIssue, CharacterRules, CreationBudget } from '../../api/characterTypes';
import { IconButton, Modal } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { PortraitError, preparePortrait } from '../../lib/portrait';
import { useToast } from '../../state/ToastContext';
import type { SaveStatus } from './useSheetEditor';
import styles from './Characters.module.css';
import { t, tv } from '../../i18n';

/** A Role Ability name that opens what the ability does (summary, main mechanics and pages). */
export function RoleAbilityButton({ rules, role, className }: { rules: CharacterRules; role: string; className?: string }) {
  const [open, setOpen] = useState(false);
  const info = rules.roles.find((r) => r.id === role)?.abilityInfo;
  if (!info) return null;
  return (
    <>
      <button type="button" className={`${styles.linkButton} ${styles.abilityLink} ${className ?? ''}`} onClick={() => setOpen(true)}
        title={t(`O que ${tv(info.name)} faz`, `What ${tv(info.name)} does`)}>
        {tv(info.name)}
      </button>
      <Modal open={open} onClose={() => setOpen(false)} title={`${tv(info.name)} · ${role}`}>
        <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
          <p>{tv(info.summary)}</p>
          <ul className={styles.abilityPoints}>
            {info.points.map((p) => <li key={p}>{p}</li>)}
          </ul>
          <p className={styles.source}>{t('Resumo de ', 'Summary of ')}{info.source}{t('. Em caso de dúvida, vale o livro.', '. When in doubt, the book wins.')}</p>
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
    <SheetPanel title={t('Efeitos do cyberware', 'Cyberware effects')} icon={<Cpu size={16} />}>
      <ul className={styles.list}>
        {effects.map((e) => (
          <li key={`${e.item}-${e.description}`} className={styles.listItem}>
            <span className={styles.small}><strong>{e.item}</strong>: {tv(e.description)} <span className={styles.source}>{e.source}</span></span>
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
  saved: t('Salvo', 'Saved'),
  pending: t('Alterações pendentes', 'Pending changes'),
  saving: t('Salvando…', 'Saving…'),
  error: t('Falha ao salvar', 'Save failed'),
};

/** Save state with icon and text (never color alone). Ctrl+S saves at once. */
export function SaveIndicator({ status }: { status: SaveStatus }) {
  const icon = status === 'saving' ? <Loader2 size={14} style={{ animation: 'spin 1s linear infinite' }} />
    : status === 'saved' ? <CheckCircle2 size={14} />
      : status === 'error' ? <XCircle size={14} />
        : <CircleDot size={14} />;
  return (
    <span className={styles.saveState} data-state={status} title={t('Ctrl+S salva na hora', 'Ctrl+S saves right away')} role="status" aria-live="polite">
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
        <span>{label}{budget.campaignVariant && <span className={styles.tag}>{t('variante', 'variant')}</span>}</span>
        <strong>{show(budget.used)} / {show(budget.total)}</strong>
      </div>
      <div className={styles.budgetBar}><div className={styles.budgetFill} style={{ width: `${pct}%` }} /></div>
      <span className={styles.source}>
        {budget.exceeded ? t(`Excedido em ${show(-budget.remaining)}`, `Over by ${show(-budget.remaining)}`) : t(`Restam ${show(budget.remaining)}`, `${show(budget.remaining)} left`)} · {budget.source}
      </span>
    </div>
  );
}

const ISSUE_ICON = {
  Error: <XCircle size={15} aria-label={t('Erro', 'Error')} />,
  Warning: <AlertTriangle size={15} aria-label={t('Aviso', 'Warning')} />,
  Info: <Info size={15} aria-label={t('Informação', 'Info')} />,
};
const ISSUE_TONE = { Error: 'bad', Warning: 'warn', Info: 'ok' } as const;

export function IssueList({ issues, empty = t('Nenhuma pendência.', 'Nothing pending.') }: { issues: CharacterIssue[]; empty?: string }) {
  if (issues.length === 0) return <p className={`${styles.muted} ${styles.small}`}>{empty}</p>;
  return (
    <ul className={styles.list}>
      {issues.map((i, n) => (
        <li key={`${i.code}-${n}`} className={styles.listItem} data-tone={ISSUE_TONE[i.severity]}>
          <span className={ISSUE_TONE[i.severity] === 'bad' ? styles.red : ISSUE_TONE[i.severity] === 'warn' ? styles.amber : styles.cyan}>
            {ISSUE_ICON[i.severity]}
          </span>
          <div className={styles.grow}>
            <div className={styles.small}>{tv(i.message)}</div>
            {i.suggestion && <div className={`${styles.small} ${styles.muted}`}>{tv(i.suggestion)}</div>}
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
      <IconButton label={t('Diminuir', 'Decrease')} onClick={() => onChange(Math.max(min, value - 1))} disabled={value <= min}><Minus size={14} /></IconButton>
      <strong>{value}</strong>
      <IconButton label={t('Aumentar', 'Increase')} onClick={() => onChange(Math.min(max, value + 1))} disabled={value >= max}><Plus size={14} /></IconButton>
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
      await characterApi.uploadPortrait(detail.id, await preparePortrait(file));
      await refresh();
    } catch (error) {
      toast.notify(error instanceof PortraitError ? error.message : problemMessage(error), 'error');
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
        ? <img src={characterApi.portraitUrl(detail.id, detail.updatedAt)} alt={t(`Retrato de ${tv(detail.sheet.identity.name)}`, `Portrait of ${tv(detail.sheet.identity.name)}`)} />
        : <div className={styles.portraitEmpty}><UserRound size={compact ? 32 : 72} /></div>}
      <div className={styles.portraitActions}>
        <input ref={input} type="file" accept="image/png,image/jpeg,image/webp,image/gif" hidden onChange={(e) => upload(e.target.files?.[0])} />
        <IconButton label={t('Trocar retrato (imagens grandes são reduzidas)', 'Change portrait (large images are shrunk)')} onClick={() => input.current?.click()} disabled={busy}><ImagePlus size={16} /></IconButton>
        {detail.hasPortrait && <IconButton label={t('Remover retrato', 'Remove portrait')} onClick={remove} disabled={busy}><Trash2 size={16} /></IconButton>}
      </div>
    </div>
  );
}
