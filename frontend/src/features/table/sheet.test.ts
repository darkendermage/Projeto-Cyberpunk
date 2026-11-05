import { describe, expect, it } from 'vitest';
import { groupSkills } from './sheet';
import type { PlayerSheetSkill } from '../../api/tableTypes';

const skill = (name: string, category: string, level = 2): PlayerSheetSkill => ({ name, namePt: null, specialization: null, category, level, total: level + 5 });

describe('groupSkills', () => {
  it('groups trained skills by category in sheet order, by name inside', () => {
    const groups = groupSkills([skill('Stealth', 'Body'), skill('Handgun', 'Ranged Weapon'), skill('Athletics', 'Body'), skill('Concentration', 'Awareness')]);
    expect(groups.map((g) => g.key)).toEqual(['Awareness', 'Body', 'Ranged Weapon']);
    expect(groups[1].skills.map((s) => s.name)).toEqual(['Athletics', 'Stealth']);
  });

  it('puts an unknown category at the end', () => {
    expect(groupSkills([skill('Odd', 'Strange'), skill('Brawling', 'Fighting')]).map((g) => g.key)).toEqual(['Fighting', 'Strange']);
  });
});
