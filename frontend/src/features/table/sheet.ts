import type { PlayerSheetSkill } from '../../api/tableTypes';
import { SKILL_CATEGORIES } from '../characters/characterLabels';

export interface SkillGroup { key: string; label: string; skills: PlayerSheetSkill[] }

/** Trained skills in the sheet's category order (unknown categories last), by name inside each. */
export function groupSkills(skills: PlayerSheetSkill[]): SkillGroup[] {
  const order = (key: string) => { const i = SKILL_CATEGORIES.findIndex((c) => c.key === key); return i < 0 ? SKILL_CATEGORIES.length : i; };
  const byKey = new Map<string, PlayerSheetSkill[]>();
  for (const s of skills) byKey.set(s.category, [...(byKey.get(s.category) ?? []), s]);
  return [...byKey.entries()]
    .sort(([a], [b]) => order(a) - order(b) || a.localeCompare(b))
    .map(([key, list]) => ({
      key,
      label: SKILL_CATEGORIES.find((c) => c.key === key)?.label ?? key,
      skills: [...list].sort((a, b) => a.name.localeCompare(b.name)),
    }));
}
