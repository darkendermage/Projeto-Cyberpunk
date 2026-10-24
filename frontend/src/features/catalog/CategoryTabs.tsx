import { NavLink } from 'react-router';
import clsx from 'clsx';
import type { CategorySummary } from '../../api/types';
import { CategoryIcon } from '../../components/art/CategoryIcon';
import styles from './CategoryTabs.module.css';

export function CategoryTabs({ categories, view }: { categories: CategorySummary[]; view: string }) {
  return (
    <nav className={styles.tabs} aria-label="Categorias">
      {categories.map((c) => (
        <NavLink key={c.key} to={`/catalog/${c.key}?view=${view}`} className={({ isActive }) => clsx(styles.tab, isActive && styles.active)}
          title={`${c.label} — ${c.itemCount} itens`}>
          <CategoryIcon icon={c.icon} size={26} className={styles.icon} />
          <span className={styles.label}>{c.label}</span>
          {c.sublabel && <span className={styles.sublabel}>{c.sublabel}</span>}
          <span className={styles.count}>{c.itemCount}</span>
        </NavLink>
      ))}
    </nav>
  );
}
