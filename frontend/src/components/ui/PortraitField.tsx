import { useRef, useState } from 'react';
import { ImagePlus, Trash2, UserRound } from 'lucide-react';
import { problemMessage } from '../../api/characters';
import { PortraitError, preparePortrait } from '../../lib/portrait';
import { useToast } from '../../state/ToastContext';
import { t } from '../../i18n';
import { IconButton } from './ui';
import styles from './PortraitField.module.css';

// Show / upload (shrinks big images first) / remove. The caller refreshes whatever lists it on onUpload and
// onRemove. No size = fills its column.
export function PortraitField({ url, alt, onUpload, onRemove, size }: {
  url: string | null;
  alt: string;
  onUpload: (file: File) => Promise<void>;
  onRemove: () => Promise<void>;
  size?: number;
}) {
  const input = useRef<HTMLInputElement>(null);
  const toast = useToast();
  const [busy, setBusy] = useState(false);

  const run = async (fn: () => Promise<void>) => {
    setBusy(true);
    try {
      await fn();
    } catch (error) {
      toast.notify(error instanceof PortraitError ? error.message : problemMessage(error), 'error');
    } finally {
      setBusy(false);
      if (input.current) input.current.value = '';
    }
  };

  return (
    <div className={styles.field} style={size ? { width: size } : undefined}>
      {url ? <img src={url} alt={alt} /> : <div className={styles.empty}><UserRound size={size ? Math.round(size / 3) : 72} /></div>}
      <div className={styles.actions}>
        <input ref={input} type="file" accept="image/png,image/jpeg,image/webp,image/gif" hidden
          onChange={(e) => { const file = e.target.files?.[0]; if (file) void run(async () => onUpload(await preparePortrait(file))); }} />
        <IconButton label={t('Trocar imagem (imagens grandes são reduzidas)', 'Change image (large images are shrunk)')} onClick={() => input.current?.click()} disabled={busy}><ImagePlus size={16} /></IconButton>
        {url && <IconButton label={t('Remover imagem', 'Remove image')} onClick={() => void run(onRemove)} disabled={busy}><Trash2 size={16} /></IconButton>}
      </div>
    </div>
  );
}

/** A small round image for lists; nothing when there is none. */
export function Thumb({ url, size = 28 }: { url: string | null; size?: number }) {
  if (!url) return null;
  return <img className={styles.thumb} src={url} alt="" width={size} height={size} loading="lazy" />;
}
