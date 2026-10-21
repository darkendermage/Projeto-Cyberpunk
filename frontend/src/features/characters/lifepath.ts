// Narrative Lifepath helpers. The tables come from the API (CP:R p.45–69); rolling on them is a narrative aid,
// so it happens here and the player can always pick or write something else (the book says "roll or choose").
import type { CharacterRules, CharacterSheet, LifepathQuestion, Relationship, RelationshipKind } from '../../api/characterTypes';

export type Rng = () => number;

/** Index rolled on a table: 1d6/1d10 pick a row; "choose" tables are never rolled. */
export function rollIndex(options: readonly string[], rng: Rng = Math.random): number {
  return Math.floor(rng() * options.length);
}

export function rollOption(options: readonly string[], rng: Rng = Math.random): string {
  return options[rollIndex(options, rng)];
}

/** Friends, enemies and tragic love affairs: 1d10 − 7, minimum 0 (CP:R p.51–52). */
export function rollRelationshipCount(rng: Rng = Math.random): number {
  const d10 = Math.floor(rng() * 10) + 1;
  return Math.max(0, d10 - 7);
}

/**
 * Questions of the Role flowchart that apply to the answers given so far (branches such as "group or solo").
 * A branch only opens when the question it depends on is itself visible, so a stale answer left in a closed branch
 * (e.g. "why did you leave?" after switching from solo to group) never reopens it.
 */
export function visibleQuestions(questions: LifepathQuestion[], answers: Record<string, string>): LifepathQuestion[] {
  const visible = new Set<string>();
  const byId = new Map(questions.map((q) => [q.id, q]));
  const isVisible = (q: LifepathQuestion, depth = 0): boolean => {
    if (!q.showIfQuestion) return true;
    const parent = byId.get(q.showIfQuestion);
    if (!parent || depth > questions.length) return false;
    return isVisible(parent, depth + 1) && answers[q.showIfQuestion] === q.showIfValue;
  };
  for (const q of questions) if (isVisible(q)) visible.add(q.id);
  return questions.filter((q) => visible.has(q.id));
}

export const RELATIONSHIP_SECTIONS: { kind: RelationshipKind; title: string; empty: string }[] = [
  { kind: 'Friend', title: 'Amigos', empty: 'Nenhum amigo registrado.' },
  { kind: 'Lover', title: 'Tragédias amorosas', empty: 'Nenhum romance trágico.' },
  { kind: 'Enemy', title: 'Inimigos', empty: 'Nenhum inimigo. Por enquanto.' },
  { kind: 'Ally', title: 'Aliados', empty: 'Nenhum aliado registrado.' },
  { kind: 'Contact', title: 'Contatos', empty: 'Nenhum contato registrado.' },
];

/** General Lifepath tables grouped as on the reference sheet. Style tables are kept in the style section. */
export const LIFEPATH_GROUPS: { title: string; tables: string[] }[] = [
  { title: 'Personalidade', tables: ['personality'] },
  { title: 'Motivações', tables: ['valueMost', 'feelAboutPeople', 'valuedPerson', 'valuedPossession'] },
  { title: 'Origem e família', tables: ['familyBackground', 'childhoodEnvironment', 'familyCrisis', 'lifeGoal'] },
];

/** Tables whose answers live in the style section of the sheet (clothing, hair, signature affectation). */
export const STYLE_TABLES = ['clothingStyle', 'hairstyle', 'affectation'] as const;

/** Temporary id for a relationship created on the client; the server replaces it when saving. */
export const tempId = () => `new-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;

/** A friend, lover or enemy with every table of its kind rolled (CP:R p.51–52). */
export function rollRelationship(kind: RelationshipKind, r: CharacterRules['relations'], rng: Rng = Math.random, id = tempId()): Relationship {
  switch (kind) {
    case 'Friend': return { id, kind, name: '', relation: rollOption(r.friendRelationship, rng) };
    case 'Lover': return { id, kind, name: '', outcome: rollOption(r.loveOutcome, rng) };
    case 'Enemy': return {
      id, kind, name: '', relation: rollOption(r.enemyWho, rng), cause: rollOption(r.enemyCause, rng),
      resources: rollOption(r.enemyResources, rng), intent: rollOption(r.enemyRevenge, rng),
    };
    default: return { id, kind, name: '' };
  }
}

/** Friends, lovers and enemies are rolled once; the count rolled (even zero) is kept so it isn't rolled again. */
export const RELATIONSHIP_ROLL_KEY = (kind: RelationshipKind) => `rolled:${kind}`;
const ROLLED_KINDS: RelationshipKind[] = ['Friend', 'Lover', 'Enemy'];

/**
 * Fills every empty Lifepath answer by rolling on its table, without touching what the player already chose:
 * Cultural Origin, the general tables, the style tables, friends/lovers/enemies (only when none of that kind exist
 * and they were never rolled) and the Role's flowchart, following its branches. "Choose" questions are left to the
 * player, and so are the branches that depend on them. Returns the new sheet and how many answers were rolled.
 */
export function rollMissing(
  sheet: CharacterSheet, rules: CharacterRules, roleId: string | null, rng: Rng = Math.random, newId: () => string = tempId,
): { sheet: CharacterSheet; filled: number } {
  let filled = 0;
  const answers = { ...sheet.lifepath.answers };
  let culturalRegion = sheet.lifepath.culturalRegion ?? null;
  if (!culturalRegion && rules.culturalOrigins.length > 0) {
    culturalRegion = rollOption(rules.culturalOrigins.map((o) => o.region), rng);
    filled++;
  }

  const rollTable = (id: string, current: string | null | undefined): string | null => {
    const t = rules.lifepathTables.find((x) => x.id === id);
    if (current || !t || t.dice === 'choose' || t.options.length === 0) return null;
    filled++;
    return rollOption(t.options, rng);
  };
  for (const id of LIFEPATH_GROUPS.flatMap((g) => g.tables)) {
    const rolled = rollTable(id, answers[id]);
    if (rolled !== null) answers[id] = rolled;
  }
  const style = { ...sheet.style };
  for (const id of STYLE_TABLES) {
    const rolled = rollTable(id, style[id]);
    if (rolled !== null) style[id] = rolled;
  }

  const relationships = [...sheet.lifepath.relationships];
  for (const kind of ROLLED_KINDS) {
    if (relationships.some((r) => r.kind === kind) || answers[RELATIONSHIP_ROLL_KEY(kind)] !== undefined) continue;
    const count = rollRelationshipCount(rng);
    answers[RELATIONSHIP_ROLL_KEY(kind)] = String(count);
    for (let i = 0; i < count; i++) relationships.push(rollRelationship(kind, rules.relations, rng, newId()));
    filled += count;
  }

  let roleLifepath = sheet.roleLifepath;
  const role = rules.roles.find((r) => r.id === roleId);
  if (role) {
    const roleAnswers = { ...(sheet.roleLifepath[role.id] ?? {}) };
    // Answering a question can open a branch, so keep going until nothing visible is left to roll.
    for (let pass = 0; pass <= role.lifepath.length; pass++) {
      const open = visibleQuestions(role.lifepath, roleAnswers).filter((q) => q.dice !== 'choose' && q.options.length > 0 && !roleAnswers[q.id]);
      if (open.length === 0) break;
      for (const q of open) roleAnswers[q.id] = rollOption(q.options, rng);
      filled += open.length;
    }
    roleLifepath = { ...sheet.roleLifepath, [role.id]: roleAnswers };
  }

  return {
    sheet: { ...sheet, lifepath: { ...sheet.lifepath, culturalRegion, answers, relationships }, style, roleLifepath },
    filled,
  };
}
