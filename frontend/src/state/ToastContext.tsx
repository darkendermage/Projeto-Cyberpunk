import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { AlertTriangle, CheckCircle2, Info, X } from 'lucide-react';
import styles from './Toasts.module.css';
import { t, tv } from '../i18n';

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

  const dismiss = useCallback((id: number) => setToasts((list) => list.filter((entry) => entry.id !== id)), []);

  const notify = useCallback((message: string, kind: ToastKind = 'success') => {
    const id = nextId++;
    setToasts((list) => [...list.slice(-3), { id, kind, message }]);
    setTimeout(() => dismiss(id), kind === 'success' ? 3200 : 6000);
  }, [dismiss]);

  const value = useMemo<ToastContextValue>(() => ({
    notify,
    error: (error, fallback = t('Algo deu errado.', 'Something went wrong.')) => notify(error instanceof Error && error.message ? error.message : fallback, 'error'),
  }), [notify]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className={styles.stack} role="status" aria-live="polite">
        {toasts.map((toast) => (
          <div key={toast.id} className={`${styles.toast} ${styles[toast.kind]}`}>
            {toast.kind === 'success' ? <CheckCircle2 size={16} /> : toast.kind === 'error' ? <AlertTriangle size={16} /> : <Info size={16} />}
            <span>{tv(toast.message)}</span>
            <button type="button" className={styles.close} onClick={() => dismiss(toast.id)} aria-label={t('Fechar aviso', 'Close notice')}>
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
