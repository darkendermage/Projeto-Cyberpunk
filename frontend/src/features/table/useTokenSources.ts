import { useMemo } from 'react';
import { useCharacters } from '../../api/characters';
import { useNpcs } from '../../api/hooks';
import { useNpcCompendium } from '../../api/npcCompendium';
import { tv } from '../../i18n';
import { buildSources, type TokenSource } from './sources';

/** Every character, saved NPC and compendium NPC a token can stand for. */
export function useTokenSources(): TokenSource[] {
  const characters = useCharacters();
  const npcs = useNpcs();
  const compendium = useNpcCompendium();
  return useMemo(() => buildSources(
    characters.data ?? [],
    (npcs.data ?? []).map((n) => ({ id: n.id, name: tv(n.name) })),
    compendium.data ?? [],
  ), [characters.data, npcs.data, compendium.data]);
}
