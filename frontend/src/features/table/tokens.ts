import type { FighterView, InjuryLocation, WoundState } from '../../api/encounterTypes';
import type { PlayerFighter, RollResult } from '../../api/tableTypes';

/** What a token shows of its fighter; hp is known only to the GM and to the fighter's own player. */
export interface TokenFighter {
  woundState: WoundState;
  hp?: number | null;
  maxHp?: number | null;
  dead: boolean;
  conditions: string[];
  injuries: string[];
}

export const fromGmFighter = (f: FighterView): TokenFighter =>
  ({ woundState: f.woundState, hp: f.hp, maxHp: f.maxHp, dead: f.dead, conditions: f.conditions, injuries: f.injuries.map((i) => i.name) });

export const fromPlayerFighter = (f: PlayerFighter): TokenFighter =>
  ({ woundState: f.woundState, hp: f.hp, maxHp: f.maxHp, dead: f.dead, conditions: f.conditions, injuries: f.injuries });

/** Without numbers the bar shows the Wound State (CP:R p.186): roughly where in the HP the fighter is. */
const WOUND_RATIO: Record<WoundState, number> = { Unhurt: 1, Lightly: 0.7, Seriously: 0.4, Mortally: 0.12 };

export const WOUND_COLOR: Record<WoundState, number> = { Unhurt: 0x3dff9a, Lightly: 0x3de0ff, Seriously: 0xffcf3a, Mortally: 0xff2a3d };

/** The bar, and what goes around the token: conditions as short text, Critical Injuries as icons. */
export interface TokenStatus { ratio: number; exact: boolean; color: number; conditions: string[]; injuries: string[] }

export function tokenStatus(f: TokenFighter): TokenStatus {
  const exact = f.hp != null && f.maxHp != null && f.maxHp > 0;
  const ratio = f.dead ? 0 : exact ? Math.max(0, Math.min(1, f.hp! / f.maxHp!)) : WOUND_RATIO[f.woundState];
  // Conditions may carry a detail after a colon ("OnFire:fire-strong"); the badge shows the name only.
  return { ratio, exact, color: f.dead ? 0x535a66 : WOUND_COLOR[f.woundState], conditions: f.conditions.map((c) => c.split(':')[0]), injuries: [...f.injuries] };
}

/** The dice part of a weapon's damage ("3d6 (Shotgun Shell)" -> "3d6"); null when there is none to roll. */
export const damageFormula = (damage: string): string | null => damage.replace(/\s+/g, '').match(/^\d*d\d+([+-]\d+)?/i)?.[0] ?? null;

// Two 6s make it a crit there too (+5 to HP and an injury roll, CP:R p.187).
export const damageCommand = (roll: RollResult, combatantId: string, location: InjuryLocation) =>
  ({ path: `combatants/${combatantId}/damage`, body: { amount: roll.total, location, critical: roll.criticalInjury } });

export function initials(label: string): string {
  const words = label.replace(/[#]/g, ' ').trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return '?';
  return words.slice(0, 2).map((w) => w[0]!.toUpperCase()).join('');
}
