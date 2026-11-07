import type { WoundState } from '../../api/encounterTypes';
import type { PartyMember, TokenView } from '../../api/tableTypes';

/** What the fight tells about a token's fighter, as this viewer may see it (players get no NPC numbers). */
export interface StripFighter {
  woundState: WoundState;
  dead: boolean;
  hp?: number | null;
  maxHp?: number | null;
  spBody?: number | null;
  maxSpBody?: number | null;
  spHead?: number | null;
  maxSpHead?: number | null;
}

/** One card of the "on the scene" strip under the map. */
export interface StripEntry {
  tokenId: string;
  /** The fighter the token stands for in the linked fight, when it is linked. */
  combatantId: string | null;
  /** A PC's sheet. */
  characterId: number | null;
  name: string;
  color: string;
  imageUrl: string | null;
  woundState: WoundState | null;
  dead: boolean;
  hp: number | null;
  maxHp: number | null;
  spHead: number | null;
  maxSpHead: number | null;
  spBody: number | null;
  maxSpBody: number | null;
  /** The player of a PC is connected now (null: not a PC). */
  online: boolean | null;
  /** A PC of the table with no token on this scene: a faded card, never a target. */
  offScene: boolean;
}

/** A fighter of the queue, to put the cards in Initiative order. */
export interface QueueSlot { id: string; characterId: number | null }

/**
 * The cards under the map: every character on the scene (from its sheet) and every fighter of the linked fight
 * (from the fight), one card per character; props without either stay out.
 */
export function buildStrip(tokens: TokenView[], party: PartyMember[], fighters: ReadonlyMap<string, StripFighter>,
  order?: readonly QueueSlot[]): StripEntry[] {
  const out: StripEntry[] = [];
  const seen = new Set<number>();
  for (const tk of tokens) {
    const base = { tokenId: tk.id, color: tk.color, imageUrl: tk.imageUrl, combatantId: tk.combatantId ?? null, characterId: tk.characterId ?? null, offScene: false };
    const pc = tk.characterId != null ? party.find((p) => p.characterId === tk.characterId) : undefined;
    if (pc) {
      if (seen.has(pc.characterId)) continue;
      seen.add(pc.characterId);
      const f = tk.combatantId ? fighters.get(tk.combatantId) : undefined;
      out.push({ ...base, name: pc.name, woundState: pc.woundState, dead: f?.dead ?? false, hp: pc.hp, maxHp: pc.maxHp, spHead: pc.spHead, maxSpHead: pc.maxSpHead, spBody: pc.spBody, maxSpBody: pc.maxSpBody, online: pc.online });
      continue;
    }
    const f = tk.combatantId ? fighters.get(tk.combatantId) : undefined;
    if (!f) continue;
    out.push({
      ...base, name: tk.label, woundState: f.woundState, dead: f.dead, hp: f.hp ?? null, maxHp: f.maxHp ?? null,
      spHead: f.spHead ?? null, maxSpHead: f.maxSpHead ?? null, spBody: f.spBody ?? null, maxSpBody: f.maxSpBody ?? null, online: null,
    });
  }
  const sorted = order ? sortByQueue(out, order) : out;
  // The table's other PCs, with no token here: faded cards at the end.
  for (const pc of party) {
    if (seen.has(pc.characterId)) continue;
    seen.add(pc.characterId);
    sorted.push({
      tokenId: `seat:${pc.seatId}`, combatantId: null, characterId: pc.characterId, name: pc.name, color: '#5c6370', imageUrl: pc.imageUrl,
      woundState: pc.woundState, dead: false, hp: pc.hp, maxHp: pc.maxHp, spHead: pc.spHead, maxSpHead: pc.maxSpHead,
      spBody: pc.spBody, maxSpBody: pc.maxSpBody, online: pc.online, offScene: true,
    });
  }
  return sorted;
}

/** Fighters in Initiative order (a PC's card matches its fighter by sheet), everyone else after, in token order. */
function sortByQueue(entries: StripEntry[], order: readonly QueueSlot[]): StripEntry[] {
  const rank = (e: StripEntry) => {
    const at = order.findIndex((q) => (e.combatantId != null && q.id === e.combatantId) || (e.characterId != null && q.characterId === e.characterId));
    return at < 0 ? Number.MAX_SAFE_INTEGER : at;
  };
  return entries.map((e, i) => ({ e, i, r: rank(e) })).sort((a, b) => a.r - b.r || a.i - b.i).map((x) => x.e);
}
