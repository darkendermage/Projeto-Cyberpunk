import { useEffect, useMemo, useRef, useState } from 'react';
import { useParams } from 'react-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../../api/client';
import { problemMessage } from '../../api/characters';
import { playerApi, tableKeys } from '../../api/table';
import type { PlayerEncounter, PlayerState, RollKind, SceneView, TableMessage, TableVisibility } from '../../api/tableTypes';
import { Button, EmptyState, Spinner } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import { lang, t } from '../../i18n';
import type { BoardCallbacks, BoardData, BoardTool, TemplateDraft } from './board/BoardController';
import { BoardToolbar } from './board/BoardToolbar';
import { TOOL_HINT } from './board/toolHints';
import { TableBoard, type BoardHandle } from './board/TableBoard';
import { ChatPanel } from './chat/ChatPanel';
import { CharacterPanel, InitiativePanel } from './PlayerPanels';
import { fromPlayerFighter } from './tokens';
import { HUB_TEXT, useTableHub } from './useTableHub';
import styles from './Table.module.css';

type Tab = 'character' | 'initiative' | 'chat';

/**
 * A player's screen (/mesa/j/:token), reached through the GM's tunnel. It lives outside the app shell: the players'
 * port only answers the table, so nothing here may call the catalog or the sheets' API.
 */
export function PlayerTablePage() {
  const { token = '' } = useParams();
  const api = useMemo(() => playerApi(token), [token]);
  const client = useQueryClient();
  const toast = useToast();
  const board = useRef<BoardHandle>(null);
  const shownRef = useRef<number | null>(null);
  const [tab, setTab] = useState<Tab>('character');
  const [tool, setTool] = useState<BoardTool>('select');
  const [draft, setDraft] = useState<TemplateDraft>({ shape: 'Circle', sizeMeters: 4, color: '#ff2a6d' });
  const [selectedToken, setSelectedToken] = useState<string | null>(null);
  const [selectedTemplate, setSelectedTemplate] = useState<string | null>(null);

  const state = useQuery({ queryKey: tableKeys.player(token), queryFn: api.state, retry: false });
  const encounter = useQuery({ queryKey: tableKeys.playerEncounter(token), queryFn: api.encounter, enabled: state.isSuccess });
  const character = useQuery({ queryKey: tableKeys.playerCharacter(token), queryFn: api.character, enabled: state.isSuccess });
  const messages = useQuery({ queryKey: tableKeys.playerMessages(token), queryFn: api.messages, enabled: state.isSuccess });

  const setScene = (scene: SceneView | null) => {
    const before = client.getQueryData<PlayerState>(tableKeys.player(token));
    client.setQueryData<PlayerState>(tableKeys.player(token), (old) => (old ? { ...old, scene } : old));
    // Another scene, or another fight linked to it: the fight on screen changes too.
    if (before?.scene?.id !== scene?.id || before?.scene?.encounterId !== scene?.encounterId)
      void client.invalidateQueries({ queryKey: tableKeys.playerEncounter(token) });
  };

  const hub = useTableHub(token, {
    scene: setScene,
    encounter: (e) => {
      client.setQueryData(tableKeys.playerEncounter(token), e as PlayerEncounter | null);
      // A hit or a heal changes the sheet too.
      void client.invalidateQueries({ queryKey: tableKeys.playerCharacter(token) });
    },
    message: (m) => client.setQueryData<TableMessage[]>(tableKeys.playerMessages(token), (old = []) => (old.some((x) => x.id === m.id) ? old : [...old, m].slice(-300))),
    tokenDrag: (d) => { if (d.sceneId === shownRef.current) board.current?.showDrag(d.tokenId, d.x, d.y); },
    ping: (p) => { if (p.sceneId === shownRef.current) board.current?.showPing(p); },
    resync: () => void client.invalidateQueries({ queryKey: tableKeys.player(token) }),
  }, state.isSuccess);

  const seat = state.data?.seat;
  const scene = state.data?.scene ?? null;
  useEffect(() => { shownRef.current = scene?.id ?? null; }, [scene?.id]);
  const fighters = useMemo(() => new Map((encounter.data?.queue ?? []).map((f) => [f.id, fromPlayerFighter(f)])), [encounter.data]);
  const data: BoardData | null = useMemo(() => scene ? {
    scene,
    fighters,
    activeCombatantId: encounter.data?.activeId ?? null,
    selectedTokenId: selectedToken,
    selectedTemplateId: selectedTemplate,
    canMoveTemplate: (tpl) => !tpl.locked && tpl.ownerSeatId === seat?.id,
    gm: false,
    lang,
  } : null, [scene, fighters, encounter.data, selectedToken, selectedTemplate, seat?.id]);

  const run = async (fn: () => Promise<SceneView | unknown>) => {
    try {
      const result = await fn();
      if (result && typeof result === 'object' && 'tokens' in result) setScene(result as SceneView);
    } catch (e) {
      toast.notify(problemMessage(e), 'error');
    }
  };

  const myToken = scene?.tokens.find((tk) => tk.controllable && seat?.characterId != null && tk.characterId === seat.characterId)
    ?? scene?.tokens.find((tk) => tk.controllable);

  const callbacks: BoardCallbacks = {
    onTokenSelect: setSelectedToken,
    onTokenDrag: (id, x, y) => { if (scene) hub.dragToken(scene.id, id, x, y); },
    onTokenDrop: (id, x, y) => void run(() => api.moveToken(id, { x, y })),
    onTemplateSelect: setSelectedTemplate,
    onTemplateDrop: (id, x, y) => {
      const tpl = scene?.templates.find((tp) => tp.id === id);
      if (tpl) void run(() => api.updateTemplate(id, { ...tpl, x, y }));
    },
    onTemplatePlace: (x, y, angle) => void run(() => api.addTemplate({ shape: draft.shape, x, y, sizeMeters: draft.sizeMeters, angle, color: seat?.color ?? draft.color })),
    onPing: (x, y) => { if (scene) hub.ping(scene.id, x, y); },
  };

  const roll = (formula: string, kind: RollKind, label: string | null, visibility: TableVisibility) =>
    run(async () => { await api.roll({ formula, kind, label, visibility }); setTab('chat'); });

  if (state.isLoading) return <div className={styles.gate}><Spinner /></div>;
  if (state.error) {
    const denied = state.error instanceof ApiError && state.error.status === 401;
    return (
      <div className={styles.gate}>
        <div className={styles.gateCard}>
          <span className={styles.brand}>Night City // {t('Mesa', 'Table')}</span>
          <h1>{denied ? t('Convite inválido', 'Invalid invite') : t('Sem sinal', 'No signal')}</h1>
          <p className={styles.muted}>
            {denied
              ? t('Este link foi revogado ou está incompleto. Peça um link novo ao mestre.', 'This link was revoked or is incomplete. Ask the GM for a new link.')
              : t('Não deu para falar com a mesa do mestre. O túnel pode ter fechado ou trocado de endereço.', 'Couldn\'t reach the GM\'s table. The tunnel may have closed or changed address.')}
          </p>
          {!denied && <Button onClick={() => state.refetch()}>{t('Tentar de novo', 'Try again')}</Button>}
        </div>
      </div>
    );
  }

  const selectedTpl = scene?.templates.find((tp) => tp.id === selectedTemplate && tp.ownerSeatId === seat?.id);

  return (
    <div className={styles.playerScreen}>
      <header className={styles.playerHeader}>
        <span className={styles.brand}>Night City // {scene?.name ?? t('Mesa', 'Table')}</span>
        <span className={styles.row}>
          {seat && <span className={styles.row}><span className={styles.swatch} style={{ background: seat.color }} /><strong>{seat.label}</strong></span>}
          <span className={styles.status}>
            <span className={`${styles.dot} ${hub.status === 'online' ? styles.dotOn : hub.status === 'offline' ? styles.dotOff : styles.dotWarn}`} />
            {HUB_TEXT[hub.status]}
          </span>
          {hub.status === 'offline' && <Button variant="ghost" onClick={hub.reconnect}>{t('Reconectar', 'Reconnect')}</Button>}
        </span>
      </header>
      <div className={styles.playerBody}>
        <section className={styles.stage}>
          <BoardToolbar tool={tool} onTool={setTool} draft={draft} onDraft={setDraft} board={board}>
            {selectedTpl && <Button variant="ghost" onClick={() => run(async () => { const v = await api.removeTemplate(selectedTpl.id); setSelectedTemplate(null); return v; })}>{t('Apagar minha área', 'Delete my area')}</Button>}
          </BoardToolbar>
          {scene ? <TableBoard ref={board} data={data} tool={tool} draft={draft} callbacks={callbacks} /> : (
            <EmptyState title={t('Aguardando o mestre', 'Waiting for the GM')}>{t('O mestre ainda não mostrou nenhuma cena.', 'The GM hasn\'t shown a scene yet.')}</EmptyState>
          )}
          {scene && <span className={styles.hint}>{TOOL_HINT[tool]}</span>}
        </section>
        <aside className={styles.column}>
          <div className={styles.tabs} role="tablist">
            {([['character', t('Personagem', 'Character')], ['initiative', t('Iniciativa', 'Initiative')], ['chat', 'Chat']] as [Tab, string][]).map(([id, label]) => (
              <Button key={id} role="tab" aria-selected={tab === id} variant={tab === id ? 'primary' : 'ghost'} onClick={() => setTab(id)}>{label}</Button>
            ))}
          </div>
          {tab === 'character' && (
            <CharacterPanel character={character.data ?? null} encounter={encounter.data ?? null} token={myToken} onRoll={roll}
              onTurn={(delta) => { if (myToken) void run(() => api.moveToken(myToken.id, { x: myToken.x, y: myToken.y, rotation: (myToken.rotation + delta + 360) % 360 })); }} />
          )}
          {tab === 'initiative' && <InitiativePanel encounter={encounter.data ?? null} />}
          {tab === 'chat' && (
            <ChatPanel gm={false} messages={messages.data ?? []}
              onSend={(text, visibility) => api.send({ text, visibility })}
              onRoll={(formula, kind, visibility) => api.roll({ formula, kind, visibility })} />
          )}
        </aside>
      </div>
    </div>
  );
}
