import { useEffect, useImperativeHandle, useRef, type Ref } from 'react';
import type { Ping } from '../../../api/tableTypes';
import { BoardController, type BoardCallbacks, type BoardData, type BoardTool, type TemplateDraft } from './BoardController';
import styles from '../Table.module.css';

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

  useEffect(() => { callbacksRef.current = callbacks; });

  useEffect(() => {
    const board = new BoardController(host.current!, {
      onTokenSelect: (id) => callbacksRef.current.onTokenSelect(id),
      onTokenDrag: (id, x, y) => callbacksRef.current.onTokenDrag(id, x, y),
      onTokenDrop: (id, x, y) => callbacksRef.current.onTokenDrop(id, x, y),
      onTemplateSelect: (id) => callbacksRef.current.onTemplateSelect(id),
      onTemplateDrop: (id, x, y) => callbacksRef.current.onTemplateDrop(id, x, y),
      onTemplatePlace: (x, y, angle) => callbacksRef.current.onTemplatePlace(x, y, angle),
      onPing: (x, y) => callbacksRef.current.onPing(x, y),
    });
    controller.current = board;
    board.init().catch(() => { /* WebGL unavailable: the board stays empty */ });
    return () => {
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
