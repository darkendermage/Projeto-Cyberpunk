import clsx from 'clsx';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { PAGE_SIZES } from '../../api/hooks';
import { Select } from '../../components/ui/ui';
import { pageWindow } from '../../lib/pagination';
import styles from './Pagination.module.css';

interface Props {
  page: number;
  totalPages: number;
  pageSize: number;
  onPage: (page: number) => void;
  onPageSize: (size: number) => void;
}

export function Pagination({ page, totalPages, pageSize, onPage, onPageSize }: Props) {
  return (
    <nav className={styles.bar} aria-label="Paginação">
      <div className={styles.pages}>
        <button type="button" className={styles.page} disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="Página anterior">
          <ChevronLeft size={16} />
        </button>
        {pageWindow(page, Math.max(totalPages, 1)).map((p, i) => p === '…'
          ? <span key={`gap-${i}`} className={styles.gap}>…</span>
          : (
            <button key={p} type="button" className={clsx(styles.page, p === page && styles.current)}
              aria-current={p === page ? 'page' : undefined} onClick={() => onPage(p)}>
              {p}
            </button>
          ))}
        <button type="button" className={styles.page} disabled={page >= totalPages} onClick={() => onPage(page + 1)} aria-label="Próxima página">
          <ChevronRight size={16} />
        </button>
      </div>
      <label className={styles.size}>
        <span>Itens por página</span>
        <Select value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))} aria-label="Itens por página">
          {PAGE_SIZES.map((s) => <option key={s} value={s}>{s}</option>)}
        </Select>
      </label>
    </nav>
  );
}
