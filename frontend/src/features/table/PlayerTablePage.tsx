import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { useNavigate, useParams } from 'react-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { problemMessage } from '../../api/characters';
import { playerApi, tableKeys } from '../../api/table';
import type { PlayerEncounter, PlayerState, RollKind, SceneView, TableMessage, TableVisibility } from '../../api/tableTypes';
import { Button, EmptyState, IconButton, Spinner } from '../../components/ui/ui';
import { Dices, Volume2, VolumeX } from 'lucide-react';
import { useToast } from '../../state/ToastContext';
import { lang, t } from '../../i18n';
import type { BoardCallbacks, BoardData, BoardTool, TemplateDraft } from './board/BoardController';
import { hasSavedDraft, readDraft, saveDraft } from './templates';
import { BoardToolbar } from './board/BoardToolbar';
import { TOOL_HINT } from './board/toolHints';
import { TableBoard, type BoardHandle } from './board/TableBoard';
import { ChatPanel } from './chat/ChatPanel';
import { CharacterPanel, InitiativePanel } from './PlayerPanels';
import { PlayerSheetPanel } from './PlayerSheetPanel';
import { fromPlayerFighter } from './tokens';
import { forgetSeat, isRefusedKey, rememberSeat, savedSeat } from './lobby';
import { LangSwitch } from './LangSwitch';
import { PanelHandle } from './PanelHandle';
import { RulesDialog } from './RulesDialog';
import { AimBar, SceneStrip } from './SceneStrip';
import { buildStrip } from './strip';
import { attackSummary, fighterIdOf, startAim } from './aim';
import { useQuickAttack } from './useQuickAttack';
import { useAttackFx } from './useAttackFx';
import { useTablePrefs } from './tablePrefs';
import { DiceTray, type DiceTrayHandle } from './dice3d/DiceTray';
import { throwOf } from './dice3d/dice';
import { NO_SELECTION, useBoardKeys, type Selection } from './useBoardKeys';
import { PLAYER_PANELS, playerColumns } from './panels';
import { usePanels } from './usePanels';
import { HUB_TEXT, useTableHub } from './useTableHub';
import styles from './Table.module.css';

type Tab = 'character' | 'sheet' | 'initiative' | 'chat';

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
  const [savedDraft, setDraftState] = useState<TemplateDraft>(readDraft);
  // A player's areas start in the seat's color; once they pick another, theirs wins.
  const [ownColor, setOwnColor] = useState(hasSavedDraft);
  const setDraft = (next: TemplateDraft) => {
    if (next.color !== draft.color) setOwnColor(true);
    setDraftState(next);
    saveDraft(next);
  };
  const [selection, setSelection] = useState<Selection>(NO_SELECTION);
  const [tablesOpen, setTablesOpen] = useState(false);

  const navigate = useNavigate();
  const [panels, setPanels] = usePanels('ncdb.table.player-panels', PLAYER_PANELS);
  const state = useQuery({ queryKey: tableKeys.player(token), queryFn: api.state, retry: false });
  const denied = isRefusedKey(state.error);
  // The general link enters this seat again directly; a key that stopped working (released, revoked) is dropped.
  useEffect(() => { if (state.isSuccess) rememberSeat(token); }, [state.isSuccess, token]);
  useEffect(() => { if (denied && savedSeat() === token) forgetSeat(); }, [denied, token]);
  const encounter = useQuery({ queryKey: tableKeys.playerEncounter(token), queryFn: api.encounter, enabled: state.isSuccess });
  const character = useQuery({ queryKey: tableKeys.playerCharacter(token), queryFn: api.character, enabled: state.isSuccess });
  // The full sheet loads when its tab opens, and again each time it is opened.
  const sheet = useQuery({ queryKey: tableKeys.playerSheet(token), queryFn: api.sheet, enabled: state.isSuccess && tab === 'sheet' });
  const messages = useQuery({ queryKey: tableKeys.playerMessages(token), queryFn: api.messages, enabled: state.isSuccess });
  const party = useQuery({ queryKey: tableKeys.playerParty(token), queryFn: api.party, enabled: state.isSuccess });
  const [armorBusy, setArmorBusy] = useState(false);

  const setScene = (scene: SceneView | null) => {
    const before = client.getQueryData<PlayerState>(tableKeys.player(token));
    client.setQueryData<PlayerState>(tableKeys.player(token), (old) => (old ? { ...old, scene } : old));
    // Another scene, or another fight linked to it: the fight on screen changes too.
    if (before?.scene?.id !== scene?.id || before?.scene?.encounterId !== scene?.encounterId)
      void client.invalidateQueries({ queryKey: tableKeys.playerEncounter(token) });
    void client.invalidateQueries({ queryKey: tableKeys.playerParty(token) });
  };

  const [prefs, setPrefs] = useTablePrefs();
  const playAttack = useAttackFx(board, prefs);
  const dice = useRef<DiceTrayHandle>(null);
  // The hub's handlers outlive renders: they read the scene and the fight on screen through this.
  const live = useRef<{ scene: SceneView | null; queue: readonly { id: string; characterId: number | null }[] }>({ scene: null, queue: [] });

  const hub = useTableHub(token, {
    scene: setScene,
    encounter: (e) => {
      client.setQueryData(tableKeys.playerEncounter(token), e as PlayerEncounter | null);
      // A hit or a heal changes the sheet too, and the party's numbers.
      void client.invalidateQueries({ queryKey: tableKeys.playerCharacter(token) });
      void client.invalidateQueries({ queryKey: tableKeys.playerParty(token) });
      void client.invalidateQueries({ queryKey: tableKeys.playerSheet(token) });
    },
    message: (m) => {
      const known = client.getQueryData<TableMessage[]>(tableKeys.playerMessages(token))?.some((x) => x.id === m.id) ?? false;
      client.setQueryData<TableMessage[]>(tableKeys.playerMessages(token), (old = []) => (old.some((x) => x.id === m.id) ? old.map((x) => (x.id === m.id ? m : x)) : [...old, m].slice(-300)));
      if (known) return;
      if (m.kind === 'Attack' && m.attack) playAttack(m.attack, live.current.scene, live.current.queue);
      const thrown = prefs.dice3d ? throwOf(m) : null;
      if (thrown) dice.current?.roll(thrown);
    },
    tokenDrag: (d) => { if (d.sceneId === shownRef.current) board.current?.showDrag(d.tokenId, d.x, d.y); },
    ping: (p) => { if (p.sceneId === shownRef.current) board.current?.showPing(p); },
    resync: () => void client.invalidateQueries({ queryKey: tableKeys.player(token) }),
  }, state.isSuccess);

  const seat = state.data?.seat;
  const draft: TemplateDraft = ownColor || !seat ? savedDraft : { ...savedDraft, color: seat.color };
  const scene = state.data?.scene ?? null;
  useEffect(() => { shownRef.current = scene?.id ?? null; }, [scene?.id]);
  useEffect(() => { live.current = { scene, queue: encounter.data?.queue ?? [] }; });
  const queue = useMemo(() => encounter.data?.queue ?? [], [encounter.data]);
  const own = queue.find((f) => f.own) ?? null;
  const quick = useQuickAttack(
    (aim, targetId) => api.attack({ weapon: aim.weapon.key, mode: aim.mode, targetIds: [targetId] }),
    (attack) => toast.notify(attackSummary(attack)),
    (e) => toast.notify(problemMessage(e), 'error'));

  // While aiming, the board lights up the shooter and the target.
  const aim = quick.aim;
  const aimTokens = useMemo(() => (aim && scene ? scene.tokens.filter((tk) => {
    const id = fighterIdOf(tk, queue);
    return id != null && (id === aim.attackerId || id === aim.targetId);
  }).map((tk) => tk.id) : null), [aim, scene, queue]);
  const fighters = useMemo(() => new Map((encounter.data?.queue ?? []).map((f) => [f.id, fromPlayerFighter(f)])), [encounter.data]);
  const data: BoardData | null = useMemo(() => scene ? {
    scene,
    fighters,
    activeCombatantId: encounter.data?.activeId ?? null,
    selectedTokens: aimTokens ?? selection.tokens,
    selectedTemplates: aimTokens ? [] : selection.templates,
    // Any player's area (a grenade that went off) is anyone's to move or clear; the GM's stay the GM's.
    canMoveTemplate: (tpl) => !tpl.locked && tpl.ownerSeatId != null,
    gm: false,
    lang,
  } : null, [scene, fighters, encounter.data, selection, aimTokens]);

  const run = async (fn: () => Promise<SceneView | unknown>) => {
    try {
      const result = await fn();
      if (result && typeof result === 'object' && 'tokens' in result) setScene(result as SceneView);
    } catch (e) {
      toast.notify(problemMessage(e), 'error');
    }
  };

  const strip = useMemo(() => buildStrip(scene?.tokens ?? [], party.data ?? [], new Map((encounter.data?.queue ?? []).map((f) => [f.id, f]))),
    [scene?.tokens, party.data, encounter.data]);
  const myToken = scene?.tokens.find((tk) => tk.controllable && seat?.characterId != null && tk.characterId === seat.characterId)
    ?? scene?.tokens.find((tk) => tk.controllable);

  const callbacks: BoardCallbacks = {
    onSelect: (tokens, templates) => {
      // While aiming, a click on a token picks the target instead of selecting it.
      if (quick.aim) {
        const tk = tokens.length === 1 && templates.length === 0 ? scene?.tokens.find((x) => x.id === tokens[0]) : undefined;
        if (tk) quick.target(fighterIdOf(tk, queue));
        return;
      }
      setSelection({ tokens, templates });
    },
    onTokenDrag: (id, x, y) => { if (scene) hub.dragToken(scene.id, id, x, y); },
    onMoveDrop: (tokens, templates) => {
      if (tokens.length === 1 && templates.length === 0) void run(() => api.moveToken(tokens[0].id, { x: tokens[0].x, y: tokens[0].y }));
      else void run(() => api.batch({ moveTokens: tokens, moveTemplates: templates }));
    },
    onTemplatePlace: (x, y, angle) => void run(() => api.addTemplate({ shape: draft.shape, x, y, sizeMeters: draft.sizeMeters, angle, color: draft.color })),
    onPing: (x, y) => { if (scene) hub.ping(scene.id, x, y); },
    onTokenPlace: (x, y) => { setTool('select'); void run(() => api.placeToken(x, y)); },
  };

  // A player clears players' areas; tokens stay (only the GM removes them).
  const deletable = (scene?.templates ?? []).filter((tp) => selection.templates.includes(tp.id) && tp.ownerSeatId != null && !tp.locked).map((tp) => tp.id);
  const removeSelection = () => {
    if (deletable.length === 0) return;
    void run(async () => { const v = await api.batch({ removeTemplates: deletable }); setSelection(NO_SELECTION); return v; });
  };
  const selectAll = () => {
    if (!scene) return;
    setSelection({
      tokens: scene.tokens.filter((tk) => tk.controllable && !tk.locked).map((tk) => tk.id),
      templates: scene.templates.filter((tp) => tp.ownerSeatId != null && !tp.locked).map((tp) => tp.id),
    });
  };
  useBoardKeys({ onDelete: removeSelection, onSelectAll: selectAll, onClear: () => (quick.aim ? quick.cancel() : setSelection(NO_SELECTION)) });

  // A roll with LUCK spends it on the sheet: the panel's LUCK left follows.
  const refreshLuck = () => void client.invalidateQueries({ queryKey: tableKeys.playerCharacter(token) });
  const roll = (formula: string, kind: RollKind, label: string | null, visibility: TableVisibility, luck = 0) =>
    run(async () => { await api.roll({ formula, kind, label, visibility, luck }); if (luck) refreshLuck(); setTab('chat'); });

  if (state.isLoading) return <div className={styles.gate}><Spinner /></div>;
  if (state.error) {
    return (
      <div className={styles.gate}>
        <div className={styles.gateCard}>
          <span className={styles.brand}>Night City // {t('Mesa', 'Table')}</span>
          <h1>{denied ? t('Convite inválido', 'Invalid invite') : t('Sem sinal', 'No signal')}</h1>
          <p className={styles.muted}>
            {denied
              ? t('Este link não vale mais: o mestre liberou ou revogou o lugar. Escolha seu personagem de novo ou peça um link novo.', 'This link no longer works: the GM released or revoked the seat. Pick your character again or ask for a new link.')
              : t('Não deu para falar com a mesa do mestre. O túnel pode ter fechado ou trocado de endereço.', 'Couldn\'t reach the GM\'s table. The tunnel may have closed or changed address.')}
          </p>
          {denied
            ? <Button onClick={() => navigate('/mesa/j/', { replace: true })}>{t('Escolher personagem', 'Pick a character')}</Button>
            : <Button onClick={() => state.refetch()}>{t('Tentar de novo', 'Try again')}</Button>}
        </div>
      </div>
    );
  }

  // The seat's own token: its character's, or (without one) the token it owns. The server allows one per scene.
  const hasOwnToken = !!seat && !!scene?.tokens.some((tk) => (seat.characterId != null ? tk.characterId === seat.characterId : tk.ownerSeatId === seat.id));

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
          <LangSwitch />
        </span>
      </header>
      <div className={styles.playerBody} style={{ '--cols': playerColumns(panels) } as CSSProperties}>
        <section className={`${styles.stage} ${styles.colMain}`}>
          <BoardToolbar tool={tool} onTool={setTool} draft={draft} onDraft={setDraft} board={board} onSelectAll={scene ? selectAll : undefined} onTables={() => setTablesOpen(true)}>
            {scene && !hasOwnToken && (
              <Button variant={tool === 'token' ? 'primary' : 'ghost'} onClick={() => setTool(tool === 'token' ? 'select' : 'token')}>
                {tool === 'token' ? t('Cancelar', 'Cancel') : t('Pôr meu token', 'Place my token')}
              </Button>
            )}
            <IconButton label={prefs.dice3d ? t('Dados 3D: ligados', '3D dice: on') : t('Dados 3D: desligados', '3D dice: off')} active={prefs.dice3d}
              onClick={() => setPrefs({ dice3d: !prefs.dice3d })}><Dices size={17} /></IconButton>
            <IconButton label={prefs.sound ? t('Som da mesa: ligado', 'Table sound: on') : t('Som da mesa: desligado', 'Table sound: off')} active={prefs.sound}
              onClick={() => setPrefs({ sound: !prefs.sound })}>{prefs.sound ? <Volume2 size={17} /> : <VolumeX size={17} />}</IconButton>
            {deletable.length > 0 && <Button variant="ghost" onClick={removeSelection}>{deletable.length === 1 ? t('Apagar área', 'Delete area') : t(`Apagar ${deletable.length} áreas`, `Delete ${deletable.length} areas`)}</Button>}
          </BoardToolbar>
          <div className={styles.boardArea}>
            {scene && <DiceTray ref={dice} sound={prefs.sound} />}
            {scene ? <TableBoard ref={board} data={data} tool={tool} draft={draft} callbacks={callbacks} /> : (
              <EmptyState title={t('Aguardando o mestre', 'Waiting for the GM')}>{t('O mestre ainda não mostrou nenhuma cena.', 'The GM hasn\'t shown a scene yet.')}</EmptyState>
            )}
            {scene && <span className={styles.hint}>{TOOL_HINT[tool]}</span>}
          </div>
          {scene && quick.aim && (
            <AimBar aim={quick.aim} busy={quick.busy} targetName={queue.find((f) => f.id === quick.aim!.targetId)?.name ?? null}
              onMode={quick.mode} onFire={() => void quick.shoot()} onCancel={quick.cancel} />
          )}
          {scene && (
            <SceneStrip entries={strip} selected={selection.tokens} aim={quick.aim}
              onPick={(e) => { if (quick.aim) quick.target(fighterIdOf(e, queue)); else setSelection({ tokens: [e.tokenId], templates: [] }); }}
              weaponsOf={(e) => own && fighterIdOf(e, queue) === own.id
                ? { weapons: own.weapons ?? [], blocked: encounter.data?.activeId === own.id ? null : t('Aguarde seu turno para atacar.', 'Wait for your Turn to attack.') }
                : null}
              onWeapon={(_e, w) => { if (own) quick.start(startAim(own.id, own.name, w)); }} />
          )}
        </section>
        <PanelHandle className={styles.handleRight} panel="right" width={panels.right} limits={PLAYER_PANELS.right} open={panels.rightOpen}
          onResize={(right) => setPanels({ right })} onToggle={() => setPanels({ rightOpen: !panels.rightOpen })} />
        <aside className={`${styles.column} ${styles.colRight} ${panels.rightOpen ? '' : styles.collapsed}`}>
          <div className={styles.tabs} role="tablist">
            {([['character', t('Personagem', 'Character')], ['sheet', t('Ficha', 'Sheet')], ['initiative', t('Iniciativa', 'Initiative')], ['chat', 'Chat']] as [Tab, string][]).map(([id, label]) => (
              <Button key={id} role="tab" aria-selected={tab === id} variant={tab === id ? 'primary' : 'ghost'} onClick={() => setTab(id)}>{label}</Button>
            ))}
          </div>
          {tab === 'character' && (
            <CharacterPanel character={character.data ?? null} encounter={encounter.data ?? null} token={myToken} onRoll={roll} busy={armorBusy}
              onArmor={(location, delta) => {
                setArmorBusy(true);
                void run(async () => {
                  const updated = await api.armor(location, delta);
                  client.setQueryData(tableKeys.playerCharacter(token), updated);
                  void client.invalidateQueries({ queryKey: tableKeys.playerParty(token) });
                  void client.invalidateQueries({ queryKey: tableKeys.playerEncounter(token) });
                }).finally(() => setArmorBusy(false));
              }}
              onTurn={(delta) => { if (myToken) void run(() => api.moveToken(myToken.id, { x: myToken.x, y: myToken.y, rotation: (myToken.rotation + delta + 360) % 360 })); }} />
          )}
          {tab === 'sheet' && (sheet.isLoading ? <Spinner /> : <PlayerSheetPanel sheet={sheet.data ?? null} />)}
          {tab === 'initiative' && <InitiativePanel encounter={encounter.data ?? null} />}
          {tab === 'chat' && (
            <ChatPanel gm={false} messages={messages.data ?? []}
              onSend={(text, visibility) => api.send({ text, visibility })}
              onRoll={async (formula, kind, visibility, _target, luck) => { await api.roll({ formula, kind, visibility, luck }); if (luck) refreshLuck(); }}
              luckAvailable={character.data ? character.data.luckAvailable : undefined} mySeatId={seat?.id}
              onReroll={async (id) => { await api.reroll(id); refreshLuck(); }} />
          )}
        </aside>
      </div>
      <RulesDialog open={tablesOpen} onClose={() => setTablesOpen(false)} load={api.rules} />
    </div>
  );
}
