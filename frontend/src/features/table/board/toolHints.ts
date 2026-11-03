import { t } from '../../../i18n';
import type { BoardTool } from './BoardController';

/** The line under the board telling what the current tool does. */
export const TOOL_HINT: Record<BoardTool, string> = {
  select: t('Arraste para mover o mapa · roda do mouse dá zoom · Alt+clique faz um ping', 'Drag to pan · mouse wheel zooms · Alt+click pings'),
  ruler: t('Arraste para medir (diagonal conta 1 quadrado)', 'Drag to measure (a diagonal counts as 1 square)'),
  template: t('Clique para pôr a área; em cone e linha, arraste para apontar', 'Click to place the area; for cones and lines, drag to aim'),
  ping: t('Clique no mapa para chamar a atenção de todos', 'Click the map to get everyone\'s attention'),
};
