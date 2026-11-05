import type { ReactNode, RefObject } from 'react';
import { Maximize2, MousePointer2, Radio, Ruler, Shapes, ZoomIn, ZoomOut } from 'lucide-react';
import type { TemplateShapeName } from '../../../api/tableTypes';
import { IconButton, Select } from '../../../components/ui/ui';
import { t } from '../../../i18n';
import { SHAPE_LABEL, TEMPLATE_PRESETS } from '../templates';
import type { BoardTool, TemplateDraft } from './BoardController';
import type { BoardHandle } from './TableBoard';
import { NumField } from '../NumField';
import styles from '../Table.module.css';

const SHAPES: TemplateShapeName[] = ['Circle', 'Square', 'Cone', 'Line'];


export function BoardToolbar({ tool, onTool, draft, onDraft, board, children }: {
  tool: BoardTool;
  onTool: (tool: BoardTool) => void;
  draft: TemplateDraft;
  onDraft: (draft: TemplateDraft) => void;
  board: RefObject<BoardHandle | null>;
  children?: ReactNode;
}) {
  return (
    <div className={styles.toolbar}>
      <IconButton label={t('Selecionar e mover', 'Select and move')} active={tool === 'select'} onClick={() => onTool('select')}><MousePointer2 size={17} /></IconButton>
      <IconButton label={t('Régua', 'Ruler')} active={tool === 'ruler'} onClick={() => onTool('ruler')}><Ruler size={17} /></IconButton>
      <IconButton label={t('Área de efeito', 'Area of effect')} active={tool === 'template'} onClick={() => onTool('template')}><Shapes size={17} /></IconButton>
      <IconButton label="Ping" active={tool === 'ping'} onClick={() => onTool('ping')}><Radio size={17} /></IconButton>
      {tool === 'template' && (
        <>
          <span className={styles.divider} />
          <Select aria-label={t('Modelo de área', 'Area preset')} value=""
            onChange={(e) => {
              const preset = TEMPLATE_PRESETS.find((p) => p.id === e.target.value);
              if (preset) onDraft({ ...draft, shape: preset.shape, sizeMeters: preset.sizeMeters });
            }}>
            <option value="">{t('Modelos…', 'Presets…')}</option>
            {TEMPLATE_PRESETS.map((p) => <option key={p.id} value={p.id} title={p.source}>{p.label}</option>)}
          </Select>
          <Select aria-label={t('Forma', 'Shape')} value={draft.shape} onChange={(e) => onDraft({ ...draft, shape: e.target.value as TemplateShapeName })}>
            {SHAPES.map((s) => <option key={s} value={s}>{SHAPE_LABEL[s]}</option>)}
          </Select>
          <NumField label={t('Tamanho em metros', 'Size in meters')} min={0.5} max={200} step={0.5}
            value={draft.sizeMeters} onChange={(sizeMeters) => onDraft({ ...draft, sizeMeters })} />
          <span className={styles.muted}>m</span>
          <input type="color" aria-label={t('Cor', 'Color')} value={draft.color} onChange={(e) => onDraft({ ...draft, color: e.target.value })} />
        </>
      )}
      <span className={styles.grow} />
      {children}
      <IconButton label={t('Afastar', 'Zoom out')} onClick={() => board.current?.zoomBy(0.8)}><ZoomOut size={17} /></IconButton>
      <IconButton label={t('Aproximar', 'Zoom in')} onClick={() => board.current?.zoomBy(1.25)}><ZoomIn size={17} /></IconButton>
      <IconButton label={t('Mostrar o mapa inteiro', 'Show the whole map')} onClick={() => board.current?.fit()}><Maximize2 size={17} /></IconButton>
    </div>
  );
}
