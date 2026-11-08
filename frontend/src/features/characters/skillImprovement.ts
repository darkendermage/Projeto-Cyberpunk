import type { CharacterSkillLine, SkillRule } from '../../api/characterTypes';
import { t } from '../../i18n';
import { SKILL_CATEGORIES, skillLabel } from './characterLabels';

/** Prefix of the option that learns a new specialization of a skill ("new:martial-arts"). */
export const NEW_SPEC = 'new:';

export interface ImproveOption {
  /** The skill key to raise ("athletics", "language:English"), or NEW_SPEC + skill id. */
  value: string;
  skillId: string;
  label: string;
  level: number;
  cost: number;
  isNew: boolean;
}

export interface ImproveGroup { category: string; label: string; options: ImproveOption[] }

type Line = Pick<CharacterSkillLine, 'key' | 'skillId' | 'name' | 'namePt' | 'specialization' | 'level' | 'ipToRaise'>;

/** IP to raise a skill to the next level: 20 × the level, 40 × for skills marked (x2) (CP:R p.410). The server has the final word. */
export function skillCost(rule: Pick<SkillRule, 'doubleCost'>, nextLevel: number): number {
  return nextLevel * (rule.doubleCost ? 40 : 20);
}

// In sheet order: skills it has (below the top), each specialization, and a "learn a new one" line for
// Language, Martial Arts...
export function improvementGroups(rules: SkillRule[], report: Line[]): ImproveGroup[] {
  const byCategory = new Map<string, { sort: string; options: ImproveOption[] }[]>();
  for (const rule of rules) {
    const name = skillLabel({ name: rule.name, namePt: rule.namePt, specialization: null });
    const options: ImproveOption[] = [];
    if (rule.specialized) {
      const learned = report.filter((l) => l.skillId === rule.id && l.specialization && l.ipToRaise != null)
        .sort((a, b) => skillLabel(a).localeCompare(skillLabel(b)));
      for (const l of learned) options.push({ value: l.key, skillId: rule.id, label: skillLabel(l), level: l.level, cost: l.ipToRaise!, isNew: false });
      options.push({ value: `${NEW_SPEC}${rule.id}`, skillId: rule.id, label: t(`${name} — nova especialização…`, `${name} — new specialization…`), level: 0, cost: skillCost(rule, 1), isNew: true });
    } else {
      const l = report.find((x) => x.key === rule.id);
      if (l && l.ipToRaise == null) continue;
      options.push({ value: rule.id, skillId: rule.id, label: name, level: l?.level ?? 0, cost: l?.ipToRaise ?? skillCost(rule, 1), isNew: false });
    }
    const list = byCategory.get(rule.category) ?? [];
    list.push({ sort: name, options });
    byCategory.set(rule.category, list);
  }
  const order = SKILL_CATEGORIES.map((c) => c.key);
  return [...byCategory.entries()]
    .sort(([a], [b]) => (order.indexOf(a) + 1 || 99) - (order.indexOf(b) + 1 || 99))
    .map(([category, skills]) => ({
      category,
      label: SKILL_CATEGORIES.find((c) => c.key === category)?.label ?? category,
      options: skills.sort((a, b) => a.sort.localeCompare(b.sort)).flatMap((s) => s.options),
    }));
}

/** Suggestions for skills the book leaves open (examples, not a closed list). */
const SCIENCES = ['Biology', 'Chemistry', 'Cybernetics', 'Genetics', 'Mathematics', 'Medicine', 'Neuroscience', 'Physics', 'Robotics'];
const INSTRUMENTS = ['Bass', 'Drums', 'Guitar', 'Keyboard', 'Saxophone', 'Synthesizer', 'Turntables', 'Violin', 'Vocals'];

// Martial Arts only take the book's forms; languages, places, sciences and instruments suggest a list but accept
// anything. Already learned ones are left out.
export function specializationChoices(skillId: string, learnedKeys: string[],
  sources: { languages: string[]; homes: string[]; martialArts: string[] }
): { options: string[]; free: boolean } {
  const learned = new Set(learnedKeys.filter((k) => k.startsWith(`${skillId}:`)).map((k) => k.slice(skillId.length + 1).toLowerCase()));
  const pick = (list: string[]) => [...new Set(list)].filter((o) => !learned.has(o.toLowerCase())).sort((a, b) => a.localeCompare(b));
  switch (skillId) {
    case 'martial-arts': return { options: pick(sources.martialArts), free: false };
    case 'language': return { options: pick(sources.languages), free: true };
    case 'local-expert': return { options: pick(sources.homes), free: true };
    case 'science': return { options: pick(SCIENCES), free: true };
    case 'play-instrument': return { options: pick(INSTRUMENTS), free: true };
    default: return { options: [], free: true };
  }
}
