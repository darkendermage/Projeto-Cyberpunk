import type { SpecialMove } from '../../api/characterTypes';
import { t } from '../../i18n';

// Martial Arts forms and Special Moves live in the server rules (martial-arts-rules.json, served with the character
// rules); this file keeps only what the screens add on top.

export const MOVE_KIND_LABEL: Record<SpecialMove['kind'], string> = {
  attack: t('golpe na mesa', 'strike at the table'),
  defense: t('defesa na mesa', 'defense at the table'),
  check: t('teste', 'check'),
};

/** A Special Move's Resolution: 1d10 + DEX + the form (CP:R p.178). */
export const moveFormula = (total: number) => (total >= 0 ? `1d10+${total}` : `1d10${total}`);
