import type { TableAttack, TokenView } from '../../../api/tableTypes';
import { t } from '../../../i18n';
import type { Point } from '../grid';

/** How an attack looks on the map: a gun's tracer, a burst of autofire or shells, a blade's arc, an explosion. */
export type EffectKind = 'shot' | 'burst' | 'melee' | 'blast';

/** One line of an effect: where it lands, whether it hit, and the word or number that floats up there. */
export interface EffectShot { to: Point; hit: boolean; label: string }

/** An attack drawn on the map, in cells (token centers). */
export interface AttackEffect { kind: EffectKind; from: Point; shots: EffectShot[] }

/** The fighter a token stands for: the token linked to the combatant, or a PC's token linked to the sheet. */
export function tokenOfFighter(fighterId: string, tokens: readonly TokenView[], queue: readonly { id: string; characterId: number | null }[]): TokenView | null {
  const linked = tokens.find((tk) => tk.combatantId === fighterId);
  if (linked) return linked;
  const characterId = queue.find((f) => f.id === fighterId)?.characterId;
  return characterId != null ? tokens.find((tk) => tk.characterId === characterId) ?? null : null;
}

const center = (tk: TokenView): Point => ({ x: tk.x + tk.width / 2, y: tk.y + tk.height / 2 });

export function effectKind(a: Pick<TableAttack, 'melee' | 'mode'>): EffectKind {
  if (a.mode === 'Explosive') return 'blast';
  if (a.melee) return 'melee';
  if (a.mode === 'Autofire' || a.mode === 'ShotgunShell' || a.mode === 'Suppressive') return 'burst';
  return 'shot';
}

// Miss, or HP lost when the reader can see it (players only get hit/miss for NPCs, same as the chat card).
export function shotLabel(target: TableAttack['targets'][number]): string {
  if (!target.hit) return t('ERROU', 'MISS');
  if (target.damage == null) return t('ACERTO', 'HIT');
  const hp = target.through ?? 0;
  return hp > 0 ? `−${hp}` : t('ARMADURA SEGUROU', 'ARMOR HELD');
}

// null when the shooter isn't on the map (hidden NPC, no token yet). Targets without a token are skipped.
export function planEffect(a: TableAttack, tokens: readonly TokenView[], queue: readonly { id: string; characterId: number | null }[]): AttackEffect | null {
  const shooter = tokenOfFighter(a.attackerId, tokens, queue);
  if (!shooter) return null;
  const shots = a.targets.flatMap((target) => {
    const tk = tokenOfFighter(target.id, tokens, queue);
    return tk ? [{ to: center(tk), hit: target.hit, label: shotLabel(target) }] : [];
  });
  return shots.length ? { kind: effectKind(a), from: center(shooter), shots } : null;
}

/** Where a missed shot ends: past the target, a cell off to the side, so it visibly flies by. */
export function missPoint(from: Point, to: Point): Point {
  const dx = to.x - from.x, dy = to.y - from.y;
  const length = Math.hypot(dx, dy) || 1;
  const ux = dx / length, uy = dy / length;
  return { x: to.x + ux * 1.5 - uy * 0.6, y: to.y + uy * 1.5 + ux * 0.6 };
}
