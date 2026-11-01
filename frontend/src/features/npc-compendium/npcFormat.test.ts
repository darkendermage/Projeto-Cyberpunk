import { describe, expect, it } from 'vitest';
import { formatPrinted, groupNpcSkills, weaponClassLabel } from './npcFormat';
import type { SkillRule } from '../../api/characterTypes';
import { t } from '../../i18n';

describe('formatPrinted', () => {
  it('shows a dash for null and undefined', () => {
    expect(formatPrinted(null)).toBe('—');
    expect(formatPrinted(undefined)).toBe('—');
  });
  it('keeps zero', () => {
    expect(formatPrinted(0)).toBe('0');
  });
  it('prints numbers', () => {
    expect(formatPrinted(17)).toBe('17');
  });
});

describe('weaponClassLabel', () => {
  it('names a WeaponClass like the rest of the app', () => {
    expect(weaponClassLabel('VeryHeavyPistol')).toBe(t('Pistola Muito Pesada', 'Very Heavy Pistol'));
    expect(weaponClassLabel('Smg')).toBe('SMG');
  });
  it('keeps a value it does not know', () => {
    expect(weaponClassLabel('Sling')).toBe('Sling');
  });
});

describe('groupNpcSkills', () => {
  const rule = (name: string, category: string, stat: string, namePt: string | null = null): SkillRule => ({
    id: name.toLowerCase(), name, namePt, stat, category, combat: false, doubleCost: false, specialized: false, page: 1,
  });
  const rules = [rule('Handgun', 'Ranged Weapon', 'REF'), rule('Athletics', 'Body', 'DEX'), rule('Language', 'Education', 'INT'), rule('Stealth', 'Body', 'DEX')];

  it('groups the printed skills like the sheet, in sheet order and by name', () => {
    const groups = groupNpcSkills({ Stealth: 7, Handgun: 14, Athletics: 9 }, {}, rules);
    expect(groups.map((g) => g.key)).toEqual(['Body', 'Ranged Weapon']);
    expect(groups[0].lines.map((l) => l.name)).toEqual(['Athletics', 'Stealth']);
    expect(groups[1].lines[0]).toMatchObject({ stat: 'REF', base: 14, armored: null });
  });
  it('finds a specialized skill by its base name and keeps the specialization', () => {
    const [group] = groupNpcSkills({ 'Language (Streetslang)': 6 }, {}, rules);
    expect(group.key).toBe('Education');
    expect(group.lines[0].name).toBe('Language (Streetslang)');
  });
  it('keeps a differing armored value only', () => {
    const [group] = groupNpcSkills({ Athletics: 13, Stealth: 8 }, { Athletics: 11, Stealth: 8 }, rules);
    expect(group.lines.map((l) => l.armored)).toEqual([11, null]);
  });
  it('puts a skill outside the rule book in a group of its own, last', () => {
    const groups = groupNpcSkills({ Surgery: 12, Handgun: 10 }, {}, rules);
    expect(groups.at(-1)?.key).toBe('Other');
    expect(groups.at(-1)?.lines[0]).toMatchObject({ name: 'Surgery', stat: null, base: 12 });
  });
  it('shows everything under Other when the rules are not loaded', () => {
    expect(groupNpcSkills({ Handgun: 10 }, {}, []).map((g) => g.key)).toEqual(['Other']);
  });
});
