import { describe, expect, it } from 'vitest';
import type { CharacterSkillLine } from '../../api/characterTypes';
import { filterSkills, groupSkills, languagesFor, signed, skillLabel } from './characterLabels';

const line = (over: Partial<CharacterSkillLine>): CharacterSkillLine => ({
  key: 'x', skillId: 'x', name: 'X', namePt: null, specialization: null, stat: 'INT', category: 'Awareness',
  level: 0, statValue: 5, modifier: 0, base: 5, actionPenalty: 0, total: 5, doubleCost: false, combat: false,
  basic: false, roleSkill: false, page: 82, ipToRaise: null, modifierParts: [], situational: [], ...over,
});

const lines = [
  line({ key: 'handgun', skillId: 'handgun', name: 'Handgun', namePt: 'Armas de Mão', stat: 'REF', category: 'Ranged Weapon', level: 6, combat: true, roleSkill: true }),
  line({ key: 'language:Streetslang', skillId: 'language', name: 'Language', namePt: 'Idioma', specialization: 'Streetslang', category: 'Education', level: 2, basic: true }),
  line({ key: 'concentration', skillId: 'concentration', name: 'Concentration', namePt: 'Concentração', stat: 'WILL' }),
];

describe('skill helpers', () => {
  it('labels skills in Portuguese with the specialization', () => {
    expect(skillLabel(lines[1])).toBe('Idioma (Streetslang)');
    expect(skillLabel(line({ name: 'Brawling' }))).toBe('Brawling');
  });

  it('searches Portuguese and English names without accents', () => {
    expect(filterSkills(lines, 'concentracao', 'all').map((l) => l.key)).toEqual(['concentration']);
    expect(filterSkills(lines, 'handgun', 'all').map((l) => l.key)).toEqual(['handgun']);
    expect(filterSkills(lines, 'streetslang', 'all')).toHaveLength(1);
  });

  it('filters by trained, role, basic and combat skills', () => {
    expect(filterSkills(lines, '', 'trained')).toHaveLength(2);
    expect(filterSkills(lines, '', 'role').map((l) => l.key)).toEqual(['handgun']);
    expect(filterSkills(lines, '', 'basic').map((l) => l.key)).toEqual(['language:Streetslang']);
    expect(filterSkills(lines, '', 'combat').map((l) => l.key)).toEqual(['handgun']);
  });

  it('groups skills in sheet order and drops empty categories', () => {
    expect(groupSkills(lines).map((g) => g.key)).toEqual(['Awareness', 'Education', 'Ranged Weapon']);
  });

  it('prints signed modifiers', () => {
    expect(signed(2)).toBe('+2');
    expect(signed(-4)).toBe('-4');
    expect(signed(0)).toBe('0');
  });

  it('suggests the Cultural Origin languages, or every language before a region is chosen', () => {
    const rules = { culturalOrigins: [
      { region: 'East Asian', languages: ['Japanese', 'English'] },
      { region: 'Western European', languages: ['French', 'English'] },
    ] };
    expect(languagesFor(rules, 'East Asian')).toEqual(['Japanese', 'English']);
    expect(languagesFor(rules, null)).toEqual(['English', 'French', 'Japanese']);
    expect(languagesFor(rules, 'Unknown')).toEqual(['English', 'French', 'Japanese']);
  });
});
