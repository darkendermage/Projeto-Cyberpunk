import {
  cloneElement, useEffect, useId, useRef, useState,
  type ButtonHTMLAttributes, type InputHTMLAttributes, type ReactElement, type ReactNode, type SelectHTMLAttributes,
} from 'react';
import { createPortal } from 'react-dom';
import clsx from 'clsx';
import { AlertTriangle, Check, ChevronDown, RefreshCw, SearchX, X } from 'lucide-react';
import { parseWholeNumber } from '../../lib/numbers';
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

/**
 * A text field that looks like {@link Select}: it opens its own list of suggestions but still takes free text
 * (a language or a neighborhood the list doesn't have). The list is portaled so modals and panels don't clip it.
 */
export function ComboBox({ id, value, onChange, options, placeholder, className, 'aria-label': ariaLabel, disabled }: {
  id?: string;
  value: string;
  onChange: (value: string) => void;
  options: readonly string[];
  placeholder?: string;
  className?: string;
  'aria-label'?: string;
  disabled?: boolean;
}) {
  const fallbackId = useId();
  const inputId = id ?? fallbackId;
  const listId = `${inputId}-list`;
  const wrap = useRef<HTMLSpanElement>(null);
  const [open, setOpen] = useState(false);
  const [typed, setTyped] = useState(false);
  const [active, setActive] = useState(-1);
  const [rect, setRect] = useState<DOMRect | null>(null);

  const unique = [...new Set(options)];
  const needle = value.trim().toLowerCase();
  // Typing narrows the list; opening a field that already holds an answer shows every option.
  const shown = typed && needle ? unique.filter((o) => o.toLowerCase().includes(needle)) : unique;

  useEffect(() => {
    if (!open) return;
    const place = () => setRect(wrap.current?.getBoundingClientRect() ?? null);
    place();
    window.addEventListener('resize', place);
    window.addEventListener('scroll', place, true);
    return () => { window.removeEventListener('resize', place); window.removeEventListener('scroll', place, true); };
  }, [open]);

  const show = () => { setTyped(false); setActive(-1); setOpen(true); };
  const pick = (option: string) => { onChange(option); setOpen(false); };

  return (
    <span ref={wrap} className={clsx(styles.select, styles.combo, className)}>
      <input id={inputId} value={value} placeholder={placeholder} disabled={disabled} autoComplete="off" spellCheck={false}
        role="combobox" aria-expanded={open} aria-controls={listId} aria-autocomplete="list" aria-label={ariaLabel}
        aria-activedescendant={open && active >= 0 ? `${listId}-${active}` : undefined}
        onChange={(e) => { onChange(e.target.value); setTyped(true); setActive(-1); setOpen(true); }}
        onClick={() => (open ? setOpen(false) : show())}
        onBlur={() => setOpen(false)}
        onKeyDown={(e) => {
          if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            e.preventDefault();
            if (!open) { show(); return; }
            const step = e.key === 'ArrowDown' ? 1 : -1;
            setActive((i) => (shown.length ? (i + step + shown.length) % shown.length : -1));
          } else if (e.key === 'Enter' && open && active >= 0 && shown[active] != null) {
            e.preventDefault();
            pick(shown[active]);
          } else if (e.key === 'Escape' && open) {
            e.stopPropagation();
            setOpen(false);
          }
        }} />
      <button type="button" tabIndex={-1} className={styles.comboToggle} aria-label="Mostrar opções" disabled={disabled}
        onMouseDown={(e) => e.preventDefault()}
        onClick={() => { if (open) setOpen(false); else { show(); document.getElementById(inputId)?.focus(); } }}>
        <ChevronDown size={14} aria-hidden />
      </button>
      {open && rect && shown.length > 0 && createPortal(
        <ul id={listId} role="listbox" className={styles.comboList}
          style={{ left: rect.left, top: rect.bottom + 2, width: rect.width }}>
          {shown.map((o, i) => (
            <li key={o} id={`${listId}-${i}`} role="option" aria-selected={o === value}
              className={clsx(styles.comboOption, i === active && styles.comboOptionActive, o === value && styles.comboOptionChosen)}
              onMouseDown={(e) => { e.preventDefault(); pick(o); }} onMouseEnter={() => setActive(i)}>
              {o}
            </li>
          ))}
        </ul>,
        document.body,
      )}
    </span>
  );
}

/**
 * Whole-number field that can be cleared and retyped: while focused it shows exactly what is typed, and only a
 * valid number (kept within min–max) reaches onChange. Left empty, it gives `null` on blur when `allowEmpty`, and
 * otherwise goes back to the current value. Without this, clearing "3" to type "5" snapped to the minimum at once.
 */
export function NumberInput({ value, onChange, min, max, allowEmpty = false, onFocus, onBlur, ...props }:
  Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'min' | 'max' | 'type'> & {
    value: number | null | undefined;
    onChange: (value: number | null) => void;
    min: number;
    max: number;
    allowEmpty?: boolean;
  }) {
  const [text, setText] = useState<string | null>(null);
  const shown = text ?? (value == null ? '' : String(value));
  return (
    <input {...props} type="number" inputMode="numeric" min={min} max={max} value={shown}
      onFocus={(e) => { setText(shown); e.target.select(); onFocus?.(e); }}
      onChange={(e) => {
        setText(e.target.value);
        const n = parseWholeNumber(e.target.value, min, max);
        if (n !== null) onChange(n);
      }}
      onBlur={(e) => {
        if (allowEmpty && text !== null && text.trim() === '') onChange(null);
        setText(null);
        onBlur?.(e);
      }} />
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

/**
 * A short hint on hover or keyboard focus. The bubble is portaled so scrolling lists don't clip it, and the wrapper uses
 * display: contents, so the child keeps its place in grids and flex rows. Without content it renders only the child.
 */
export function Tooltip({ content, children }: { content?: ReactNode; children: ReactElement<{ 'aria-describedby'?: string }> }) {
  const id = useId();
  const wrap = useRef<HTMLSpanElement>(null);
  const timer = useRef<number | undefined>(undefined);
  const [rect, setRect] = useState<DOMRect | null>(null);

  useEffect(() => () => window.clearTimeout(timer.current), []);
  useEffect(() => {
    if (!rect) return;
    const hide = () => setRect(null);
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') hide(); };
    window.addEventListener('scroll', hide, true);
    window.addEventListener('keydown', onKey);
    return () => { window.removeEventListener('scroll', hide, true); window.removeEventListener('keydown', onKey); };
  }, [rect]);

  if (!content) return children;
  const show = () => {
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => setRect(wrap.current?.firstElementChild?.getBoundingClientRect() ?? null), 300);
  };
  const hide = () => { window.clearTimeout(timer.current); setRect(null); };
  const below = rect != null && rect.top < 100;
  const left = rect ? Math.min(Math.max(rect.left + rect.width / 2, 150), window.innerWidth - 150) : 0;

  return (
    <span ref={wrap} className={styles.tooltipAnchor} onMouseEnter={show} onMouseLeave={hide} onBlur={hide}
      onFocus={(e) => { if ((e.target as HTMLElement).matches(':focus-visible')) show(); }}>
      {cloneElement(children, { 'aria-describedby': rect ? id : undefined })}
      {rect && createPortal(
        <span id={id} role="tooltip" className={clsx(styles.tooltip, below && styles.tooltipBelow)}
          style={{ left, top: below ? rect.bottom + 8 : rect.top - 8 }}>
          {content}
        </span>,
        document.body,
      )}
    </span>
  );
}

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
