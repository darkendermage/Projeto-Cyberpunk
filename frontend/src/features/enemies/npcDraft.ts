// Pure state handling for the enemy editor. No game rules here: budgets, limits and derived values all come
// from the server-side validator (POST /api/npcs/validate), shared by the automatic, semi-automatic and manual modes.
import type {
  CyberwareRuleState, NpcCyberwareEntry, NpcGearEntry, NpcGenerationRequest, NpcLevel, NpcSheet,
} from '../../api/types';

export type CreationMode = 'auto' | 'semi' | 'manual';

export const STAT_KEYS = ['INT', 'REF', 'DEX', 'TECH', 'COOL', 'WILL', 'MOVE', 'BODY', 'EMP'] as const;

export const LEVEL_LABEL: Record<NpcLevel, string> = {
  Mook: 'Mook',
  Lieutenant: 'Lieutenant',
  MiniBoss: 'Mini-Boss',
  Boss: 'Boss',
};

export function emptySheet(level: NpcLevel = 'Mook'): NpcSheet {
  return {
    name: '',
    concept: null,
    level,
    hardened: false,
    profileId: null,
    role: null,
    roleRank: 0,
    nativeLanguage: 'English',
    home: 'Watson',
    stats: Object.fromEntries(STAT_KEYS.map((k) => [k, 2])),
    luck: null,
    skills: {},
    keySkills: [],
    gear: [],
    cyberware: [],
    cyberwareRules: [],
    exceptions: [],
    locks: { stats: [], skills: [], identity: false },
    notes: null,
  };
}

export const skillKey = (id: string, specialization?: string | null) =>
  specialization && specialization.trim() ? `${id}:${specialization.trim()}` : id;

export type DraftAction =
  | { type: 'load'; sheet: NpcSheet }
  | { type: 'set'; patch: Partial<Pick<NpcSheet, 'name' | 'concept' | 'level' | 'hardened' | 'role' | 'roleRank' | 'nativeLanguage' | 'home' | 'luck' | 'notes' | 'profileId'>> }
  | { type: 'stat'; key: string; value: number }
  | { type: 'skill'; key: string; value: number | null }
  | { type: 'toggleKeySkill'; key: string }
  | { type: 'toggleStatLock'; key: string }
  | { type: 'toggleSkillLock'; key: string }
  | { type: 'toggleIdentityLock' }
  | { type: 'addGear'; itemId: number }
  | { type: 'updateGear'; index: number; patch: Partial<NpcGearEntry> }
  | { type: 'removeGear'; index: number }
  | { type: 'setCyberware'; entries: NpcCyberwareEntry[] }
  | { type: 'updateCyber'; instanceId: string; patch: Partial<NpcCyberwareEntry> }
  | { type: 'removeCyber'; instanceId: string }
  | { type: 'cyberRule'; itemId: number; state: CyberwareRuleState }
  | { type: 'addException'; key: string; justification: string }
  | { type: 'removeException'; key: string };

const toggle = (list: string[], value: string) =>
  list.some((v) => v.toLowerCase() === value.toLowerCase())
    ? list.filter((v) => v.toLowerCase() !== value.toLowerCase())
    : [...list, value];

const toInt = (value: number, min = 0, max = 20) => Math.min(max, Math.max(min, Math.round(Number.isFinite(value) ? value : 0)));

export function draftReducer(sheet: NpcSheet, action: DraftAction): NpcSheet {
  switch (action.type) {
    case 'load':
      return action.sheet;
    case 'set':
      return { ...sheet, ...action.patch };
    case 'stat':
      return { ...sheet, stats: { ...sheet.stats, [action.key]: toInt(action.value) } };
    case 'skill': {
      const skills = { ...sheet.skills };
      if (action.value === null) delete skills[action.key];
      else skills[action.key] = toInt(action.value);
      return { ...sheet, skills };
    }
    case 'toggleKeySkill':
      return { ...sheet, keySkills: toggle(sheet.keySkills, action.key) };
    case 'toggleStatLock':
      return { ...sheet, locks: { ...sheet.locks, stats: toggle(sheet.locks.stats, action.key) } };
    case 'toggleSkillLock':
      return { ...sheet, locks: { ...sheet.locks, skills: toggle(sheet.locks.skills, action.key) } };
    case 'toggleIdentityLock':
      return { ...sheet, locks: { ...sheet.locks, identity: !sheet.locks.identity } };
    case 'addGear':
      return { ...sheet, gear: [...sheet.gear, { itemId: action.itemId, quantity: 1 }] };
    case 'updateGear':
      return { ...sheet, gear: sheet.gear.map((g, i) => (i === action.index ? { ...g, ...action.patch } : g)) };
    case 'removeGear':
      return { ...sheet, gear: sheet.gear.filter((_, i) => i !== action.index) };
    case 'setCyberware':
      return { ...sheet, cyberware: action.entries };
    case 'updateCyber':
      return { ...sheet, cyberware: sheet.cyberware.map((c) => (c.instanceId === action.instanceId ? { ...c, ...action.patch } : c)) };
    case 'removeCyber': {
      const gone = new Set([action.instanceId, ...descendants(sheet.cyberware, action.instanceId)]);
      return { ...sheet, cyberware: sheet.cyberware.filter((c) => !gone.has(c.instanceId)) };
    }
    case 'cyberRule': {
      const rest = sheet.cyberwareRules.filter((r) => r.itemId !== action.itemId);
      return { ...sheet, cyberwareRules: action.state === 'Allowed' ? rest : [...rest, { itemId: action.itemId, state: action.state }] };
    }
    case 'addException':
      return {
        ...sheet,
        exceptions: [...sheet.exceptions.filter((e) => e.key !== action.key), { key: action.key, justification: action.justification.trim() }],
      };
    case 'removeException':
      return { ...sheet, exceptions: sheet.exceptions.filter((e) => e.key !== action.key) };
  }
}

/** Every implant installed (directly or indirectly) in the given foundational piece. */
export function descendants(entries: NpcCyberwareEntry[], instanceId: string): string[] {
  const children = entries.filter((e) => e.parentInstanceId === instanceId).map((e) => e.instanceId);
  return children.flatMap((c) => [c, ...descendants(entries, c)]);
}

/** Implants ordered as a tree: each foundational piece followed by its options. */
export function cyberwareTree(entries: NpcCyberwareEntry[]): { entry: NpcCyberwareEntry; depth: number }[] {
  const ids = new Set(entries.map((e) => e.instanceId));
  const walk = (parent: string | null, depth: number): { entry: NpcCyberwareEntry; depth: number }[] =>
    entries
      .filter((e) => (parent === null ? !e.parentInstanceId || !ids.has(e.parentInstanceId) : e.parentInstanceId === parent))
      .flatMap((e) => [{ entry: e, depth }, ...walk(e.instanceId, depth + 1)]);
  return walk(null, 0);
}

export interface GeneratorSettings {
  profileId: string;
  seed: string;
  applyEmpGuideline: boolean;
}

/**
 * Automatic mode sends only the level and constraints; semi-automatic mode sends the sheet so the server
 * keeps the name, concept and every locked value. Manual mode never calls the generator.
 */
export function buildGenerationRequest(mode: CreationMode, sheet: NpcSheet, settings: GeneratorSettings): NpcGenerationRequest {
  const seed = settings.seed.trim() === '' ? null : Number.parseInt(settings.seed, 10);
  return {
    level: sheet.level,
    hardened: sheet.hardened,
    profileId: settings.profileId || null,
    seed: seed !== null && Number.isFinite(seed) ? seed : null,
    base: mode === 'semi' ? sheet : null,
    cyberwareRules: sheet.cyberwareRules,
    applyEmpGuideline: settings.applyEmpGuideline,
  };
}

/** After a generation, the editor keeps the user's constraints even in automatic mode. */
export function mergeGenerated(generated: NpcSheet, current: NpcSheet, mode: CreationMode): NpcSheet {
  return {
    ...generated,
    cyberwareRules: current.cyberwareRules,
    exceptions: mode === 'semi' ? generated.exceptions : [],
    locks: mode === 'semi' ? current.locks : { stats: [], skills: [], identity: false },
  };
}

export const cyberRuleState = (sheet: NpcSheet, itemId: number): CyberwareRuleState =>
  sheet.cyberwareRules.find((r) => r.itemId === itemId)?.state ?? 'Allowed';
