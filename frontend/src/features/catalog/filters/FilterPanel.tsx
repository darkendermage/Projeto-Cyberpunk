import { useEffect, useMemo, useState, type ReactNode } from 'react';
import clsx from 'clsx';
import { Filter, Search } from 'lucide-react';
import type { CategorySchema, FacetValue, FieldDescriptor } from '../../../api/types';
import { Collapsible, Switch } from '../../../components/ui/ui';
import { type Criterion, encodeCriterion, getCriterion, isFieldApplicable } from '../../../lib/filters';
import { useDebouncedValue } from '../../../lib/hooks';
import { shortLabel } from '../../../lib/format';
import { EnumFilter } from './EnumFilter';
import { NumberFilter } from './NumberFilter';
import { PriceFilter } from './PriceFilter';
import { asRange, BooleanSelect, RangeFilter, SelectFilter } from './SelectFilters';
import styles from './Filters.module.css';

interface Props {
  schema: CategorySchema;
  facets: Record<string, FacetValue[]> | undefined;
  criteria: Criterion[];
  q: string;
  onCriteria: (criteria: Criterion[]) => void;
  onQuery: (q: string) => void;
  onClear: () => void;
  /** Called after "Aplicar Filtros" (the mobile drawer closes itself). */
  onApplied?: () => void;
}

const SOURCE_FIELDS = ['source', 'ruleSystem', 'dataOrigin', 'priceCategory'];
const signature = (criteria: Criterion[]) => criteria.map(encodeCriterion).sort().join('&');

/**
 * Filters are generated from the category schema. Changes are staged in a draft and sent with
 * "Aplicar Filtros", so several criteria can be adjusted before a single search runs.
 */
export function FilterPanel({ schema, facets, criteria, q, onCriteria, onQuery, onClear, onApplied }: Props) {
  const [draft, setDraft] = useState(criteria);
  const [advanced, setAdvanced] = useState(false);
  const applied = signature(criteria);
  useEffect(() => setDraft(criteria), [applied]); // eslint-disable-line react-hooks/exhaustive-deps
  const dirty = signature(draft) !== applied;

  const fields = useMemo(() => new Map(schema.fields.map((f) => [f.key, f])), [schema]);
  const ordered = schema.filterOrder
    .map((key) => fields.get(key))
    .filter((f): f is FieldDescriptor => Boolean(f?.filterable))
    .filter((f) => isFieldApplicable(f, draft, schema));

  const subtype = ordered.find((f) => f.key === schema.subtypeField);
  const numbers = ordered.filter((f) => f.kind === 'number' && f.key !== 'price' && f.min != null && f.max != null && f.min !== f.max);
  const price = ordered.find((f) => f.key === 'price');
  const selects = ordered.filter((f) => (f.kind === 'enum' || f.kind === 'set') && f !== subtype && f.key !== 'tags' && !SOURCE_FIELDS.includes(f.key));
  const booleans = ordered.filter((f) => f.kind === 'boolean');
  const tags = ordered.find((f) => f.key === 'tags');
  const sourceFields = ordered.filter((f) => SOURCE_FIELDS.includes(f.key));
  const hasOptions = (f: FieldDescriptor) => (facets?.[f.key]?.length ?? f.options?.length ?? 0) > 0 || Boolean(getCriterion(draft, f.key));

  const apply = () => {
    onCriteria(draft);
    onApplied?.();
  };

  return (
    <form className={styles.panel} onSubmit={(e) => { e.preventDefault(); apply(); }}>
      <header className={styles.header}>
        <h2 className="hud-title">Filtros</h2>
        <button type="button" className={styles.clearAll} onClick={() => { setDraft([]); onClear(); }}
          disabled={criteria.length === 0 && draft.length === 0 && !q}>
          Limpar tudo
        </button>
      </header>

      <SearchBox value={q} onChange={onQuery} placeholder={`Buscar em ${schema.label.toLowerCase()}...`} />

      {subtype && hasOptions(subtype) && (
        <Block title={subtype.label}>
          <EnumFilter field={subtype} facets={facets?.[subtype.key]} criteria={draft} onChange={setDraft} columns={2} limit={20} />
        </Block>
      )}

      {selects.filter((f) => f.key === 'manufacturer' && hasOptions(f)).map((f) => (
        <Block key={f.key} title="Fabricante">
          <SelectFilter field={f} facets={facets?.[f.key]} criteria={draft} onChange={setDraft} />
        </Block>
      ))}

      {numbers.length > 0 && (
        <div className={styles.rows}>
          {numbers.map((f) => {
            const criterion = getCriterion(draft, f.key);
            const useOperators = advanced || (criterion !== undefined && asRange(criterion) === null);
            return useOperators ? (
              <div key={f.key} className={styles.advancedRow}><NumberFilter field={f} criteria={draft} onChange={setDraft} /></div>
            ) : (
              <Row key={f.key} label={shortLabel(f)} hint={f.description ?? f.label} active={Boolean(criterion)}>
                <RangeFilter field={f} criteria={draft} onChange={setDraft} />
              </Row>
            );
          })}
          <div className={styles.advancedToggle}>
            <Switch checked={advanced} onChange={setAdvanced} label="Operadores avançados (=, ≠, >, <)" />
          </div>
        </div>
      )}

      {price && price.max != null && (
        <Block title="Custo (eddies)">
          <PriceFilter field={price} criteria={draft} onChange={setDraft} />
        </Block>
      )}

      {(booleans.length > 0 || selects.some((f) => f.key !== 'manufacturer')) && (
        <div className={styles.rows}>
          {booleans.map((f) => (
            <Row key={f.key} label={f.key === 'concealable' ? 'Ocultabilidade' : shortLabel(f)} hint={f.label} active={Boolean(getCriterion(draft, f.key))}>
              <BooleanSelect field={f} criteria={draft} onChange={setDraft} />
            </Row>
          ))}
          {selects.filter((f) => f.key !== 'manufacturer' && hasOptions(f)).map((f) => (
            <Row key={f.key} label={f.key === 'calibers' ? 'Tipo de munição' : shortLabel(f)} hint={f.description ?? f.label}
              active={Boolean(getCriterion(draft, f.key))}>
              <SelectFilter field={f} facets={facets?.[f.key]} criteria={draft} onChange={setDraft} />
            </Row>
          ))}
        </div>
      )}

      {tags && hasOptions(tags) && (
        <Collapsible title={tags.label} active={getCriterion(draft, tags.key) ? 1 : 0}>
          <EnumFilter field={tags} facets={facets?.[tags.key]} criteria={draft} onChange={setDraft} columns={2} />
        </Collapsible>
      )}

      {sourceFields.length > 0 && (
        <Collapsible title="Fonte, sistema & origem" active={sourceFields.filter((f) => getCriterion(draft, f.key)).length}>
          <div className={styles.rows}>
            {sourceFields.filter(hasOptions).map((f) => (
              <Row key={f.key} label={f.label} active={Boolean(getCriterion(draft, f.key))}>
                <SelectFilter field={f} facets={facets?.[f.key]} criteria={draft} onChange={setDraft} />
              </Row>
            ))}
          </div>
        </Collapsible>
      )}

      <div className={styles.applyBar}>
        {dirty && <p className={styles.pending} role="status">Há alterações não aplicadas.</p>}
        <button type="submit" className={clsx(styles.apply, dirty && styles.applyDirty)}>
          <Filter size={16} />
          Aplicar Filtros
        </button>
      </div>
    </form>
  );
}

function Block({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className={styles.block}>
      <h3 className={styles.blockTitle}>{title}</h3>
      {children}
    </section>
  );
}

function Row({ label, children, hint, active }: { label: string; children: ReactNode; hint?: string; active?: boolean }) {
  return (
    <div className={styles.row}>
      <span className={clsx(styles.rowLabel, active && styles.rowActive)} title={hint}>{label}</span>
      <div className={styles.rowControl}>{children}</div>
    </div>
  );
}

function SearchBox({ value, onChange, placeholder }: { value: string; onChange: (v: string) => void; placeholder: string }) {
  const [text, setText] = useState(value);
  const debounced = useDebouncedValue(text, 350);
  useEffect(() => setText(value), [value]);
  useEffect(() => { if (debounced !== value) onChange(debounced); }, [debounced]); // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <label className={styles.search}>
      <Search size={16} />
      <input type="search" value={text} onChange={(e) => setText(e.target.value)} placeholder={placeholder} aria-label={placeholder}
        onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); onChange(text); } }} />
    </label>
  );
}
