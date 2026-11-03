import { useEffect, useImperativeHandle, useRef, type Ref } from 'react';
import type { Ping } from '../../../api/tableTypes';
import { BoardController, type BoardCallbacks, type BoardData, type BoardTool, type TemplateDraft } from './BoardController';
import styles from '../Table.module.css';

/** The weights of the board's font (token names, ruler, areas) that canvas text needs loaded. */
const BOARD_FONTS = ['600 14px Rajdhani', '700 14px Rajdhani'];

export interface BoardHandle {
  showDrag: (tokenId: string, x: number, y: number) => void;
  showPing: (ping: Ping) => void;
  fit: () => void;
  zoomBy: (factor: number) => void;
}

/**
 * The PixiJS board inside React. The controller lives as long as the component; data, tool and draft flow in as
 * props, live effects (drags of others, pings) through the handle.
 */
export function TableBoard({ data, tool, draft, callbacks, ref }: {
  data: BoardData | null;
  tool: BoardTool;
  draft: TemplateDraft;
  callbacks: BoardCallbacks;
  ref?: Ref<BoardHandle>;
}) {
  const host = useRef<HTMLDivElement>(null);
  const controller = useRef<BoardController | null>(null);
  const callbacksRef = useRef(callbacks);
  const dataRef = useRef(data);

  useEffect(() => { callbacksRef.current = callbacks; dataRef.current = data; });

  useEffect(() => {
    const board = new BoardController(host.current!, {
      onTokenSelect: (id) => callbacksRef.current.onTokenSelect(id),
      onTokenDrag: (id, x, y) => callbacksRef.current.onTokenDrag(id, x, y),
      onTokenDrop: (id, x, y) => callbacksRef.current.onTokenDrop(id, x, y),
      onTemplateSelect: (id) => callbacksRef.current.onTemplateSelect(id),
      onTemplateDrop: (id, x, y) => callbacksRef.current.onTemplateDrop(id, x, y),
      onTemplatePlace: (x, y, angle) => callbacksRef.current.onTemplatePlace(x, y, angle),
      onPing: (x, y) => callbacksRef.current.onPing(x, y),
      onTokenPlace: (x, y) => callbacksRef.current.onTokenPlace?.(x, y),
    });
    controller.current = board;
    board.init().catch(() => { /* WebGL unavailable: the board stays empty */ });
    // Canvas text uses the app's font only once the browser has it; draw again when it arrives.
    let gone = false;
    void Promise.all(BOARD_FONTS.map((f) => document.fonts?.load(f))).then(() => { if (!gone && dataRef.current) board.setData(dataRef.current); }, () => {});
    return () => {
      gone = true;
      board.destroy();
      controller.current = null;
    };
  }, []);

  useEffect(() => { if (data) controller.current?.setData(data); }, [data]);
  useEffect(() => { controller.current?.setTool(tool); }, [tool]);
  useEffect(() => { controller.current?.setDraft(draft); }, [draft]);

  useImperativeHandle(ref, () => ({
    showDrag: (tokenId, x, y) => controller.current?.showDrag(tokenId, x, y),
    showPing: (ping) => controller.current?.showPing(ping),
    fit: () => controller.current?.fit(),
    zoomBy: (factor) => controller.current?.zoomBy(factor),
  }), []);

  return <div ref={host} className={styles.board} />;
}
