import type { TokenInput, TokenView } from '../../api/tableTypes';
import { t } from '../../i18n';

/** What a token can stand for: a player character, a saved NPC of the generator or a named NPC of the books. */
export type TokenSourceKind = 'character' | 'npc' | 'compendium';

export interface TokenSource { kind: TokenSourceKind; id: string; name: string }

export const SOURCE_LABEL: Record<TokenSourceKind, string> = {
  character: 'PC',
  npc: 'NPC',
  compendium: t('Compêndio', 'Compendium'),
};

export function buildSources(
  characters: { id: number; name: string; handle: string | null }[],
  npcs: { id: number; name: string }[],
  compendium: { id: string; name: string }[],
): TokenSource[] {
  return [
    ...characters.map((c): TokenSource => ({ kind: 'character', id: String(c.id), name: c.handle ?? c.name })),
    ...npcs.map((n): TokenSource => ({ kind: 'npc', id: String(n.id), name: n.name })),
    ...compendium.map((n): TokenSource => ({ kind: 'compendium', id: n.id, name: n.name })),
  ];
}

const fold = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

/** Sources whose name holds the query (case and accents ignored); names starting with it come first. */
export function matchSources(list: TokenSource[], query: string, limit = 20): TokenSource[] {
  const q = fold(query.trim());
  const scored = list
    .map((s) => ({ s, at: fold(s.name).indexOf(q) }))
    .filter((x) => x.at >= 0)
    .sort((a, b) => Number(a.at !== 0) - Number(b.at !== 0));
  return scored.slice(0, limit).map((x) => x.s);
}

/** The token fields for a source: exactly one link, the others cleared. */
export function sourceFields(src: TokenSource | null): Pick<TokenInput, 'characterId' | 'npcId' | 'compendiumNpcId'> {
  return {
    characterId: src?.kind === 'character' ? Number(src.id) : null,
    npcId: src?.kind === 'npc' ? Number(src.id) : null,
    compendiumNpcId: src?.kind === 'compendium' ? src.id : null,
  };
}

/** What a token stands for, among the known sources. */
export function sourceOf(token: { characterId?: number | null; npcId?: number | null; compendiumNpcId?: string | null }, list: TokenSource[]): TokenSource | null {
  const key = token.characterId != null ? `character:${token.characterId}`
    : token.npcId != null ? `npc:${token.npcId}`
      : token.compendiumNpcId ? `compendium:${token.compendiumNpcId}` : null;
  return key === null ? null : list.find((s) => `${s.kind}:${s.id}` === key) ?? null;
}

/** Everything the GM set on a token, as the update wants it back (position included). */
export function tokenInputOf(tk: TokenView): TokenInput {
  return {
    label: tk.label, x: tk.x, y: tk.y, width: tk.width, height: tk.height, rotation: tk.rotation, locked: tk.locked,
    imageMediaId: tk.imageMediaId, color: tk.color, ownerSeatId: tk.ownerSeatId, visibility: tk.visibility,
    visibleSeatIds: tk.visibleSeatIds, characterId: tk.characterId, combatantId: tk.combatantId, rangeRingMeters: tk.rangeRingMeters,
    npcId: tk.npcId, compendiumNpcId: tk.compendiumNpcId,
  };
}
