import { useCharacterRules } from '../../api/characters';
import type { SkillChipRule } from '../../api/characterTypes';

/** The server's Skill Chip rule when this item is one (CP:R p.358). */
export function useSkillChip(slug: string | null | undefined): SkillChipRule | null {
  const rules = useCharacterRules().data;
  const rule = rules?.cyberware.skillChip ?? null;
  return rule && slug === rule.slug ? rule : null;
}

/** Price of a chip for this skill key: (x2) skills cost more. */
export function useSkillChipPrice(rule: SkillChipRule | null, skillKey: string | null): number | null {
  const rules = useCharacterRules().data;
  if (!rule || !skillKey) return null;
  const skill = rules?.skills.find((s) => s.id === skillKey.split(':')[0]);
  return skill?.doubleCost ? rule.priceDoubleCost : rule.price;
}
