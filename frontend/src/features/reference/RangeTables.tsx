import clsx from 'clsx';
import { Segmented } from '../../components/ui/ui';
import {
  AUTOFIRE, AUTOFIRE_BANDS, bandLabel, RANGE_BANDS, type RangeBand, type RangeRow, type RangeUnit, rangeRowsFor, SINGLE_SHOT,
} from '../../lib/rangeTable';
import page from '../../styles/Page.module.css';
import styles from './ReferencePages.module.css';

export function RangeUnitToggle({ unit, onChange }: { unit: RangeUnit; onChange: (unit: RangeUnit) => void }) {
  return (
    <Segmented<RangeUnit> size="sm" ariaLabel="Unidade de distância" value={unit} onChange={onChange}
      options={[{ value: 'meters', label: 'Metros / jardas' }, { value: 'tiles', label: 'Tiles (2 m)' }]} />
  );
}

export function RangeDvTable({
  rows, bands = RANGE_BANDS, unit, highlight,
}: { rows: RangeRow[]; bands?: RangeBand[]; unit: RangeUnit; highlight?: string }) {
  return (
    <div className={page.tableWrap}>
      <table className={clsx(page.table, styles.dvTable)}>
        <thead>
          <tr>
            <th>Arma</th>
            {bands.map((_, i) => <th key={i}>{bandLabel(i, unit, bands)}<small>{unit === 'tiles' ? ' tiles' : ' m'}</small></th>)}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.key} className={clsx(highlight === row.key && styles.highlightRow)}>
              <td>{row.label}</td>
              {row.dv.map((dv, i) => <td key={i} className={styles.dv}>{dv === null ? 'N/A' : dv}</td>)}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function SingleShotTable({ unit, highlight }: { unit: RangeUnit; highlight?: string }) {
  return <RangeDvTable rows={SINGLE_SHOT} unit={unit} highlight={highlight} />;
}

export function AutofireTable({ unit, highlight }: { unit: RangeUnit; highlight?: string }) {
  return <RangeDvTable rows={AUTOFIRE} bands={AUTOFIRE_BANDS} unit={unit} highlight={highlight} />;
}

/** The weapon's own row of the range table, shown in the item sheet. */
export function WeaponRange({ weaponClass, autofire, unit }: { weaponClass?: string; autofire: boolean; unit: RangeUnit }) {
  const { single, autofire: auto } = rangeRowsFor(weaponClass);
  if (!single) return null;
  return (
    <>
      <RangeDvTable rows={[single]} unit={unit} />
      {autofire && auto && <RangeDvTable rows={[{ ...auto, label: `${auto.label} (Autofire)` }]} bands={AUTOFIRE_BANDS} unit={unit} />}
    </>
  );
}
