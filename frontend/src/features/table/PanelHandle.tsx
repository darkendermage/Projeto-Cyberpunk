import { useRef } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { t } from '../../i18n';
import { clampPanel, type PanelLimits } from './panels';
import styles from './Table.module.css';

// Drag to resize, double-click to reset, arrow keys for small steps, button to hide/show.
export function PanelHandle({ panel, width, limits, open, onResize, onToggle, className }: {
  panel: 'left' | 'right';
  width: number;
  limits: PanelLimits;
  open: boolean;
  onResize: (width: number) => void;
  onToggle: () => void;
  className?: string;
}) {
  const start = useRef<{ x: number; width: number } | null>(null);
  // The left panel grows to the right, the right one to the left.
  const sign = panel === 'left' ? 1 : -1;
  const pointsLeft = panel === 'left' ? open : !open;
  return (
    <div className={`${styles.handle} ${className ?? ''}`} role="separator" aria-orientation="vertical" tabIndex={open ? 0 : -1}
      aria-label={t('Arraste para mudar a largura do painel', 'Drag to change the panel width')}
      aria-valuenow={open ? width : 0} aria-valuemin={limits.min} aria-valuemax={limits.max}
      onPointerDown={(e) => {
        if (!open || (e.target as HTMLElement).closest('button')) return;
        e.currentTarget.setPointerCapture(e.pointerId);
        start.current = { x: e.clientX, width };
      }}
      onPointerMove={(e) => { if (start.current) onResize(clampPanel(limits, start.current.width + sign * (e.clientX - start.current.x))); }}
      onPointerUp={() => { start.current = null; }}
      onPointerCancel={() => { start.current = null; }}
      onDoubleClick={() => { if (open) onResize(limits.initial); }}
      onKeyDown={(e) => {
        if (!open || (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight')) return;
        e.preventDefault();
        onResize(clampPanel(limits, width + (e.key === 'ArrowRight' ? 16 : -16) * sign));
      }}>
      <button type="button" className={styles.handleToggle} onClick={onToggle}
        aria-label={open ? t('Esconder painel', 'Hide panel') : t('Mostrar painel', 'Show panel')} title={open ? t('Esconder painel', 'Hide panel') : t('Mostrar painel', 'Show panel')}>
        {pointsLeft ? <ChevronLeft size={14} /> : <ChevronRight size={14} />}
      </button>
    </div>
  );
}
