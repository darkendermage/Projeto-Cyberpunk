import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { problemMessage } from '../../api/characters';
import { encounterKeys, useEncounter, useEncounterCommand } from '../../api/encounters';
import type { EncounterDetail, InjuryLocation } from '../../api/encounterTypes';
import { tableApi, tableKeys } from '../../api/table';
import type { SceneGrid, SceneView, TableMessage } from '../../api/tableTypes';
import { Button, EmptyState, ErrorState, Spinner } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import { lang, t } from '../../i18n';
import type { BoardCallbacks, BoardData, BoardTool, TemplateDraft } from './board/BoardController';
import { BoardToolbar } from './board/BoardToolbar';
import { TOOL_HINT } from './board/toolHints';
import { TableBoard, type BoardHandle } from './board/TableBoard';
import { ChatPanel } from './chat/ChatPanel';
import { CombatTab, InvitesTab, SceneTab, ScenesColumn, TemplateInspector, TokenInspector, type Act } from './GmPanels';
import { damageCommand, fromGmFighter } from './tokens';
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
  const command = useEncounterCommand(scene.data?.encounterId ?? 0);

  const [tab, setTab] = useState<Tab>('scene');
  const [tool, setTool] = useState<BoardTool>('select');
  const [draft, setDraft] = useState<TemplateDraft>({ shape: 'Circle', sizeMeters: 4, color: '#ff2a6d' });
  // The grid being calibrated, shown on the board before it is saved; it belongs to one scene version.
  const [gridDraft, setGridDraft] = useState<{ key: string; grid: SceneGrid } | null>(null);
  const [selectedToken, setSelectedToken] = useState<string | null>(null);
  const [selectedTemplate, setSelectedTemplate] = useState<string | null>(null);

  const putScene = useCallback((view: SceneView) => {
    client.setQueryData(tableKeys.scene(view.id), view);
    void client.invalidateQueries({ queryKey: tableKeys.scenes, exact: true });
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
    encounter: (detail) => { if (detail) client.setQueryData(encounterKeys.detail(detail.id), detail as EncounterDetail); },
    message: (m) => client.setQueryData<TableMessage[]>(tableKeys.messages, (old = []) => (old.some((x) => x.id === m.id) ? old : [...old, m].slice(-300))),
    // Live effects belong to the live scene: ignored while the GM edits another one.
    tokenDrag: (d) => { if (d.sceneId === shownRef.current) board.current?.showDrag(d.tokenId, d.x, d.y); },
    ping: (p) => { if (p.sceneId === shownRef.current) board.current?.showPing(p); },
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
    selectedTokenId: selectedToken,
    selectedTemplateId: selectedTemplate,
    canMoveTemplate: (tpl) => !tpl.locked,
    gm: true,
    lang,
  } : null, [view, draftGrid, fighters, encounter.data, selectedToken, selectedTemplate]);

  const callbacks: BoardCallbacks = {
    onTokenSelect: (id) => { setSelectedToken(id); if (id) { setSelectedTemplate(null); setTab('token'); } },
    // Previews only make sense on the scene the players are looking at.
    onTokenDrag: (id, x, y) => { if (live && view) hub.dragToken(view.id, id, x, y); },
    onTokenDrop: (id, x, y) => { if (view) void act(() => tableApi.moveToken(view.id, id, { x, y })); },
    onTemplateSelect: (id) => { setSelectedTemplate(id); if (id) { setSelectedToken(null); setTab('token'); } },
    onTemplateDrop: (id, x, y) => {
      const tpl = view?.templates.find((tp) => tp.id === id);
      if (view && tpl) void act(() => tableApi.updateTemplate(view.id, id, { ...tpl, x, y }));
    },
    onTemplatePlace: (x, y, angle) => {
      if (view) void act(() => tableApi.addTemplate(view.id, { shape: draft.shape, x, y, sizeMeters: draft.sizeMeters, angle, color: draft.color }));
    },
    onPing: (x, y) => {
      if (live && view) hub.ping(view.id, x, y);
      else board.current?.showPing({ sceneId: view?.id ?? 0, x, y, color: '#f5e663', label: 'GM' });
    },
  };

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
    <div className={styles.workspace}>
      <ScenesColumn scenes={scenes.data ?? []} currentId={sceneId} onSelect={(id) => { setChosen(id); setSelectedToken(null); setSelectedTemplate(null); }} act={act} />

      <section className={styles.stage}>
        <BoardToolbar tool={tool} onTool={setTool} draft={draft} onDraft={setDraft} board={board}>
          <span className={styles.status} title={live ? t('Os jogadores veem esta cena', 'The players see this scene') : t('Os jogadores não veem esta cena', 'The players don\'t see this scene')}>
            <span className={`${styles.dot} ${hub.status === 'online' ? styles.dotOn : hub.status === 'offline' ? styles.dotOff : styles.dotWarn}`} />
            {HUB_TEXT[hub.status]}{view ? ` · ${live ? t('ao vivo', 'live') : t('preparando', 'preparing')}` : ''}
          </span>
          {hub.status === 'offline' && <Button variant="ghost" onClick={hub.reconnect}>{t('Reconectar', 'Reconnect')}</Button>}
          {view && !live && <Button variant="ghost" onClick={() => act(() => tableApi.activateScene(view.id), t('Cena ao vivo.', 'Scene is live.'))}>{t('Mostrar aos jogadores', 'Show to the players')}</Button>}
        </BoardToolbar>
        {scenes.isLoading ? <Spinner /> : !view ? (
          <EmptyState title={t('Nenhuma cena', 'No scene')}>{t('Crie uma cena à esquerda para começar a montar o mapa.', 'Create a scene on the left to start building the map.')}</EmptyState>
        ) : (
          <TableBoard ref={board} data={data} tool={tool} draft={draft} callbacks={callbacks} />
        )}
        {view && <span className={styles.hint}>{TOOL_HINT[tool]}</span>}
      </section>

      <aside className={styles.column}>
        <div className={styles.tabs} role="tablist">
          {TABS.map((tb) => <Button key={tb.id} role="tab" aria-selected={tab === tb.id} variant={tab === tb.id ? 'primary' : 'ghost'} onClick={() => setTab(tb.id)}>{tb.label}</Button>)}
        </div>
        {tab === 'scene' && (view ? <SceneTab key={viewKey} scene={view} act={act} onGridDraft={(grid) => setGridDraft(grid ? { key: viewKey, grid } : null)} /> : <span className={styles.muted}>{t('Escolha uma cena.', 'Pick a scene.')}</span>)}
        {tab === 'token' && view && (
          token ? <TokenInspector key={token.id} scene={view} token={token} seats={seats.data ?? []} encounter={encounter.data} act={act} onDone={() => setSelectedToken(null)} />
          : template ? <TemplateInspector key={template.id} template={template} editable
              onSave={(tpl) => act(() => tableApi.updateTemplate(view.id, tpl.id, tpl), t('Área salva.', 'Area saved.'))}
              onRemove={() => act(async () => { const v = await tableApi.removeTemplate(view.id, template.id); setSelectedTemplate(null); return v; })} />
          : <span className={styles.muted}>{t('Clique num token ou numa área do mapa para editá-lo.', 'Click a token or an area on the map to edit it.')}</span>
        )}
        {tab === 'combat' && view && <CombatTab scene={view} encounter={encounter.data} onSelectToken={(id) => { setSelectedToken(id); setTab('token'); }} />}
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
    </div>
  );
}

/** Under a damage roll: send it to a fighter of the linked fight through the combat table's own damage action. */
function ApplyDamage({ total, encounter, onApply }: { total: number; encounter: EncounterDetail; onApply: (combatantId: string, location: InjuryLocation) => void }) {
  const [target, setTarget] = useState('');
  const [location, setLocation] = useState<InjuryLocation>('Body');
  return (
    <div className={styles.row}>
      <select aria-label={t('Alvo do dano', 'Damage target')} value={target} onChange={(e) => setTarget(e.target.value)}>
        <option value="">{t('Aplicar em…', 'Apply to…')}</option>
        {encounter.queue.filter((f) => !f.dead).map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
      </select>
      <select aria-label={t('Local', 'Location')} value={location} onChange={(e) => setLocation(e.target.value as InjuryLocation)}>
        <option value="Body">{t('Corpo', 'Body')}</option>
        <option value="Head">{t('Cabeça', 'Head')}</option>
      </select>
      <Button disabled={!target} onClick={() => onApply(target, location)}>{t(`Aplicar ${total}`, `Apply ${total}`)}</Button>
    </div>
  );
}
