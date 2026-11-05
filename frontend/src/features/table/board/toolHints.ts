import { t } from '../../../i18n';
import type { BoardTool } from './BoardController';

/** A phone or tablet: a finger pans, and there is no right button or keyboard. */
const coarse = typeof window !== 'undefined' && typeof window.matchMedia === 'function' && window.matchMedia('(pointer: coarse)').matches;

/** The line under the board telling what the current tool does. */
export const TOOL_HINT: Record<BoardTool, string> = {
  select: coarse
    ? t('Um dedo arrasta o mapa ou o que você tocar · pinça dá zoom · segure para fazer um ping', 'One finger drags the map or what you touch · pinch zooms · hold to ping')
    : t('Botão direito arrasta o mapa · arraste no vazio para selecionar vários · segure para fazer um ping · Delete apaga · Ctrl+A seleciona tudo', 'Right button pans · drag on empty ground to select several · hold to ping · Delete clears · Ctrl+A selects all'),
  ruler: t('Arraste para medir (diagonal conta 1 quadrado)', 'Drag to measure (a diagonal counts as 1 square)'),
  template: t('Clique para pôr a área; em cone e linha, arraste para apontar', 'Click to place the area; for cones and lines, drag to aim'),
  token: t('Toque no mapa onde o seu token deve aparecer', 'Tap the map where your token should appear'),
};
