import { useQuery } from '@tanstack/react-query';
import { request } from './client';

export type NpcSource = { book: string; page: number };

export type NpcSummary = {
  id: string;
  name: string;
  aliases: string[];
  kind: 'Sheet' | 'Profile';
  role: string | null;
  roleRank: number | null;
  rep: number | null;
  affiliation: string | null;
  sources: NpcSource[];
  summary: string;
};

export type NpcWeapon = {
  name: string;
  skill: string;
  attackBase: number;
  damage: string;
  rof: number;
  magazine: number | null;
  autofireMultiplier: number | null;
  /** Eight bands (0-6 to 401-800); null where the book prints a dash. */
  rangeDv: (number | null)[] | null;
  class: string | null;
  melee: boolean;
  notes: string | null;
};

export type NpcArmor = { location: string; name: string; sp: number; spVsMelee: number | null };

export type NpcSpecial = { name: string; text: string };

export type NpcSheet = {
  hp: number;
  seriouslyWounded: number | null;
  deathSave: number | null;
  stats: Record<string, number | null>;
  armoredStats: Record<string, number>;
  weapons: NpcWeapon[];
  armor: NpcArmor[];
  skills: Record<string, number>;
  armoredSkills: Record<string, number>;
  gear: string[];
  cyberware: string[];
  specials: NpcSpecial[];
};

export type NpcDetail = NpcSummary & {
  sheetNote: string | null;
  sheet: NpcSheet | null;
};

export const npcCompendiumApi = {
  list: (signal?: AbortSignal) => request<NpcSummary[]>('/npc-compendium', { signal }),
  get: (id: string, signal?: AbortSignal) => request<NpcDetail>(`/npc-compendium/${encodeURIComponent(id)}`, { signal }),
};

export const npcCompendiumKeys = {
  list: ['npc-compendium'] as const,
  detail: (id: string) => ['npc-compendium', id] as const,
};

export const useNpcCompendium = () =>
  useQuery({ queryKey: npcCompendiumKeys.list, queryFn: ({ signal }) => npcCompendiumApi.list(signal), staleTime: Infinity });

export const useCompendiumNpc = (id: string) =>
  useQuery({ queryKey: npcCompendiumKeys.detail(id), queryFn: ({ signal }) => npcCompendiumApi.get(id, signal), enabled: !!id, staleTime: Infinity });
