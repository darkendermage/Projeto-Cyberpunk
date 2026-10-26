import { describe, expect, it } from 'vitest';
import type { CategorySchema, FieldDescriptor } from '../api/types';
import {
  type Criterion, decodeCriterion, describeCriterion, encodeCriterion, isFieldApplicable, setCriterion,
  toApiCriteria, toggleValue,
} from './filters';

const field = (partial: Partial<FieldDescriptor> & Pick<FieldDescriptor, 'key' | 'kind'>): FieldDescriptor => ({
  label: partial.key,
  filterable: true,
  sortable: false,
  facet: false,
  editable: true,
  required: false,
  base: false,
  operators: partial.kind === 'number'
    ? ['eq', 'neq', 'gt', 'gte', 'lt', 'lte', 'between']
    : partial.kind === 'boolean' ? ['eq'] : ['in', 'nin', 'eq', 'neq', 'all'],
  ...partial,
});

const schema: CategorySchema = {
  key: 'weapons',
  category: 'Weapon',
  label: 'Armas',
  description: '',
  icon: 'weapons',
  subtypeField: 'weaponClass',
  cardFields: [],
  tableFields: [],
  filterOrder: [],
  sortOptions: [],
  defaultSort: 'name',
  itemCount: 0,
  fields: [
    field({ key: 'weaponClass', kind: 'enum', label: 'Tipo de arma' }),
    field({ key: 'rof', kind: 'number', label: 'ROF' }),
    field({ key: 'price', kind: 'number', label: 'Preço', unit: 'eb' }),
    field({ key: 'concealable', kind: 'boolean', label: 'Ocultável' }),
    field({ key: 'magazine', kind: 'number', label: 'Pente', appliesTo: ['Smg', 'MediumPistol'] }),
    field({ key: 'notes', kind: 'text', filterable: false, operators: [] }),
  ],
};

describe('URL encoding', () => {
  it('round-trips values containing separators', () => {
    const c: Criterion = { field: 'manufacturer', op: 'in', values: ["Faisal's", 'A|B', 'x.y:z'] };
    expect(decodeCriterion(encodeCriterion(c))).toEqual(c);
  });

  it('rejects malformed input and unknown operators', () => {
    expect(decodeCriterion('rof')).toBeNull();
    expect(decodeCriterion('rof.like:2')).toBeNull();
  });
});

describe('state transitions', () => {
  it('toggles values and removes the criterion when the last value is removed', () => {
    let criteria: Criterion[] = [];
    criteria = toggleValue(criteria, 'weaponClass', 'Smg', 'in');
    criteria = toggleValue(criteria, 'weaponClass', 'HeavySmg', 'in');
    expect(criteria).toEqual([{ field: 'weaponClass', op: 'in', values: ['Smg', 'HeavySmg'] }]);
    criteria = toggleValue(criteria, 'weaponClass', 'Smg', 'in');
    criteria = toggleValue(criteria, 'weaponClass', 'HeavySmg', 'in');
    expect(criteria).toEqual([]);
  });

  it('keeps one criterion per field', () => {
    let criteria = setCriterion([], 'rof', { field: 'rof', op: 'eq', values: ['1'] });
    criteria = setCriterion(criteria, 'rof', { field: 'rof', op: 'gte', values: ['2'] });
    expect(criteria).toEqual([{ field: 'rof', op: 'gte', values: ['2'] }]);
  });
});

describe('toApiCriteria', () => {
  it('combines SMG type and ROF = 2 into two AND-ed criteria (spec example)', () => {
    const criteria: Criterion[] = [
      { field: 'weaponClass', op: 'in', values: ['Smg'] },
      { field: 'rof', op: 'eq', values: ['2'] },
    ];
    expect(toApiCriteria(criteria, schema)).toEqual([
      { field: 'weaponClass', op: 'in', values: ['Smg'] },
      { field: 'rof', op: 'eq', value: 2 },
    ]);
  });

  it('converts numbers, open ranges and booleans', () => {
    const criteria: Criterion[] = [
      { field: 'price', op: 'between', values: ['', '50000'] },
      { field: 'concealable', op: 'eq', values: ['true'] },
    ];
    expect(toApiCriteria(criteria, schema)).toEqual([
      { field: 'price', op: 'between', min: null, max: 50000 },
      { field: 'concealable', op: 'eq', value: true },
    ]);
  });

  it('drops incomplete, unknown or non-filterable criteria instead of sending invalid requests', () => {
    const criteria: Criterion[] = [
      { field: 'rof', op: 'eq', values: [''] },
      { field: 'rof', op: 'gt', values: ['abc'] },
      { field: 'unknown', op: 'eq', values: ['1'] },
      { field: 'notes', op: 'contains', values: ['x'] },
      { field: 'weaponClass', op: 'gt', values: ['Smg'] },
    ];
    expect(toApiCriteria(criteria, schema)).toEqual([]);
  });
});

describe('presentation', () => {
  it('hides attributes that do not apply to the selected subtypes', () => {
    const magazine = schema.fields.find((f) => f.key === 'magazine')!;
    expect(isFieldApplicable(magazine, [], schema)).toBe(true);
    expect(isFieldApplicable(magazine, [{ field: 'weaponClass', op: 'in', values: ['Shotgun'] }], schema)).toBe(false);
    expect(isFieldApplicable(magazine, [{ field: 'weaponClass', op: 'in', values: ['Shotgun', 'Smg'] }], schema)).toBe(true);
  });

  it('describes criteria for the removable chips', () => {
    const label = (_: string, v: string) => (v === 'Smg' ? 'SMG' : v);
    const byKey = (k: string) => schema.fields.find((f) => f.key === k);
    expect(describeCriterion({ field: 'weaponClass', op: 'in', values: ['Smg'] }, byKey('weaponClass'), label)).toBe('Tipo de arma: SMG');
    expect(describeCriterion({ field: 'weaponClass', op: 'nin', values: ['Smg'] }, byKey('weaponClass'), label)).toBe('Tipo de arma except SMG');
    expect(describeCriterion({ field: 'price', op: 'between', values: ['100', '500'] }, byKey('price'), label)).toBe('Preço between 100 and 500 eb');
    expect(describeCriterion({ field: 'rof', op: 'gte', values: ['2'] }, byKey('rof'), label)).toBe('ROF ≥ 2');
  });
});
