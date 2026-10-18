import type { CategorySchema, FieldDescriptor, FilterCriterionDto, FilterOp } from '../api/types';

/**
 * UI-side filter model: one criterion per field, values kept as strings (as they live in the URL).
 * Criteria are AND-ed by the API; multiple values inside "in" are OR-ed.
 */
export interface Criterion {
  field: string;
  op: FilterOp;
  values: string[];
}

export const OP_LABELS: Record<FilterOp, string> = {
  eq: '=',
  neq: '≠',
  gt: '>',
  gte: '≥',
  lt: '<',
  lte: '≤',
  between: 'entre',
  in: 'é',
  nin: 'não é',
  all: 'tem todos',
  contains: 'contém',
  isnull: 'sem valor',
  notnull: 'com valor',
};

const VALUELESS: FilterOp[] = ['isnull', 'notnull'];

// ---------------------------------------------------------------- URL encoding: f=field.op:v1|v2

const enc = (v: string) => encodeURIComponent(v).replace(/\./g, '%2E');

export function encodeCriterion(c: Criterion): string {
  return `${c.field}.${c.op}:${c.values.map(enc).join('|')}`;
}

export function decodeCriterion(raw: string): Criterion | null {
  const match = raw.match(/^([A-Za-z0-9_-]+)\.([a-z]+):(.*)$/);
  if (!match) return null;
  const [, field, op, rest] = match;
  if (!(op in OP_LABELS)) return null;
  let values: string[];
  try {
    values = rest === '' ? [] : rest.split('|').map((v) => decodeURIComponent(v));
  } catch {
    return null;
  }
  return { field, op: op as FilterOp, values };
}

// ---------------------------------------------------------------- state transitions (pure)

export function getCriterion(criteria: Criterion[], field: string): Criterion | undefined {
  return criteria.find((c) => c.field === field);
}

/** Replaces the criterion of a field (or removes it when null). Order of the other criteria is kept. */
export function setCriterion(criteria: Criterion[], field: string, next: Criterion | null): Criterion[] {
  const index = criteria.findIndex((c) => c.field === field);
  if (!next) return index < 0 ? criteria : criteria.filter((c) => c.field !== field);
  if (index < 0) return [...criteria, next];
  const copy = criteria.slice();
  copy[index] = next;
  return copy;
}

/** Adds/removes a value of a multi-select field, keeping its current operator. */
export function toggleValue(criteria: Criterion[], field: string, value: string, defaultOp: FilterOp): Criterion[] {
  const current = getCriterion(criteria, field);
  const values = current?.values ?? [];
  const nextValues = values.includes(value) ? values.filter((v) => v !== value) : [...values, value];
  return setCriterion(criteria, field, nextValues.length ? { field, op: current?.op ?? defaultOp, values: nextValues } : null);
}

export function setOperator(criteria: Criterion[], field: string, op: FilterOp): Criterion[] {
  const current = getCriterion(criteria, field);
  if (!current) return criteria;
  return setCriterion(criteria, field, { ...current, op });
}

export function isComplete(c: Criterion): boolean {
  if (VALUELESS.includes(c.op)) return true;
  if (c.op === 'between') return c.values.some((v) => v !== '' && v !== undefined);
  return c.values.length > 0 && c.values.every((v) => v !== '');
}

// ---------------------------------------------------------------- conversion to API payload

export function toApiCriteria(criteria: Criterion[], schema: CategorySchema | undefined): FilterCriterionDto[] {
  if (!schema) return [];
  const fields = new Map(schema.fields.map((f) => [f.key, f]));
  const result: FilterCriterionDto[] = [];

  for (const c of criteria) {
    const field = fields.get(c.field);
    if (!field || !field.filterable || !field.operators.includes(c.op) || !isComplete(c)) continue;

    if (VALUELESS.includes(c.op)) {
      result.push({ field: c.field, op: c.op });
    } else if (field.kind === 'number') {
      if (c.op === 'between') {
        const [min, max] = c.values.map(toNumber);
        if (min === null && max === null) continue;
        result.push({ field: c.field, op: c.op, min, max });
      } else {
        const value = toNumber(c.values[0]);
        if (value === null) continue;
        result.push({ field: c.field, op: c.op, value });
      }
    } else if (field.kind === 'boolean') {
      result.push({ field: c.field, op: 'eq', value: c.values[0] === 'true' });
    } else {
      result.push({ field: c.field, op: c.op, values: c.values });
    }
  }
  return result;
}

function toNumber(value: string | undefined): number | null {
  if (value === undefined || value.trim() === '') return null;
  const n = Number(value.replace(',', '.'));
  return Number.isFinite(n) ? n : null;
}

// ---------------------------------------------------------------- presentation helpers

/** Fields whose filters make sense for the subtypes currently selected (e.g. hide SP when only "Roupa" is selected). */
export function isFieldApplicable(field: FieldDescriptor, criteria: Criterion[], schema: CategorySchema): boolean {
  if (!field.appliesTo?.length || !schema.subtypeField) return true;
  const subtype = getCriterion(criteria, schema.subtypeField);
  if (!subtype || subtype.op !== 'in' || subtype.values.length === 0) return true;
  return subtype.values.some((v) => field.appliesTo!.includes(v));
}

export function describeCriterion(
  c: Criterion,
  field: FieldDescriptor | undefined,
  labelFor: (field: string, value: string) => string,
): string {
  const label = field?.label ?? c.field;
  const unit = field?.unit && field.unit !== 'penalty' ? ` ${field.unit}` : '';
  if (VALUELESS.includes(c.op)) return `${label}: ${OP_LABELS[c.op]}`;
  if (field?.kind === 'boolean') return `${label}: ${c.values[0] === 'true' ? 'Sim' : 'Não'}`;
  if (field?.kind === 'number') {
    if (c.op === 'between') {
      const [min, max] = c.values;
      if (min && max) return `${label} entre ${min} e ${max}${unit}`;
      return min ? `${label} ≥ ${min}${unit}` : `${label} ≤ ${max}${unit}`;
    }
    return `${label} ${OP_LABELS[c.op]} ${c.values[0]}${unit}`;
  }
  const values = c.values.map((v) => labelFor(c.field, v)).join(', ');
  if (c.op === 'nin') return `${label} exceto ${values}`;
  if (c.op === 'all' && c.values.length > 1) return `${label}: ${values} (todos)`;
  return `${label}: ${values}`;
}
