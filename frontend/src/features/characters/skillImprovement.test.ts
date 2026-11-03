import { describe, expect, it } from 'vitest';
import type { CharacterSkillLine, SkillRule } from '../../api/characterTypes';
import { improvementGroups, NEW_SPEC, specializationChoices, skillCost } from './skillImprovement';

const rule = (id: string, category: string, change: Partial<SkillRule> = {}): SkillRule => ({
  id, name: id, namePt: null, stat: 'INT', category, combat: false, doubleCost: false, specialized: false, page: 1, ...change,
});
const line = (key: string, level: number, ipToRaise: number | null): Pick<CharacterSkillLine, 'key' | 'skillId' | 'name' | 'namePt' | 'specialization' | 'level' | 'ipToRaise'> => {
  const [skillId, specialization = null] = key.split(':');
  return { key, skillId, name: skillId, namePt: null, specialization, level, ipToRaise };
};

const rules = [
  rule('athletics', 'Body'),
  rule('acrobatics', 'Body'),
  rule('language', 'Education', { specialized: true }),
  rule('martial-arts', 'Fighting', { specialized: true, doubleCost: true }),
  rule('pilot-air-vehicle', 'Control', { doubleCost: true }),
];
const report = [
  line('athletics', 4, 100),
  line('acrobatics', 0, 20),
  line('language:English', 8, 180),
  line('language:Japanese', 10, null),
  line('pilot-air-vehicle', 0, 40),
];

describe('improvementGroups', () => {
  const groups = improvementGroups(rules, report);

  it('lists every skill under its category, in the sheet order', () => {
    expect(groups.map((g) => g.category)).toEqual(['Body', 'Control', 'Education', 'Fighting']);
    expect(groups[0].options.map((o) => o.value)).toEqual(['acrobatics', 'athletics']);
  });

  it('shows each specialization the character has, then a way to learn a new one', () => {
    expect(groups[2].options.map((o) => o.value)).toEqual(['language:English', `${NEW_SPEC}language`]);
    expect(groups[3].options.map((o) => o.value)).toEqual([`${NEW_SPEC}martial-arts`]);
  });

  it('carries the level and the cost, and leaves out skills already at the top', () => {
    const athletics = groups[0].options.find((o) => o.value === 'athletics')!;
    expect([athletics.level, athletics.cost]).toEqual([4, 100]);
    expect(groups.flatMap((g) => g.options).some((o) => o.value === 'language:Japanese')).toBe(false);
  });

  it('prices a new specialization at level 1, doubled for (x2) skills', () => {
    expect(groups[3].options[0].cost).toBe(40);
    expect(groups[2].options[1].cost).toBe(20);
  });
});

describe('skillCost', () => {
  it('follows CP:R p.410', () => {
    expect(skillCost(rule('x', 'Body'), 5)).toBe(100);
    expect(skillCost(rule('x', 'Body', { doubleCost: true }), 3)).toBe(120);
  });
});

describe('specializationChoices', () => {
  const sources = { languages: ['English', 'Japanese', 'French'], homes: ['Watson', 'Heywood'], martialArts: ['Aikido', 'Karate'] };

  it('offers a closed list of martial arts and drops the ones already learned', () => {
    const c = specializationChoices('martial-arts', ['martial-arts:Karate'], sources);
    expect(c).toEqual({ options: ['Aikido'], free: false });
  });

  it('lets languages and places outside the list be typed', () => {
    expect(specializationChoices('language', ['language:English'], sources)).toEqual({ options: ['French', 'Japanese'], free: true });
    expect(specializationChoices('local-expert', [], sources).free).toBe(true);
    expect(specializationChoices('local-expert', [], sources).options).toEqual(['Heywood', 'Watson']);
  });

  it('suggests sciences and instruments', () => {
    expect(specializationChoices('science', [], sources).options.length).toBeGreaterThan(3);
    expect(specializationChoices('play-instrument', [], sources).free).toBe(true);
  });
});
