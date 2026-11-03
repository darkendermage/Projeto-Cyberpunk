import { useState } from 'react';
import { Swords } from 'lucide-react';
import { tableApi } from '../../api/table';
import type { SceneView } from '../../api/tableTypes';
import { Button, Checkbox, Segmented } from '../../components/ui/ui';
import { t } from '../../i18n';
import { fightEntries, fightRequest, type FightEntry, type FightEntryKind } from './fight';
import type { Act } from './GmPanels';
import styles from './Table.module.css';

const KIND_LABEL: Record<FightEntryKind, string> = {
  pc: 'PC',
  npc: 'NPC',
  compendium: t('Compêndio', 'Compendium'),
  generic: t('genérico', 'generic'),
};

/**
 * A fight started from the map: the scene's tokens, each ticked or not and on a side. The new fight is linked to the
 * scene and every ticked token to its fighter. Mounted per scene (key), so the list starts from the current tokens.
 */
export function NewFight({ scene, act }: { scene: SceneView; act: Act }) {
  const [name, setName] = useState('');
  const [entries, setEntries] = useState<FightEntry[]>(() => fightEntries(scene.tokens));
  const change = (id: string, patch: Partial<FightEntry>) => setEntries((list) => list.map((e) => (e.tokenId === id ? { ...e, ...patch } : e)));
  const chosen = entries.filter((e) => e.include).length;

  return (
    <form className={styles.stack} onSubmit={(e) => {
      e.preventDefault();
      void act(() => tableApi.startFight(scene.id, fightRequest(name, entries)), t('Combate criado e vinculado à cena.', 'Fight created and linked to the scene.'));
    }}>
      <span className={styles.label}>{t('Novo combate', 'New fight')}</span>
      <input aria-label={t('Nome do combate', 'Fight name')} value={name} maxLength={80} placeholder={t('Ex.: Tiroteio no Afterlife', 'E.g. Shootout at the Afterlife')} onChange={(e) => setName(e.target.value)} />
      {entries.length === 0
        ? <span className={styles.muted}>{t('Ponha tokens no mapa (aba Cena) ou vincule um combate já montado.', 'Put tokens on the map (Scene tab) or link a fight you already built.')}</span>
        : (
          <div className={styles.fightList}>
            {entries.map((e) => (
              <div key={e.tokenId} className={styles.fightRow}>
                <Checkbox checked={e.include} onChange={(include) => change(e.tokenId, { include })} label={<>{e.label} <small className={styles.source}>{KIND_LABEL[e.kind]}</small></>} />
                <Segmented size="sm" ariaLabel={t(`Lado de ${e.label}`, `${e.label}'s side`)} value={e.side} onChange={(side) => change(e.tokenId, { side })}
                  options={[{ value: 'Ally', label: t('Aliado', 'Ally') }, { value: 'Enemy', label: t('Inimigo', 'Enemy') }]} />
              </div>
            ))}
          </div>
        )}
      <span className={styles.muted}>{t('PCs e NPCs entram com a ficha. Um token sem ficha marcado entra como inimigo genérico, com o nome do token.', 'PCs and NPCs come in with their sheets. A ticked token without a sheet fights as a generic enemy named after the token.')}</span>
      <Button type="submit" variant="primary" icon={<Swords size={15} />} disabled={chosen === 0}>{t(`Começar combate (${chosen})`, `Start the fight (${chosen})`)}</Button>
    </form>
  );
}
