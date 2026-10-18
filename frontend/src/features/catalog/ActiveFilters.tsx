import type { CategorySchema, FacetValue } from '../../api/types';
import { Chip } from '../../components/ui/ui';
import { type Criterion, describeCriterion, setCriterion } from '../../lib/filters';
import { optionLabel } from '../../lib/format';
import styles from './CatalogPage.module.css';

interface Props {
  schema: CategorySchema;
  facets: Record<string, FacetValue[]> | undefined;
  criteria: Criterion[];
  q: string;
  onCriteria: (criteria: Criterion[]) => void;
  onQuery: (q: string) => void;
  onClear: () => void;
}

/** Removable chips for every applied criterion — the "AND" of the query, made visible. */
export function ActiveFilters({ schema, facets, criteria, q, onCriteria, onQuery, onClear }: Props) {
  if (criteria.length === 0 && !q) return null;
  const fields = new Map(schema.fields.map((f) => [f.key, f]));
  const labelFor = (field: string, value: string) =>
    facets?.[field]?.find((f) => f.value === value)?.label ?? optionLabel(fields.get(field), value);

  return (
    <div className={styles.activeFilters} aria-label="Filtros aplicados">
      {q && <Chip onRemove={() => onQuery('')}>Busca: “{q}”</Chip>}
      {criteria.map((c) => {
        const text = describeCriterion(c, fields.get(c.field), labelFor);
        return <Chip key={c.field} title={text} onRemove={() => onCriteria(setCriterion(criteria, c.field, null))}>{text}</Chip>;
      })}
      {criteria.length + (q ? 1 : 0) > 1 && (
        <button type="button" className={styles.clearChips} onClick={onClear}>Limpar todos</button>
      )}
    </div>
  );
}
