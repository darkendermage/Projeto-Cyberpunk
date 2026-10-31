import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { ArrowLeft, Skull, Swords, UserRound } from 'lucide-react';
import { useCompendiumNpc } from '../../api/npcCompendium';
import { Button, ErrorState, Spinner } from '../../components/ui/ui';
import page from '../../styles/Page.module.css';
import { SheetPanel, StateTag } from '../characters/CharacterUi';
import styles from '../characters/Characters.module.css';
import dossier from './NpcDossier.module.css';
import { t } from '../../i18n';
import { AddToCombatDialog } from './AddToCombatDialog';
import { NpcArmor, NpcCyberware, NpcGear, NpcHealth, NpcSkills, NpcSpecials, NpcStats, NpcWeapons } from './NpcSheetPanels';

export function NpcDossierPage() {
  const { id = '' } = useParams();
  const query = useCompendiumNpc(id);
  const [adding, setAdding] = useState(false);
  const npc = query.data;
  const back = <Link to="/npcs" className={page.link}><ArrowLeft size={14} /> {t('Voltar ao compêndio', 'Back to the compendium')}</Link>;

  if (query.isLoading) return <div className={page.page}><Spinner /></div>;
  if (query.error || !npc) {
    return <div className={page.page}>{back}<ErrorState error={query.error} onRetry={() => query.refetch()} /></div>;
  }

  const sheet = npc.sheet;
  return (
    <div className={page.page}>
      {back}
      <header className={dossier.header}>
        <div className={styles.avatar}>{sheet ? <Skull size={34} /> : <UserRound size={34} />}</div>
        <div className={styles.cardBody}>
          <h1 className="hud-title">{npc.name}</h1>
          {npc.aliases.length > 0 && <div className={`${styles.small} ${styles.muted}`}>{npc.aliases.join(' · ')}</div>}
          <div className={styles.cardMeta}>
            <span>{npc.role ?? '—'}</span>
            {npc.roleRank != null && <span>Rank {npc.roleRank}</span>}
            {npc.rep != null && <span className={styles.muted}>Rep {npc.rep}</span>}
          </div>
          {npc.affiliation && <div className={styles.cardMeta}><span className={styles.muted}>{npc.affiliation}</span></div>}
          <div className={styles.source}>{npc.sources.map((s) => `${s.book} p.${s.page}`).join(' · ')}</div>
          {npc.kind === 'Sheet' && (
            <div className={styles.row}>
              <Button variant="primary" onClick={() => setAdding(true)}><Swords size={14} /> {t('Adicionar ao combate', 'Add to combat')}</Button>
            </div>
          )}
        </div>
      </header>

      <SheetPanel title={t('Biografia', 'Biography')}><p>{npc.summary}</p></SheetPanel>

      {npc.sheetNote && <p className={`${styles.small} ${styles.muted}`}>{npc.sheetNote}</p>}

      {!sheet && (
        <div className={styles.row}>
          <StateTag tone="warn">{t('sem ficha oficial', 'no official sheet')}</StateTag>
          <span>{t('O livro não traz ficha deste personagem.', 'The book has no stat block for this character.')}</span>
        </div>
      )}

      {sheet && (
        <div className={dossier.sheet}>
          <NpcStats sheet={sheet} />
          <div className={`${styles.column} ${dossier.panels}`}>
            <NpcHealth sheet={sheet} />
            <NpcWeapons weapons={sheet.weapons} />
            <NpcArmor armor={sheet.armor} />
            <NpcSkills skills={sheet.skills} />
            <NpcGear gear={sheet.gear} />
            <NpcCyberware cyberware={sheet.cyberware} />
            <NpcSpecials specials={sheet.specials} />
          </div>
        </div>
      )}
      {npc.kind === 'Sheet' && <AddToCombatDialog npc={npc} open={adding} onClose={() => setAdding(false)} />}
    </div>
  );
}
