import type { SceneGrid, SceneTemplate, TemplateShapeName } from '../../api/tableTypes';
import { t } from '../../i18n';
import { cellToPx } from './grid';

export type ShapeGeometry =
  | { kind: 'circle'; cx: number; cy: number; r: number }
  | { kind: 'poly'; points: number[] };

/** Half the opening of a cone: as wide at its end as it is long. */
const CONE_HALF_ANGLE = Math.atan(0.5);
const CONE_STEPS = 10;

/**
 * An area in image pixels. A circle's size is its radius; a square's is its side (centered on the origin); a cone's
 * and a line's is their length from the origin, pointing at the angle. A line is one cell wide.
 */
export function templateShape(g: SceneGrid, tpl: Pick<SceneTemplate, 'shape' | 'x' | 'y' | 'sizeMeters' | 'angle'>): ShapeGeometry {
  const o = cellToPx(g, tpl.x, tpl.y);
  const length = (tpl.sizeMeters / g.metersPerCell) * g.sizePx;
  const a = (tpl.angle * Math.PI) / 180;
  const rotate = (dx: number, dy: number) => [o.x + dx * Math.cos(a) - dy * Math.sin(a), o.y + dx * Math.sin(a) + dy * Math.cos(a)];

  switch (tpl.shape) {
    case 'Circle':
      return { kind: 'circle', cx: o.x, cy: o.y, r: length };
    case 'Square': {
      const h = length / 2;
      return { kind: 'poly', points: [...rotate(-h, -h), ...rotate(h, -h), ...rotate(h, h), ...rotate(-h, h)] };
    }
    case 'Line': {
      const w = g.sizePx / 2;
      return { kind: 'poly', points: [...rotate(0, -w), ...rotate(length, -w), ...rotate(length, w), ...rotate(0, w)] };
    }
    case 'Cone': {
      const points = [o.x, o.y];
      for (let i = 0; i <= CONE_STEPS; i++) {
        const angle = -CONE_HALF_ANGLE + (2 * CONE_HALF_ANGLE * i) / CONE_STEPS;
        const reach = length / Math.cos(CONE_HALF_ANGLE);
        points.push(...rotate(reach * Math.cos(angle), reach * Math.sin(angle)));
      }
      return { kind: 'poly', points };
    }
  }
}

export interface TemplatePreset { id: string; shape: TemplateShapeName; sizeMeters: number; label: string; source: string }

/** Ready-made areas. The book gives the sizes; where it gives no shape, the shape is a table convention. */
export const TEMPLATE_PRESETS: TemplatePreset[] = [
  { id: 'explosive', shape: 'Square', sizeMeters: 10, label: t('Explosão (10×10 m)', 'Explosion (10×10 m)'), source: 'CP:R p.174 (Explosives)' },
  { id: 'shotgun', shape: 'Cone', sizeMeters: 6, label: t('Shotgun Shell (6 m à frente)', 'Shotgun Shell (6 m ahead)'),
    source: t('CP:R p.174 (alcance de 6 m; o cone é convenção da mesa)', 'CP:R p.174 (6 m reach; the cone is a table convention)') },
  { id: 'suppressive', shape: 'Circle', sizeMeters: 25, label: t('Fogo de supressão (25 m)', 'Suppressive Fire (25 m)'), source: 'CP:R p.174 (Suppressive Fire)' },
  { id: 'circle', shape: 'Circle', sizeMeters: 4, label: t('Círculo livre', 'Free circle'), source: t('livre', 'free') },
  { id: 'line', shape: 'Line', sizeMeters: 20, label: t('Linha livre', 'Free line'), source: t('livre', 'free') },
];

export const SHAPE_LABEL: Record<TemplateShapeName, string> = {
  Circle: t('Círculo', 'Circle'),
  Square: t('Quadrado', 'Square'),
  Cone: t('Cone', 'Cone'),
  Line: t('Linha', 'Line'),
};

/** What the area tool places next: shape, size and color. */
export interface AreaDraft { shape: TemplateShapeName; sizeMeters: number; color: string }

/** The area tool remembers its last shape in this browser. */
export const DRAFT_KEY = 'ncdb.table.area-draft';

const SHAPES: TemplateShapeName[] = ['Circle', 'Square', 'Cone', 'Line'];

/** The draft to start with: the last one used, or else the book's explosion (a 10×10 m square, CP:R p.174). */
export function initialDraft(stored: string | null): AreaDraft {
  const fallback: AreaDraft = { shape: 'Square', sizeMeters: 10, color: '#ff2a6d' };
  try {
    const d = JSON.parse(stored ?? 'null') as Partial<AreaDraft> | null;
    if (d && SHAPES.includes(d.shape as TemplateShapeName) && typeof d.sizeMeters === 'number' && d.sizeMeters >= 0.5 && d.sizeMeters <= 200)
      return { shape: d.shape as TemplateShapeName, sizeMeters: d.sizeMeters, color: /^#[0-9a-f]{6}$/i.test(d.color ?? '') ? d.color! : fallback.color };
  } catch { /* not a draft */ }
  return fallback;
}

export function readDraft(): AreaDraft {
  try { return initialDraft(localStorage.getItem(DRAFT_KEY)); } catch { return initialDraft(null); }
}

export function saveDraft(draft: AreaDraft) {
  try { localStorage.setItem(DRAFT_KEY, JSON.stringify(draft)); } catch { /* not remembered */ }
}

/** Whether this browser already used the area tool (its color choice is then the person's own). */
export function hasSavedDraft(): boolean {
  try { return localStorage.getItem(DRAFT_KEY) !== null; } catch { return false; }
}
