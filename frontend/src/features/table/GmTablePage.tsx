import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { problemMessage } from '../../api/characters';
import { encounterKeys, useEncounter, useEncounterCommand } from '../../api/encounters';
import type { EncounterDetail, InjuryLocation } from '../../api/encounterTypes';
import { tableApi, tableKeys } from '../../api/table';
import type { SceneGrid, SceneView, TableMessage } from '../../api/tableTypes';
import { Trash2 } from 'lucide-react';
import { Button, EmptyState, ErrorState, IconButton, Select, Spinner } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import { lang, t } from '../../i18n';
import type { BoardCallbacks, BoardData, BoardTool, TemplateDraft } from './board/BoardController';
import { readDraft, saveDraft } from './templates';
import { BoardToolbar } from './board/BoardToolbar';
import { TOOL_HINT } from './board/toolHints';
import { TableBoard, type BoardHandle } from './board/TableBoard';
import { ChatPanel } from './chat/ChatPanel';
import { CombatTab, InvitesTab, SceneTab, ScenesColumn, TemplateInspector, TokenInspector, type Act } from './GmPanels';
import { damageCommand, fromGmFighter } from './tokens';
import { PanelHandle } from './PanelHandle';
import { RulesDialog } from './RulesDialog';
import { SceneStrip } from './SceneStrip';
import { buildStrip } from './strip';
import { GM_PANELS, gmColumns } from './panels';
import { usePanels } from './usePanels';
import { NO_SELECTION, useBoardKeys, type Selection } from './useBoardKeys';
import { HUB_TEXT, useTableHub } from './useTableHub';
import styles from './Table.module.css';

type Tab = 'scene' | 'token' | 'combat' | 'invites' | 'chat';

const TABS: { id: Tab; label: string }[] = [
  { id: 'scene', label: t('Cena', 'Scene') },
  { id: 'token', label: t('Seleção', 'Selection') },
  { id: 'combat', label: t('Combate', 'Fight') },
  { id: 'invites', label: t('Convites', 'Invites') },
  { id: 'chat', label: 'Chat' },
];


/**
 * The GM's side of the virtual table (/mesa): prepare scenes, edit the one selected (live or not), run the linked
 * fight, invite players and talk to them. The scene on the players' screens is the one marked live.
 */
export function GmTablePage() {
  const client = useQueryClient();
  const toast = useToast();
  const board = useRef<BoardHandle>(null);
  const scenes = useQuery({ queryKey: tableKeys.scenes, queryFn: tableApi.scenes });
  const seats = useQuery({ queryKey: tableKeys.seats, queryFn: tableApi.seats });
  const messages = useQuery({ queryKey: tableKeys.messages, queryFn: tableApi.messages });
  const [chosen, setChosen] = useState<number | null>(null);
  const sceneId = (chosen != null && scenes.data?.some((s) => s.id === chosen) ? chosen : null)
    ?? scenes.data?.find((s) => s.active)?.id ?? scenes.data?.[0]?.id ?? null;
  const scene = useQuery({ queryKey: tableKeys.scene(sceneId ?? 0), queryFn: ({ signal }) => tableApi.scene(sceneId!, signal), enabled: sceneId != null });
  const encounter = useEncounter(scene.data?.encounterId ?? 0);
  const party = useQuery({ queryKey: tableKeys.party(sceneId ?? 0), queryFn: () => tableApi.party(sceneId!), enabled: sceneId != null });
  const command = useEncounterCommand(scene.data?.encounterId ?? 0);

  const [tab, setTab] = useState<Tab>('scene');
  const [panels, setPanels] = usePanels('ncdb.table.gm-panels', GM_PANELS);
  const [tool, setTool] = useState<BoardTool>('select');
  const [draft, setDraftState] = useState<TemplateDraft>(readDraft);
  const setDraft = (next: TemplateDraft) => { setDraftState(next); saveDraft(next); };
  // The grid being calibrated, shown on the board before it is saved; it belongs to one scene version.
  const [gridDraft, setGridDraft] = useState<{ key: string; grid: SceneGrid } | null>(null);
  const [selection, setSelection] = useState<Selection>(NO_SELECTION);
  const [tablesOpen, setTablesOpen] = useState(false);
  const selectedToken = selection.tokens.length === 1 && selection.templates.length === 0 ? selection.tokens[0] : null;
  const selectedTemplate = selection.templates.length === 1 && selection.tokens.length === 0 ? selection.templates[0] : null;
  const selectToken = (id: string | null) => setSelection(id ? { tokens: [id], templates: [] } : NO_SELECTION);

  const putScene = useCallback((view: SceneView) => {
    client.setQueryData(tableKeys.scene(view.id), view);
    void client.invalidateQueries({ queryKey: tableKeys.scenes, exact: true });
    void client.invalidateQueries({ queryKey: tableKeys.party(view.id) });
  }, [client]);

  const act: Act = useCallback(async (fn, done) => {
    try {
      const view = await fn();
      if (view) putScene(view);
      else {
        void client.invalidateQueries({ queryKey: tableKeys.scenes, exact: true });
        void client.invalidateQueries({ queryKey: tableKeys.seats });
      }
      if (done) toast.notify(done);
    } catch (e) {
      toast.notify(problemMessage(e), 'error');
    }
  }, [client, putScene, toast]);

  const hub = useTableHub(null, {
    scene: (view) => { if (view) putScene(view); },
    sceneRemoved: () => void client.invalidateQueries({ queryKey: tableKeys.scenes, exact: true }),
    encounter: (detail) => {
      if (detail) client.setQueryData(encounterKeys.detail(detail.id), detail as EncounterDetail);
      // Damage to a PC lands on their sheet: the strip's numbers follow.
      void client.invalidateQueries({ queryKey: ['table', 'party'] });
    },
    message: (m) => client.setQueryData<TableMessage[]>(tableKeys.messages, (old = []) => (old.some((x) => x.id === m.id) ? old : [...old, m].slice(-300))),
    // Live effects belong to the live scene: ignored while the GM edits another one.
    tokenDrag: (d) => { if (d.sceneId === shownRef.current) board.current?.showDrag(d.tokenId, d.x, d.y); },
    ping: (p) => { if (p.sceneId === shownRef.current) board.current?.showPing(p); },
    seats: () => {
      void client.invalidateQueries({ queryKey: tableKeys.seats });
      void client.invalidateQueries({ queryKey: tableKeys.online });
    },
    resync: () => void client.invalidateQueries({ queryKey: ['table'] }),
  });

  const view = scene.data;
  const shownRef = useRef<number | null>(null);
  useEffect(() => { shownRef.current = view?.id ?? null; }, [view?.id]);
  const viewKey = view ? `${view.id}:${view.version}` : '';
  const draftGrid = gridDraft?.key === viewKey ? gridDraft.grid : null;
  const live = view?.active ?? false;
  const fighters = useMemo(() => new Map((encounter.data?.queue ?? []).map((f) => [f.id, fromGmFighter(f)])), [encounter.data]);
  const data: BoardData | null = useMemo(() => view ? {
    scene: draftGrid ? { ...view, grid: draftGrid } : view,
    fighters,
    activeCombatantId: encounter.data?.status === 'Active' ? encounter.data.activeId : null,
    selectedTokens: selection.tokens,
    selectedTemplates: selection.templates,
    canMoveTemplate: (tpl) => !tpl.locked,
    gm: true,
    lang,
  } : null, [view, draftGrid, fighters, encounter.data, selection]);

  const callbacks: BoardCallbacks = {
    onSelect: (tokens, templates) => { setSelection({ tokens, templates }); if (tokens.length + templates.length > 0) setTab('token'); },
    // Previews only make sense on the scene the players are looking at.
    onTokenDrag: (id, x, y) => { if (live && view) hub.dragToken(view.id, id, x, y); },
    onMoveDrop: (tokens, templates) => {
      if (!view) return;
      if (tokens.length === 1 && templates.length === 0) void act(() => tableApi.moveToken(view.id, tokens[0].id, { x: tokens[0].x, y: tokens[0].y }));
      else void act(() => tableApi.batch(view.id, { moveTokens: tokens, moveTemplates: templates }));
    },
    onTemplatePlace: (x, y, angle) => {
      if (view) void act(() => tableApi.addTemplate(view.id, { shape: draft.shape, x, y, sizeMeters: draft.sizeMeters, angle, color: draft.color }));
    },
    onPing: (x, y) => {
      if (live && view) hub.ping(view.id, x, y);
      else board.current?.showPing({ sceneId: view?.id ?? 0, x, y, color: '#f5e663', label: 'GM' });
    },
  };

  /** Clears the selection (tokens after a confirmation: there is no undo). */
  const removeSelection = () => {
    if (!view || selection.tokens.length + selection.templates.length === 0) return;
    const n = selection.tokens.length;
    if (n > 0 && !window.confirm(t(n === 1 ? 'Remover o token selecionado?' : `Remover os ${n} tokens selecionados?`, n === 1 ? 'Remove the selected token?' : `Remove the ${n} selected tokens?`))) return;
    void act(async () => { const v = await tableApi.batch(view.id, { removeTokens: selection.tokens, removeTemplates: selection.templates }); setSelection(NO_SELECTION); return v; });
  };
  const selectAll = () => { if (view) setSelection({ tokens: view.tokens.map((tk) => tk.id), templates: view.templates.map((tp) => tp.id) }); };
  const setHidden = (hidden: boolean) => {
    if (view && selection.tokens.length) void act(() => tableApi.batch(view.id, hidden ? { hideTokens: selection.tokens } : { showTokens: selection.tokens }));
  };
  useBoardKeys({ onDelete: removeSelection, onSelectAll: selectAll, onClear: () => setSelection(NO_SELECTION) });
  const selectedCount = selection.tokens.length + selection.templates.length;

  const strip = useMemo(() => buildStrip(view?.tokens ?? [], party.data ?? [], new Map((encounter.data?.queue ?? []).map((f) => [f.id, f]))),
    [view?.tokens, party.data, encounter.data]);

  const token = view?.tokens.find((tk) => tk.id === selectedToken);
  const template = view?.templates.find((tp) => tp.id === selectedTemplate);
  const openSeats = (seats.data ?? []).filter((s) => !s.revoked);

  const applyDamage = (m: TableMessage) => {
    if (m.kind !== 'Roll' || m.roll?.kind !== 'Damage' || !encounter.data) return null;
    return <ApplyDamage total={m.roll.total} encounter={encounter.data}
      onApply={(id, location) => command.mutate(damageCommand(m.roll!, id, location),
        { onSuccess: () => toast.notify(t('Dano aplicado.', 'Damage applied.')), onError: (e) => toast.notify(problemMessage(e), 'error') })} />;
  };

  if (scenes.error) return <ErrorState error={scenes.error} onRetry={() => scenes.refetch()} />;

  return (
    <div className={styles.workspace} style={{ '--cols': gmColumns(panels) } as CSSProperties}>
      <ScenesColumn className={`${styles.colLeft} ${panels.leftOpen ? '' : styles.collapsed}`} scenes={scenes.data ?? []} currentId={sceneId}
        onSelect={(id) => { setChosen(id); setSelection(NO_SELECTION); }} act={act} />
      <PanelHandle className={styles.handleLeft} panel="left" width={panels.left} limits={GM_PANELS.left} open={panels.leftOpen}
        onResize={(left) => setPanels({ left })} onToggle={() => setPanels({ leftOpen: !panels.leftOpen })} />

      <section className={`${styles.stage} ${styles.colMain}`}>
        <BoardToolbar tool={tool} onTool={setTool} draft={draft} onDraft={setDraft} board={board} onSelectAll={view ? selectAll : undefined} onTables={() => setTablesOpen(true)}>
          {selectedCount > 0 && (
            <IconButton label={t(`Apagar seleção (${selectedCount}) — Delete`, `Delete selection (${selectedCount}) — Delete`)} onClick={removeSelection}><Trash2 size={17} /></IconButton>
          )}
          <span className={styles.status} title={live ? t('Os jogadores veem esta cena', 'The players see this scene') : t('Os jogadores não veem esta cena', 'The players don\'t see this scene')}>
            <span className={`${styles.dot} ${hub.status === 'online' ? styles.dotOn : hub.status === 'offline' ? styles.dotOff : styles.dotWarn}`} />
            {HUB_TEXT[hub.status]}{view ? ` · ${live ? t('ao vivo', 'live') : t('preparando', 'preparing')}` : ''}
          </span>
          {hub.status === 'offline' && <Button variant="ghost" onClick={hub.reconnect}>{t('Reconectar', 'Reconnect')}</Button>}
          {view && !live && <Button variant="ghost" onClick={() => act(() => tableApi.activateScene(view.id), t('Cena ao vivo.', 'Scene is live.'))}>{t('Mostrar aos jogadores', 'Show to the players')}</Button>}
        </BoardToolbar>
        <div className={styles.boardArea}>
          {scenes.isLoading ? <Spinner /> : !view ? (
            <EmptyState title={t('Nenhuma cena', 'No scene')}>{t('Crie uma cena à esquerda para começar a montar o mapa.', 'Create a scene on the left to start building the map.')}</EmptyState>
          ) : (
            <TableBoard ref={board} data={data} tool={tool} draft={draft} callbacks={callbacks} />
          )}
          {view && <span className={styles.hint}>{TOOL_HINT[tool]}</span>}
        </div>
        {view && <SceneStrip entries={strip} selected={selection.tokens} onPick={(id) => { selectToken(id); setTab('token'); }} />}
      </section>

      <PanelHandle className={styles.handleRight} panel="right" width={panels.right} limits={GM_PANELS.right} open={panels.rightOpen}
        onResize={(right) => setPanels({ right })} onToggle={() => setPanels({ rightOpen: !panels.rightOpen })} />
      <aside className={`${styles.column} ${styles.colRight} ${panels.rightOpen ? '' : styles.collapsed}`}>
        <div className={styles.tabs} role="tablist">
          {TABS.map((tb) => <Button key={tb.id} role="tab" aria-selected={tab === tb.id} variant={tab === tb.id ? 'primary' : 'ghost'} onClick={() => setTab(tb.id)}>{tb.label}</Button>)}
        </div>
        {tab === 'scene' && (view ? <SceneTab key={viewKey} scene={view} act={act} onGridDraft={(grid) => setGridDraft(grid ? { key: viewKey, grid } : null)} /> : <span className={styles.muted}>{t('Escolha uma cena.', 'Pick a scene.')}</span>)}
        {tab === 'token' && view && (
          token ? <TokenInspector key={token.id} scene={view} token={token} seats={seats.data ?? []} encounter={encounter.data} act={act} onDone={() => selectToken(null)} />
          : template ? <TemplateInspector key={template.id} template={template} editable
              onSave={(tpl) => act(() => tableApi.updateTemplate(view.id, tpl.id, tpl), t('Área salva.', 'Area saved.'))}
              onRemove={() => act(async () => { const v = await tableApi.removeTemplate(view.id, template.id); setSelection(NO_SELECTION); return v; })} />
          : selectedCount > 1 ? (
            <div className={styles.stack}>
              <span className={styles.title}>{t(`${selectedCount} selecionados`, `${selectedCount} selected`)}</span>
              <span className={styles.muted}>{t(`${selection.tokens.length} tokens e ${selection.templates.length} áreas. Arraste um deles no mapa para mover todos juntos.`, `${selection.tokens.length} tokens and ${selection.templates.length} areas. Drag one of them on the map to move them all.`)}</span>
              <div className={styles.row}>
                {selection.tokens.length > 0 && <Button onClick={() => setHidden(true)}>{t('Ocultar dos jogadores', 'Hide from players')}</Button>}
                {selection.tokens.length > 0 && <Button onClick={() => setHidden(false)}>{t('Mostrar a todos', 'Show to everyone')}</Button>}
                <Button variant="danger" icon={<Trash2 size={15} />} onClick={removeSelection}>{t('Apagar', 'Delete')}</Button>
              </div>
            </div>
          )
          : <span className={styles.muted}>{t('Clique num token ou numa área do mapa para editá-lo. Arraste no vazio para selecionar vários; Ctrl+A seleciona tudo.', 'Click a token or an area on the map to edit it. Drag on empty ground to select several; Ctrl+A selects all.')}</span>
        )}
        {tab === 'combat' && view && <CombatTab scene={view} encounter={encounter.data} act={act} onSelectToken={(id) => { selectToken(id); setTab('token'); }} />}
        {tab === 'invites' && <InvitesTab seats={seats.data ?? []} act={act} />}
        {tab === 'chat' && (
          <ChatPanel gm messages={messages.data ?? []} targets={openSeats.map((s) => ({ id: s.id, label: s.label }))}
            onSend={(text, visibility, targetSeatId) => tableApi.send({ text, visibility, targetSeatId })}
            onRoll={(formula, kind, visibility, targetSeatId) => tableApi.roll({ formula, kind, visibility, targetSeatId })}
            renderActions={applyDamage} />
        )}
        {tab === 'chat' && (messages.data?.length ?? 0) > 0 && (
          <Button variant="ghost" onClick={() => {
            if (window.confirm(t('Apagar o histórico do chat para começar uma sessão nova?', 'Clear the chat history to start a new session?')))
              void tableApi.clearMessages().then(() => client.setQueryData(tableKeys.messages, []), (e) => toast.notify(problemMessage(e), 'error'));
          }}>{t('Limpar histórico', 'Clear history')}</Button>
        )}
      </aside>
      <RulesDialog open={tablesOpen} onClose={() => setTablesOpen(false)} load={tableApi.rules} />
    </div>
  );
}

/** Under a damage roll: send it to a fighter of the linked fight through the combat table's own damage action. */
function ApplyDamage({ total, encounter, onApply }: { total: number; encounter: EncounterDetail; onApply: (combatantId: string, location: InjuryLocation) => void }) {
  const [target, setTarget] = useState('');
  const [location, setLocation] = useState<InjuryLocation>('Body');
  return (
    <div className={styles.row}>
      <Select aria-label={t('Alvo do dano', 'Damage target')} value={target} onChange={(e) => setTarget(e.target.value)}>
        <option value="">{t('Aplicar em…', 'Apply to…')}</option>
        {encounter.queue.filter((f) => !f.dead).map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
      </Select>
      <Select aria-label={t('Local', 'Location')} value={location} onChange={(e) => setLocation(e.target.value as InjuryLocation)}>
        <option value="Body">{t('Corpo', 'Body')}</option>
        <option value="Head">{t('Cabeça', 'Head')}</option>
      </Select>
      <Button disabled={!target} onClick={() => onApply(target, location)}>{t(`Aplicar ${total}`, `Apply ${total}`)}</Button>
    </div>
  );
}
