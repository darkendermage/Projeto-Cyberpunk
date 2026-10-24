import { useLocation, useNavigate } from 'react-router';
import { GitCompareArrows, X } from 'lucide-react';
import { Button } from '../../components/ui/ui';
import { MAX_COMPARE, useCompare } from '../../state/CompareContext';
import styles from './CompareTray.module.css';

/** Floating bar listing the items selected for comparison. */
export function CompareTray() {
  const compare = useCompare();
  const navigate = useNavigate();
  const location = useLocation();
  if (compare.entries.length === 0 || location.pathname.startsWith('/compare')) return null;

  return (
    <div className={styles.tray} role="region" aria-label="Itens selecionados para comparação">
      <span className={styles.label}><GitCompareArrows size={16} /> {compare.entries.length}/{MAX_COMPARE}</span>
      <div className={styles.items}>
        {compare.entries.map((e) => (
          <span key={e.id} className={styles.item}>
            {e.name}
            <button type="button" onClick={() => compare.remove(e.id)} aria-label={`Remover ${e.name} da comparação`}><X size={12} /></button>
          </span>
        ))}
      </div>
      <Button variant="primary" disabled={compare.entries.length < 2}
        onClick={() => navigate(`/compare?ids=${compare.entries.map((e) => e.id).join(',')}`)}>
        Comparar
      </Button>
      <button type="button" className={styles.clear} onClick={compare.clear}>limpar</button>
    </div>
  );
}
