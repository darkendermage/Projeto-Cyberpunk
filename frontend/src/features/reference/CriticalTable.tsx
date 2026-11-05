import { Badge } from '../../components/ui/ui';
import type { CriticalInjuryRule } from '../../api/characterTypes';
import page from '../../styles/Page.module.css';
import styles from './ReferencePages.module.css';
import { t, tv } from '../../i18n';

/** One Critical Injury table (body or head), as the book prints it. */
export function CriticalTable({ title, rows }: { title: string; rows: CriticalInjuryRule[] }) {
  return (
    <div>
      <h3 className={styles.subTitle}>{title}</h3>
      <div className={page.tableWrap}>
        <table className={page.table}>
          <thead><tr><th>2d6</th><th>{t('Lesão', 'Injury')}</th><th>{t('Efeito', 'Effect')}</th><th>Quick Fix</th><th>{t('Tratamento', 'Treatment')}</th></tr></thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.roll}>
                <td className={styles.dv}>{r.roll}</td>
                <td className={styles.injuryName}>{tv(r.name)}</td>
                <td>
                  {tv(r.effect)}
                  {r.deathSave && <> <Badge tone="red" title={t('A penalidade base de Death Save aumenta em 1', 'The base Death Save penalty goes up by 1')}>Death Save +1</Badge></>}
                </td>
                <td>{r.quickFix ?? '—'}</td>
                <td>{r.treatment}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
