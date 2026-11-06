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
}

/**
 * The cards under the map: every character on the scene (from its sheet) and every fighter of the linked fight
 * (from the fight), one card per character; props without either stay out.
 */
export function buildStrip(tokens: TokenView[], party: PartyMember[], fighters: ReadonlyMap<string, StripFighter>): StripEntry[] {
  const out: StripEntry[] = [];
  const seen = new Set<number>();
  for (const tk of tokens) {
    const base = { tokenId: tk.id, color: tk.color, imageUrl: tk.imageUrl, combatantId: tk.combatantId ?? null, characterId: tk.characterId ?? null };
    const pc = tk.characterId != null ? party.find((p) => p.characterId === tk.characterId) : undefined;
    if (pc) {
      if (seen.has(pc.characterId)) continue;
      seen.add(pc.characterId);
      const f = tk.combatantId ? fighters.get(tk.combatantId) : undefined;
      out.push({ ...base, name: pc.name, woundState: pc.woundState, dead: f?.dead ?? false, hp: pc.hp, maxHp: pc.maxHp, spHead: pc.spHead, maxSpHead: pc.maxSpHead, spBody: pc.spBody, maxSpBody: pc.maxSpBody });
      continue;
    }
    const f = tk.combatantId ? fighters.get(tk.combatantId) : undefined;
    if (!f) continue;
    out.push({
      ...base, name: tk.label, woundState: f.woundState, dead: f.dead, hp: f.hp ?? null, maxHp: f.maxHp ?? null,
      spHead: f.spHead ?? null, maxSpHead: f.maxSpHead ?? null, spBody: f.spBody ?? null, maxSpBody: f.maxSpBody ?? null,
    });
  }
  return out;
}
