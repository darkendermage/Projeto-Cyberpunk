import { useRef, useState } from 'react';
import { ArrowDown, ArrowUp, Eye, EyeOff, ImagePlus, Lock, LockOpen, Trash2 } from 'lucide-react';
import type { ImageInput, SceneImageView } from '../../api/tableTypes';
import { Button, IconButton } from '../../components/ui/ui';
import { t } from '../../i18n';
import { aboveForMove, inputOf, orderedImages } from './images';
import styles from './Table.module.css';

/**
 * The "Imagens" tab (round 5): the scene's images like Photoshop layers, top first. Each row: picture, name, eye (GM),
 * lock, opacity, order, delete. A player edits only their own images; the GM, all of them.
 */
export function ImagesTab({ images, selected, gm, mySeatId, ownerName, limit, busy, onAdd, onSelect, onChange, onOrder, onRemove }: {
  images: SceneImageView[];
  selected: string | null;
  gm: boolean;
  mySeatId?: number;
  /** Who placed an image, for the owner tag. */
  ownerName: (seatId: number | null) => string;
  /** A player's limit per scene (the GM has none). */
  limit?: number;
  busy: boolean;
  onAdd: (file: File) => void;
  onSelect: (id: string) => void;
  onChange: (image: SceneImageView, input: ImageInput) => void;
  onOrder: (id: string, above: string | null) => void;
  onRemove: (image: SceneImageView) => void;
}) {
  const file = useRef<HTMLInputElement>(null);
  const [renaming, setRenaming] = useState<string | null>(null);
  const mine = images.filter((i) => i.ownerSeatId === mySeatId).length;
  const full = limit != null && mine >= limit;
  const stack = orderedImages(images);
  // A player may lock or unlock their own image unless the GM locked it; the GM, any.
  const canLock = (i: SceneImageView) => gm || (i.ownerSeatId === mySeatId && !i.lockedByGm);

  return (
    <div className={styles.stack}>
      <div className={styles.rowBetween}>
        <span className={styles.title}>{t('Imagens da cena', 'Scene images')}</span>
        {limit != null && <span className={styles.muted}>{mine}/{limit}</span>}
      </div>
      <input ref={file} type="file" accept="image/png,image/jpeg,image/webp,image/gif" hidden
        onChange={(e) => { const f = e.target.files?.[0]; if (f) onAdd(f); e.target.value = ''; }} />
      <Button variant="primary" icon={<ImagePlus size={15} />} disabled={busy || full} onClick={() => file.current?.click()}
        title={full ? t(`Limite de ${limit} imagens por cena.`, `Limit of ${limit} images per scene.`) : undefined}>
        {t('Adicionar imagem', 'Add image')}
      </Button>
      <span className={styles.muted}>{t('Arraste no mapa para mover; os cantos redimensionam (Shift solta a proporção) e a bolinha gira.', 'Drag on the map to move; the corners resize (Shift frees the proportion) and the knob turns.')}</span>
      {stack.length === 0 && <span className={styles.muted}>{t('Nenhuma imagem nesta cena.', 'No images on this scene.')}</span>}
      <ul className={styles.layerList}>
        {stack.map((i) => {
          const own = gm || i.ownerSeatId === mySeatId;
          const editable = own && (gm || !i.locked);
          const up = aboveForMove(images, i.id, 'up');
          const down = aboveForMove(images, i.id, 'down');
          return (
            <li key={i.id} className={`${styles.layerRow} ${selected === i.id ? styles.layerRowOn : ''} ${i.hidden ? styles.layerHidden : ''}`}
              title={own ? undefined : t(`Imagem de ${ownerName(i.ownerSeatId)}`, `${ownerName(i.ownerSeatId)}'s image`)}>
              <button type="button" className={styles.layerThumb} onClick={() => onSelect(i.id)} aria-label={t(`Selecionar ${i.name}`, `Select ${i.name}`)}>
                <img src={i.url} alt="" style={{ opacity: i.opacity }} />
              </button>
              <div className={styles.layerBody}>
                {renaming === i.id ? (
                  <input autoFocus defaultValue={i.name} aria-label={t('Nome da imagem', 'Image name')}
                    onBlur={(e) => { setRenaming(null); if (e.target.value.trim() && e.target.value !== i.name) onChange(i, inputOf(i, { name: e.target.value })); }}
                    onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); if (e.key === 'Escape') setRenaming(null); }} />
                ) : (
                  <span className={styles.layerName} onDoubleClick={() => { if (editable) setRenaming(i.id); }}>{i.name}</span>
                )}
                <span className={styles.muted}>{ownerName(i.ownerSeatId)}</span>
                <input type="range" min={10} max={100} step={5} defaultValue={Math.round(i.opacity * 100)} key={`${i.id}:${i.opacity}`} disabled={!editable || busy}
                  aria-label={t('Opacidade', 'Opacity')}
                  onPointerUp={(e) => onChange(i, inputOf(i, { opacity: Number((e.target as HTMLInputElement).value) / 100 }))}
                  onKeyUp={(e) => onChange(i, inputOf(i, { opacity: Number((e.target as HTMLInputElement).value) / 100 }))} />
              </div>
              <div className={styles.layerActions}>
                {gm && (
                  <IconButton label={i.hidden ? t('Mostrar aos jogadores', 'Show to the players') : t('Ocultar dos jogadores', 'Hide from the players')} disabled={busy}
                    onClick={() => onChange(i, inputOf(i, { hidden: !i.hidden }))}>{i.hidden ? <EyeOff size={14} /> : <Eye size={14} />}</IconButton>
                )}
                <IconButton label={i.locked ? (i.lockedByGm ? t('Destravar (travada pelo mestre)', 'Unlock (locked by the GM)') : t('Destravar', 'Unlock')) : t('Travar: ninguém move nem redimensiona', 'Lock: nobody moves or resizes it')}
                  active={i.locked} disabled={busy || !canLock(i)} onClick={() => onChange(i, inputOf(i, { locked: !i.locked }))}>
                  {i.locked ? <Lock size={14} color={i.lockedByGm ? 'var(--red)' : undefined} /> : <LockOpen size={14} />}
                </IconButton>
                <IconButton label={t('Subir na pilha', 'Move up the stack')} disabled={!editable || busy || up === undefined} onClick={() => onOrder(i.id, up ?? null)}><ArrowUp size={14} /></IconButton>
                <IconButton label={t('Descer na pilha', 'Move down the stack')} disabled={!editable || busy || down === undefined} onClick={() => { if (down !== undefined) onOrder(i.id, down); }}><ArrowDown size={14} /></IconButton>
                <IconButton label={t('Apagar imagem', 'Delete image')} disabled={!editable || busy}
                  onClick={() => { if (window.confirm(t(`Apagar "${i.name}"?`, `Delete "${i.name}"?`))) onRemove(i); }}><Trash2 size={14} /></IconButton>
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
