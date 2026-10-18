import { useEffect, useId, useRef, useState, type ButtonHTMLAttributes, type ReactNode, type SelectHTMLAttributes } from 'react';
import { createPortal } from 'react-dom';
import clsx from 'clsx';
import { AlertTriangle, Check, ChevronDown, RefreshCw, SearchX, X } from 'lucide-react';
import styles from './ui.module.css';

// ---------------------------------------------------------------- buttons

type ButtonVariant = 'primary' | 'ghost' | 'outline' | 'danger';

export function Button({
  variant = 'outline', active, icon, children, className, ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant; active?: boolean; icon?: ReactNode }) {
  return (
    <button type="button" {...props} className={clsx(styles.button, styles[variant], active && styles.active, className)}>
      {icon}
      {children && <span>{children}</span>}
    </button>
  );
}

export function IconButton({
  label, children, className, active, ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { label: string; active?: boolean }) {
  return (
    <button type="button" aria-label={label} title={label} {...props}
      className={clsx(styles.iconButton, active && styles.active, className)}>
      {children}
    </button>
  );
}

// ---------------------------------------------------------------- form controls

export function Checkbox({
  checked, onChange, label, count, disabled, muted,
}: { checked: boolean; onChange: (checked: boolean) => void; label: ReactNode; count?: number; disabled?: boolean; muted?: boolean }) {
  return (
    <label className={clsx(styles.checkbox, disabled && styles.disabled, muted && !checked && styles.muted)}>
      <input type="checkbox" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
      <span className={styles.box} aria-hidden>{checked && <Check size={12} strokeWidth={3} />}</span>
      <span className={styles.checkLabel}>{label}</span>
      {count !== undefined && <span className={styles.count}>({count})</span>}
    </label>
  );
}

export function Switch({ checked, onChange, label }: { checked: boolean; onChange: (checked: boolean) => void; label: ReactNode }) {
  return (
    <label className={styles.switch}>
      <input type="checkbox" role="switch" checked={checked} onChange={(e) => onChange(e.target.checked)} />
      <span className={styles.track} aria-hidden><span className={styles.thumb} /></span>
      <span>{label}</span>
    </label>
  );
}

export function Segmented<T extends string>({
  value, options, onChange, ariaLabel, size = 'md',
}: { value: T; options: { value: T; label: ReactNode }[]; onChange: (value: T) => void; ariaLabel: string; size?: 'sm' | 'md' }) {
  return (
    <div className={clsx(styles.segmented, size === 'sm' && styles.segmentedSm)} role="radiogroup" aria-label={ariaLabel}>
      {options.map((o) => (
        <button key={o.value} type="button" role="radio" aria-checked={value === o.value}
          className={clsx(value === o.value && styles.segmentActive)} onClick={() => onChange(o.value)}>
          {o.label}
        </button>
      ))}
    </div>
  );
}

export function Select({ className, children, highlighted, ...props }: SelectHTMLAttributes<HTMLSelectElement> & { highlighted?: boolean }) {
  return (
    <span className={clsx(styles.select, highlighted && styles.selectActive, className)}>
      <select {...props}>{children}</select>
      <ChevronDown size={14} aria-hidden />
    </span>
  );
}

export function Field({ label, error, hint, children, htmlFor }: { label: ReactNode; error?: string; hint?: ReactNode; children: ReactNode; htmlFor?: string }) {
  return (
    <div className={clsx(styles.field, error && styles.fieldError)}>
      <label htmlFor={htmlFor}>{label}</label>
      {children}
      {error ? <span className={styles.errorText}>{error}</span> : hint ? <span className={styles.hint}>{hint}</span> : null}
    </div>
  );
}

// ---------------------------------------------------------------- feedback

export function Spinner({ label = 'Carregando' }: { label?: string }) {
  return (
    <span className={styles.spinner} role="status">
      <span className={styles.spinnerRing} aria-hidden />
      <span className="visually-hidden">{label}</span>
    </span>
  );
}

export function Panel({ children, className, as: Tag = 'section' }: { children: ReactNode; className?: string; as?: 'section' | 'div' | 'aside' }) {
  return <Tag className={clsx(styles.panel, className)}>{children}</Tag>;
}

export function EmptyState({ title, children, action, icon }: { title: string; children?: ReactNode; action?: ReactNode; icon?: ReactNode }) {
  return (
    <div className={styles.empty}>
      <div className={styles.emptyIcon}>{icon ?? <SearchX size={30} />}</div>
      <h3>{title}</h3>
      {children && <div className={styles.emptyText}>{children}</div>}
      {action}
    </div>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const message = error instanceof Error ? error.message : 'Falha ao carregar os dados.';
  return (
    <div className={clsx(styles.empty, styles.errorBox)} role="alert">
      <div className={styles.emptyIcon}><AlertTriangle size={30} /></div>
      <h3>Conexão interrompida</h3>
      <div className={styles.emptyText}>{message}</div>
      {onRetry && <Button icon={<RefreshCw size={14} />} onClick={onRetry}>Tentar novamente</Button>}
    </div>
  );
}

export function Badge({ children, tone = 'default', title }: { children: ReactNode; tone?: 'default' | 'red' | 'amber' | 'cyan'; title?: string }) {
  return <span className={clsx(styles.badge, styles[`badge_${tone}`])} title={title}>{children}</span>;
}

export function Chip({ children, onRemove, title }: { children: ReactNode; onRemove?: () => void; title?: string }) {
  return (
    <span className={styles.chip} title={title}>
      <span>{children}</span>
      {onRemove && (
        <button type="button" onClick={onRemove} aria-label={`Remover filtro ${typeof children === 'string' ? children : ''}`}>
          <X size={12} />
        </button>
      )}
    </span>
  );
}

/** Section with a red HUD header that expands and collapses. Opens by itself when `active` becomes positive. */
export function Collapsible({
  title, children, active = 0, defaultOpen, extra,
}: { title: ReactNode; children: ReactNode; active?: number; defaultOpen?: boolean; extra?: ReactNode }) {
  const [open, setOpen] = useState(defaultOpen ?? active > 0);
  useEffect(() => { if (active > 0) setOpen(true); }, [active]);
  return (
    <section className={styles.collapsible}>
      <button type="button" className={styles.collapsibleHeader} aria-expanded={open} onClick={() => setOpen((o) => !o)}>
        <span>{title}</span>
        {active > 0 && <span className={styles.collapsibleCount}>{active}</span>}
        {extra}
        <ChevronDown size={15} className={clsx(styles.collapsibleChevron, open && styles.collapsibleChevronOpen)} />
      </button>
      {open && <div className={styles.collapsibleBody}>{children}</div>}
    </section>
  );
}

// ---------------------------------------------------------------- overlays

export function Modal({
  open, onClose, title, children, wide, labelledBy, variant = 'center',
}: {
  open: boolean;
  onClose: () => void;
  title?: ReactNode;
  children: ReactNode;
  wide?: boolean;
  labelledBy?: string;
  variant?: 'center' | 'drawer-right' | 'top';
}) {
  const fallbackId = useId();
  const ref = useRef<HTMLDivElement>(null);
  const titleId = labelledBy ?? fallbackId;

  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement as HTMLElement | null;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
      if (e.key === 'Tab' && ref.current) {
        const focusable = ref.current.querySelectorAll<HTMLElement>(
          'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])');
        if (focusable.length === 0) return;
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
      }
    };
    document.addEventListener('keydown', onKey);
    document.body.style.overflow = 'hidden';
    requestAnimationFrame(() => ref.current?.querySelector<HTMLElement>('[data-autofocus]')?.focus() ?? ref.current?.focus());
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = '';
      previous?.focus?.();
    };
  }, [open, onClose]);

  if (!open) return null;
  return createPortal(
    <div className={clsx(styles.overlay, styles[`overlay_${variant}`])} onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div ref={ref} className={clsx(styles.modal, styles[`modal_${variant}`], wide && styles.modalWide)}
        role="dialog" aria-modal="true" aria-labelledby={title ? titleId : undefined} tabIndex={-1}>
        {title && (
          <header className={styles.modalHeader}>
            <h2 id={titleId} className="hud-title">{title}</h2>
            <IconButton label="Fechar" onClick={onClose}><X size={18} /></IconButton>
          </header>
        )}
        {children}
      </div>
    </div>,
    document.body,
  );
}
