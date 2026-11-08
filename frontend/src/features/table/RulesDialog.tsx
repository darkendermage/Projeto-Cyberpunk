import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { tableKeys } from '../../api/table';
import type { TableRules } from '../../api/tableTypes';
import { ErrorState, Modal, Segmented, Spinner } from '../../components/ui/ui';
import { t, tv } from '../../i18n';
import { useRangeUnit } from '../../lib/hooks';
import page from '../../styles/Page.module.css';
import { CriticalTable } from '../reference/CriticalTable';
import { AutofireTable, RangeUnitToggle, SingleShotTable } from '../reference/RangeTables';
import styles from './Table.module.css';

type Tab = 'range' | 'injuries' | 'cover' | 'rules';

// Range DVs live in the app; crits, cover and area rules come from the server (players' port has its own route).
export function RulesDialog({ open, onClose, load }: { open: boolean; onClose: () => void; load: () => Promise<TableRules> }) {
  const [tab, setTab] = useState<Tab>('range');
  const [unit, setUnit] = useRangeUnit();
  const rules = useQuery({ queryKey: tableKeys.rules, queryFn: load, enabled: open, staleTime: Infinity });
  const data = rules.data;
  return (
    <Modal open={open} onClose={onClose} title={t('Tabelas', 'Tables')} wide>
      <div className={styles.stack}>
        <Segmented<Tab> ariaLabel={t('Tabela', 'Table')} value={tab} onChange={setTab}
          options={[
            { value: 'range', label: t('Alcance', 'Range') },
            { value: 'injuries', label: t('Ferimentos Críticos', 'Critical Injuries') },
            { value: 'cover', label: t('Cobertura', 'Cover') },
            { value: 'rules', label: t('Áreas e regras', 'Areas and rules') },
          ]} />
        {tab === 'range' && (
          <>
            <RangeUnitToggle unit={unit} onChange={setUnit} />
            <h3 className={styles.title}>{t('Tiro único (CP:R p.173)', 'Single Shot (CP:R p.173)')}</h3>
            <SingleShotTable unit={unit} />
            <h3 className={styles.title}>Autofire</h3>
            <AutofireTable unit={unit} />
          </>
        )}
        {tab !== 'range' && (rules.isLoading ? <Spinner /> : rules.error || !data ? <ErrorState error={rules.error} onRetry={() => rules.refetch()} /> : (
          <>
            {tab === 'injuries' && (
              <div className={styles.stack}>
                <CriticalTable title={t('Corpo (CP:R p.187)', 'Body (CP:R p.187)')} rows={data.criticalInjuries.filter((i) => i.location === 'Body')} />
                <CriticalTable title={t('Cabeça (CP:R p.188)', 'Head (CP:R p.188)')} rows={data.criticalInjuries.filter((i) => i.location === 'Head')} />
              </div>
            )}
            {tab === 'cover' && (
              <div className={styles.stack}>
                <span className={styles.source}>{data.coverMaterialsSource}</span>
                <div className={page.tableWrap}>
                  <table className={page.table}>
                    <thead><tr><th>{t('Material', 'Material')}</th><th>{t('Grossa (HP)', 'Thick (HP)')}</th><th>{t('Fina (HP)', 'Thin (HP)')}</th></tr></thead>
                    <tbody>{data.coverMaterials.map((m) => <tr key={m.id}><td>{tv(m.label)}</td><td>{m.thick}</td><td>{m.thin || '—'}</td></tr>)}</tbody>
                  </table>
                </div>
                <span className={styles.source}>{data.coverExamplesSource}</span>
                <div className={page.tableWrap}>
                  <table className={page.table}>
                    <thead><tr><th>{t('Exemplo', 'Example')}</th><th>HP</th></tr></thead>
                    <tbody>{data.coverExamples.map((c) => <tr key={c.label}><td>{tv(c.label)}</td><td>{c.hp}</td></tr>)}</tbody>
                  </table>
                </div>
              </div>
            )}
            {tab === 'rules' && (
              <div className={styles.stack}>
                {data.notes.map((n) => (
                  <div key={n.title} className={styles.section}>
                    <span className={styles.title}>{n.title}</span>
                    <span>{tv(n.text)}</span>
                    <span className={styles.source}>{n.source}</span>
                  </div>
                ))}
              </div>
            )}
          </>
        ))}
      </div>
    </Modal>
  );
}
