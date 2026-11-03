import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useLocation, useParams } from 'react-router';
import { ArrowLeft, Swords } from 'lucide-react';
import { useCharacterRules } from '../../api/characters';
import { useCompendiumNpc } from '../../api/npcCompendium';
import { npcImageApi, npcImageKeys } from '../../api/npcImages';
import { PortraitField } from '../../components/ui/PortraitField';
import { Button, ErrorState, Spinner } from '../../components/ui/ui';
import page from '../../styles/Page.module.css';
import { RoleAbilityButton, StateTag } from '../characters/CharacterUi';
import styles from '../characters/Characters.module.css';
import dossier from './NpcDossier.module.css';
import { t } from '../../i18n';
import { AddToCombatDialog } from './AddToCombatDialog';
import { NpcArmor, NpcBiography, NpcCyberware, NpcGear, NpcHealth, NpcIdentity, NpcSkills, NpcSpecials, NpcStats, NpcWeapons } from './NpcSheetPanels';

export function NpcDossierPage() {
  const { id = '' } = useParams();
  const query = useCompendiumNpc(id);
  const rules = useCharacterRules();
  const [adding, setAdding] = useState(false);
  const npc = query.data;
  // Opened from the list, the link carries its filters; opened directly, it goes to the whole list.
  const listSearch = (useLocation().state as { listSearch?: string } | null)?.listSearch ?? '';
  const back = <Link to={`/npcs${listSearch}`} className={styles.tab} aria-label={t('Voltar ao compêndio', 'Back to the compendium')}><ArrowLeft size={16} /></Link>;

  if (query.isLoading) return <div className={page.page}><Spinner /></div>;
  if (query.error || !npc) {
    return <div className={page.page}>{back}<ErrorState error={query.error} onRetry={() => query.refetch()} /></div>;
  }

  const sheet = npc.sheet;
  const ability = rules.data && npc.role && (
    <span className={styles.small}>{t('Habilidade de Role: ', 'Role Ability: ')}<RoleAbilityButton rules={rules.data} role={npc.role} /></span>
  );

  return (
    <div className={page.page}>
      <div className={styles.topbar}>
        {back}
        <div className={styles.topbarName}>
          <strong>{npc.name}{npc.aliases[0] && <span className={styles.muted}> “{npc.aliases[0]}”</span>}</strong>
          <span className={styles.source}>{[npc.role, npc.affiliation].filter(Boolean).join(' · ') || t('Compêndio de NPCs', 'NPC Compendium')}</span>
        </div>
        {sheet
          ? <StateTag tone="ok">{t('ficha do livro', 'book sheet')}</StateTag>
          : <StateTag tone="warn" title={t('O livro não traz ficha deste personagem.', 'The book has no stat block for this character.')}>{t('sem ficha oficial', 'no official sheet')}</StateTag>}
        {sheet && (
          <div className={styles.topbarRight}>
            <Button variant="primary" onClick={() => setAdding(true)}><Swords size={14} /> {t('Adicionar ao combate', 'Add to combat')}</Button>
          </div>
        )}
      </div>

      {sheet ? (
        <div className={styles.sheet}>
          <div className={styles.column}>
            <NpcIdentity npc={npc} role={ability} portrait={<DossierImage id={npc.id} name={npc.name} />} />
            <NpcBiography npc={npc} />
          </div>
          <NpcStats sheet={sheet} />
          <div className={styles.column}>
            <NpcSkills sheet={sheet} rules={rules.data?.skills ?? []} />
            <NpcSpecials specials={sheet.specials} />
          </div>
          <div className={styles.sheetRight}>
            <NpcHealth sheet={sheet} />
            <NpcArmor armor={sheet.armor} />
            <NpcWeapons weapons={sheet.weapons} />
            <NpcGear gear={sheet.gear} />
            <NpcCyberware cyberware={sheet.cyberware} />
          </div>
        </div>
      ) : (
        <div className={dossier.profile}>
          <div className={styles.column}><NpcIdentity npc={npc} role={ability} portrait={<DossierImage id={npc.id} name={npc.name} />} /></div>
          <NpcBiography npc={npc} />
        </div>
      )}
      {sheet && <AddToCombatDialog npc={npc} open={adding} onClose={() => setAdding(false)} />}
    </div>
  );
}

/** The GM's image of a book NPC: kept with the app, shown on the NPC's tokens of the Virtual Table. */
function DossierImage({ id, name }: { id: string; name: string }) {
  const client = useQueryClient();
  const images = useQuery({ queryKey: npcImageKeys.compendium, queryFn: npcImageApi.compendiumStamps });
  const stamp = images.data?.[id];
  const refresh = () => client.invalidateQueries({ queryKey: npcImageKeys.compendium });
  return (
    <PortraitField url={stamp ? npcImageApi.compendiumUrl(id, stamp) : null} alt={name}
      onUpload={async (file) => { await npcImageApi.uploadCompendium(id, file); await refresh(); }}
      onRemove={async () => { await npcImageApi.removeCompendium(id); await refresh(); }} />
  );
}
