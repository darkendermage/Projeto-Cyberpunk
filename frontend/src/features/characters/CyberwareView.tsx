import { useState } from 'react';
import { problemMessage, useCharacterCommand } from '../../api/characters';
import type { CharacterDetail, CharacterRules, CyberLine } from '../../api/characterTypes';
import { useToast } from '../../state/ToastContext';
import { AnatomyFigure } from './AnatomyFigure';
import { firstFilledRegion, type RegionKey } from './anatomy';
import { CyberEffectsPanel } from './CharacterUi';
import type { Run } from './CombatPanels';
import { Clinic } from './CyberwareClinic';
import { DetailsPanel, type Selection } from './CyberwareDetails';
import { HistoryPanel, ImplantList, RegionList, RegionPieces, RemoveImplantDialog, StatusBar, StoredPanel, TherapyPanel } from './CyberwarePanels';
import type { SheetEditor } from './useSheetEditor';
import base from './Characters.module.css';
import styles from './Cyberware.module.css';
import { t, tv } from '../../i18n';

interface Props { detail: CharacterDetail; rules: CharacterRules; editor: SheetEditor }

// Four columns: the region list, the figure (or the implant list), the selected region with the item details,
// and the clinic with the stored cyberware. Effects, therapy and history below.
export function CyberwareView({ detail, rules, editor }: Props) {
  const toast = useToast();
  const command = useCharacterCommand(detail.id);
  const report = detail.report;
  const creating = !detail.sheet.creation.finalized;
  const busy = command.isPending;
  const regions = new Map(report.regions.map((r) => [r.key as RegionKey, r]));
  const lines = new Map(report.cyberware.map((c) => [c.instanceId, c]));

  const firstPiece = (key: RegionKey): Selection => {
    const id = regions.get(key)?.pieces[0];
    return id ? { kind: 'installed', instanceId: id } : null;
  };
  const [region, setRegion] = useState<RegionKey>(() => firstFilledRegion(report.regions));
  const [selection, setSelection] = useState<Selection>(() => firstPiece(region));
  const [view, setView] = useState<'figure' | 'list'>('figure');
  const [showAll, setShowAll] = useState(false);
  const [replacing, setReplacing] = useState<string | null>(null);
  const [removing, setRemoving] = useState<CyberLine | null>(null);

  const run: Run = async (path, body = {}, success) => {
    await editor.flush();
    try {
      const result = await command.mutateAsync({ path, body });
      result.notices.forEach((n) => toast.notify(n, 'info'));
      if (success) toast.notify(success);
      return result;
    } catch (error) {
      toast.notify(problemMessage(error), 'error');
      return null;
    }
  };

  // Picking a region shows its first implant, unless a clinic item is open (to choose where it goes).
  const pickRegion = (key: RegionKey) => {
    setRegion(key);
    setShowAll(false);
    if (selection?.kind !== 'catalog' && selection?.kind !== 'inventory') { setSelection(firstPiece(key)); setReplacing(null); }
  };
  const pickPiece = (id: string) => { setSelection({ kind: 'installed', instanceId: id }); setReplacing(null); };
  const startReplace = (id: string) => { setSelection({ kind: 'installed', instanceId: id }); setReplacing(id); setShowAll(false); };
  const done = () => { setSelection(null); setReplacing(null); };
  const current = regions.get(region);
  const replacingName = replacing ? lines.get(replacing)?.name ?? null : null;

  return (
    <div className={base.column}>
      <StatusBar detail={detail} />

      <div className={styles.layout}>
        <div className={styles.col}>
          <RegionList regions={regions} selected={region} onSelect={pickRegion} />
        </div>

        <div className={`${styles.col} ${styles.figureCol}`}>
          <div className={styles.figureWrap}>
            <div className={styles.viewToggle} role="group" aria-label={t('Visualização', 'View')}>
              <button type="button" aria-pressed={view === 'figure'} className={view === 'figure' ? styles.viewOn : undefined} onClick={() => setView('figure')}>{t('Esqueleto', 'Skeleton')}</button>
              <button type="button" aria-pressed={view === 'list'} className={view === 'list' ? styles.viewOn : undefined} onClick={() => setView('list')}>{t('Lista', 'List')}</button>
            </div>
            {view === 'figure'
              ? <AnatomyFigure regions={report.regions} selected={region} onSelect={pickRegion} fbc={report.fullBodyConversion} />
              : <ImplantList regions={regions} lines={lines} selection={selection} onSelect={(key, id) => { setRegion(key); pickPiece(id); }} />}
            <p className={styles.figureHint}>{t('Clique numa região do corpo para ver e gerenciar os implantes.', 'Click a body region to see and manage its implants.')}</p>
          </div>
        </div>

        <div className={styles.col}>
          {current && (
            <RegionPieces regionKey={region} region={current} lines={lines} selection={selection} creating={creating} busy={busy}
              onSelect={pickPiece} onReplace={startReplace} onRemove={setRemoving} />
          )}
          <DetailsPanel detail={detail} rules={rules} selection={selection} run={run} busy={busy} creating={creating}
            side={region.endsWith('-left') ? 'Left' : region.endsWith('-right') ? 'Right' : null}
            replacing={replacing} onReplace={(id) => { setReplacing(id); if (id) setShowAll(false); }} onRemove={setRemoving} onDone={done} />
        </div>

        <div className={styles.col}>
          <Clinic detail={detail} region={region} regionLabel={current ? tv(current.label) : null} showAll={showAll} onShowAll={setShowAll}
            replacingName={replacingName} selection={selection} onPick={(item) => setSelection({ kind: 'catalog', item })} />
          <StoredPanel detail={detail} onInstall={(instanceId, itemId, name) => setSelection({ kind: 'inventory', instanceId, itemId, name })} />
        </div>
      </div>

      <div className={styles.bottom}>
        <CyberEffectsPanel detail={detail} />
        <TherapyPanel detail={detail} rules={rules} run={run} busy={busy} />
        <HistoryPanel detail={detail} />
      </div>

      {removing && (
        <RemoveImplantDialog detail={detail} line={removing} run={run} busy={busy} onClose={() => setRemoving(null)}
          onDone={() => { setRemoving(null); done(); }} />
      )}
    </div>
  );
}
