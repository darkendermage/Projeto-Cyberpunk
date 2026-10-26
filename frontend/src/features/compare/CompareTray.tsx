import { useLocation, useNavigate } from 'react-router';
import { GitCompareArrows, X } from 'lucide-react';
import { Button } from '../../components/ui/ui';
import { MAX_COMPARE, useCompare } from '../../state/CompareContext';
import styles from './CompareTray.module.css';
import { t, tv } from '../../i18n';

/** Floating bar listing the items selected for comparison. */
export function CompareTray() {
  const compare = useCompare();
  const navigate = useNavigate();
  const location = useLocation();
  if (compare.entries.length === 0 || location.pathname.startsWith('/compare')) return null;

  return (
    <div className={styles.tray} role="region" aria-label={t('Itens selecionados para comparação', 'Items selected for comparison')}>
      <span className={styles.label}><GitCompareArrows size={16} /> {compare.entries.length}/{MAX_COMPARE}</span>
      <div className={styles.items}>
        {compare.entries.map((e) => (
          <span key={e.id} className={styles.item}>
            {tv(e.name)}
            <button type="button" onClick={() => compare.remove(e.id)} aria-label={t(`Remover ${tv(e.name)} da comparação`, `Remove ${tv(e.name)} from the comparison`)}><X size={12} /></button>
          </span>
        ))}
      </div>
      <Button variant="primary" disabled={compare.entries.length < 2}
        onClick={() => navigate(`/compare?ids=${compare.entries.map((e) => e.id).join(',')}`)}>
        {t('Comparar', 'Compare')}
      </Button>
      <button type="button" className={styles.clear} onClick={compare.clear}>{t('limpar', 'clear')}</button>
    </div>
  );
}
