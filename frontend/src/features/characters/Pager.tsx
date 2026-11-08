import { ChevronLeft, ChevronRight } from 'lucide-react';
import { t } from '../../i18n';
import { PAGE_SIZE } from './gearLayout';
import styles from './Characters.module.css';

// "Exibindo 11–20 de 34" plus the arrows. noun comes already translated.
export function Pager({ page, pages, rows, total, noun, onPage, size = PAGE_SIZE }: {
  page: number; pages: number; rows: number; total: number; noun: string; onPage: (page: number) => void; size?: number;
}) {
  const first = total === 0 ? 0 : page * size + 1;
  const last = total === 0 ? 0 : page * size + rows;
  return (
    <footer className={styles.gearPager}>
      <span className={styles.muted}>{t(`Exibindo ${first}–${last} de ${total} ${noun}`, `Showing ${first}–${last} of ${total} ${noun}`)}</span>
      {pages > 1 && (
        <span className={styles.row} style={{ gap: 4 }}>
          <button type="button" className={styles.gearPage} disabled={page === 0} onClick={() => onPage(page - 1)} aria-label={t('Página anterior', 'Previous page')}><ChevronLeft size={14} /></button>
          <span className={styles.small}>{page + 1}/{pages}</span>
          <button type="button" className={styles.gearPage} disabled={page >= pages - 1} onClick={() => onPage(page + 1)} aria-label={t('Próxima página', 'Next page')}><ChevronRight size={14} /></button>
        </span>
      )}
    </footer>
  );
}
