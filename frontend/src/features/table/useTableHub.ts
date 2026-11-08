import { useCallback, useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import type { EncounterDetail } from '../../api/encounterTypes';
import type { Ping, PlayerEncounter, SceneView, TableMessage, TableMusic, TokenDrag } from '../../api/tableTypes';
import { t } from '../../i18n';

export type HubStatus = 'connecting' | 'online' | 'reconnecting' | 'offline';

export const HUB_TEXT: Record<HubStatus, string> = {
  connecting: t('conectando…', 'connecting…'),
  online: t('conectado', 'connected'),
  reconnecting: t('reconectando…', 'reconnecting…'),
  offline: t('desconectado', 'offline'),
};

/** What the server pushes. The GM gets full views; a player gets their seat's view (null scene: none shown). */
export interface HubHandlers {
  scene?: (view: SceneView | null) => void;
  sceneRemoved?: (id: number) => void;
  encounter?: (detail: EncounterDetail | PlayerEncounter | null) => void;
  encounterRemoved?: (id: number) => void;
  message?: (message: TableMessage) => void;
  tokenDrag?: (drag: TokenDrag) => void;
  ping?: (ping: Ping) => void;
  /** GM only: seats changed hands (picked at the entrance, entered, left, released). */
  seats?: () => void;
  /** A character's sheet changed, or a player came or went (characterId null): refresh the party strip. */
  party?: (change: { characterId: number | null }) => void;
  /** The table's music changed (null: stopped). */
  music?: (music: TableMusic | null) => void;
  /** A new session started: the chat starts over. */
  chatReset?: () => void;
  /** Someone is moving, resizing or turning an image. */
  imageDrag?: (drag: { sceneId: number; imageId: string; x: number; y: number; width: number; height: number; rotation: number }) => void;
  /** After a reconnection: whatever was missed must be fetched again. */
  resync?: () => void;
}

/** Drag previews go out at most this often; the drop itself goes through the API. */
const DRAG_INTERVAL_MS = 50;

// No seat = GM (only the GM's port accepts that). Nothing connects while enabled is false (player link not
// checked yet). Handlers can change every render, the connection always uses the latest.
export function useTableHub(seat: string | null, handlers: HubHandlers, enabled = true) {
  const [status, setStatus] = useState<HubStatus>('connecting');
  const [attempt, setAttempt] = useState(0);
  const handlersRef = useRef(handlers);
  const connectionRef = useRef<HubConnection | null>(null);
  const lastDrag = useRef(0);

  useEffect(() => { handlersRef.current = handlers; });

  useEffect(() => {
    if (!enabled) return;
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/table', seat ? { accessTokenFactory: () => seat } : {})
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 15000, 30000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();
    connectionRef.current = connection;
    const on = <K extends keyof HubHandlers>(event: string, key: K) =>
      // Returns nothing on purpose: a value returned from a handler is taken by SignalR as a client result.
      connection.on(event, (payload: unknown) => { (handlersRef.current[key] as ((p: unknown) => void) | undefined)?.(payload); });
    on('scene', 'scene');
    on('sceneRemoved', 'sceneRemoved');
    on('encounter', 'encounter');
    on('encounterRemoved', 'encounterRemoved');
    on('message', 'message');
    on('tokenDrag', 'tokenDrag');
    on('ping', 'ping');
    on('seats', 'seats');
    on('party', 'party');
    on('music', 'music');
    on('chatReset', 'chatReset');
    on('imageDrag', 'imageDrag');
    // A connection closed by this effect's cleanup (StrictMode, a new seat) must not touch the next one's status.
    let disposed = false;
    connection.onreconnecting(() => { if (!disposed) setStatus('reconnecting'); });
    connection.onreconnected(() => { if (!disposed) { setStatus('online'); handlersRef.current.resync?.(); } });
    connection.onclose(() => { if (!disposed) setStatus('offline'); });

    connection.start()
      .then(() => { if (!disposed) setStatus('online'); })
      .catch(() => { if (!disposed) setStatus('offline'); });
    return () => {
      disposed = true;
      connectionRef.current = null;
      connection.stop().catch(() => { /* closing anyway */ });
    };
  }, [seat, attempt, enabled]);

  const send = useCallback((method: string, ...args: unknown[]) => {
    const connection = connectionRef.current;
    if (connection?.state === HubConnectionState.Connected) connection.invoke(method, ...args).catch(() => { /* fleeting */ });
  }, []);

  /** A token being dragged on a scene, in cells; throttled. Token ids repeat across scenes, hence the scene id. */
  const dragToken = useCallback((sceneId: number, tokenId: string, x: number, y: number) => {
    const now = performance.now();
    if (now - lastDrag.current < DRAG_INTERVAL_MS) return;
    lastDrag.current = now;
    send('DragToken', sceneId, tokenId, x, y);
  }, [send]);

  /** An image being dragged, throttled like the tokens. */
  const lastImage = useRef(0);
  const dragImage = useCallback((sceneId: number, imageId: string, r: { x: number; y: number; width: number; height: number; rotation: number }) => {
    const now = performance.now();
    if (now - lastImage.current < DRAG_INTERVAL_MS) return;
    lastImage.current = now;
    send('DragImage', sceneId, imageId, r.x, r.y, r.width, r.height, r.rotation);
  }, [send]);

  const ping = useCallback((sceneId: number, x: number, y: number) => send('Ping', sceneId, x, y), [send]);
  const reconnect = useCallback(() => { setStatus('connecting'); setAttempt((n) => n + 1); }, []);

  return { status, dragToken, dragImage, ping, reconnect };
}
