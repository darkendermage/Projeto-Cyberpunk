import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { AlertTriangle, CheckCircle2, Info, X } from 'lucide-react';
import styles from './Toasts.module.css';

type ToastKind = 'success' | 'error' | 'info';

interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
}

interface ToastContextValue {
  notify: (message: string, kind?: ToastKind) => void;
  error: (error: unknown, fallback?: string) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);
let nextId = 1;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const dismiss = useCallback((id: number) => setToasts((list) => list.filter((t) => t.id !== id)), []);

  const notify = useCallback((message: string, kind: ToastKind = 'success') => {
    const id = nextId++;
    setToasts((list) => [...list.slice(-3), { id, kind, message }]);
    setTimeout(() => dismiss(id), kind === 'success' ? 3200 : 6000);
  }, [dismiss]);

  const value = useMemo<ToastContextValue>(() => ({
    notify,
    error: (error, fallback = 'Algo deu errado.') => notify(error instanceof Error && error.message ? error.message : fallback, 'error'),
  }), [notify]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className={styles.stack} role="status" aria-live="polite">
        {toasts.map((t) => (
          <div key={t.id} className={`${styles.toast} ${styles[t.kind]}`}>
            {t.kind === 'success' ? <CheckCircle2 size={16} /> : t.kind === 'error' ? <AlertTriangle size={16} /> : <Info size={16} />}
            <span>{t.message}</span>
            <button type="button" className={styles.close} onClick={() => dismiss(t.id)} aria-label="Fechar aviso">
              <X size={14} />
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used inside ToastProvider');
  return ctx;
}
