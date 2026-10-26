
import { t } from '../i18n';// Character portraits are stored with the sheet and the server accepts up to 2 MB (CharacterService.PortraitMaxBytes).
// Big PNGs (a 1536 × 1024 render easily passes 3 MB) are shrunk and re-encoded in the browser before upload, so the
// player never hits the limit with an ordinary picture.

export const PORTRAIT_MAX_BYTES = 2 * 1024 * 1024;
export const PORTRAIT_MAX_SIDE = 1024;
export const PORTRAIT_TYPES = ['image/png', 'image/jpeg', 'image/webp', 'image/gif'];

/** Scales width × height down (never up) so the longest side fits in `maxSide`. */
export function fitSize(width: number, height: number, maxSide = PORTRAIT_MAX_SIDE): { width: number; height: number } {
  const longest = Math.max(width, height);
  if (longest <= maxSide) return { width, height };
  const scale = maxSide / longest;
  return { width: Math.max(1, Math.round(width * scale)), height: Math.max(1, Math.round(height * scale)) };
}

/** Whether a file can go as it is: already small and not oversized (GIFs always go as they are, to keep animation). */
export function canUploadAsIs(file: { type: string; size: number }, width: number, height: number): boolean {
  if (file.size > PORTRAIT_MAX_BYTES) return false;
  return file.type === 'image/gif' || Math.max(width, height) <= PORTRAIT_MAX_SIDE;
}

export class PortraitError extends Error {}

const QUALITIES = [0.85, 0.75, 0.6, 0.45];

/**
 * Returns a file the server will accept: the original when it is already small, otherwise the picture resized to at
 * most 1024 px and re-encoded as WebP (JPEG where the browser can't write WebP), lowering the quality until it fits.
 */
export async function preparePortrait(file: File): Promise<File> {
  if (!PORTRAIT_TYPES.includes(file.type)) throw new PortraitError(t('Use uma imagem PNG, JPEG, WebP ou GIF.', 'Use a PNG, JPEG, WebP or GIF image.'));
  const bitmap = await createImageBitmap(file).catch(() => {
    throw new PortraitError(t('Não deu para abrir essa imagem.', 'Couldn\'t open that image.'));
  });
  try {
    if (canUploadAsIs(file, bitmap.width, bitmap.height)) return file;
    if (file.type === 'image/gif') throw new PortraitError(t('GIFs precisam ter até 2 MB (para não perder a animação).', 'GIFs must be 2 MB or less (so the animation isn\'t lost).'));

    const size = fitSize(bitmap.width, bitmap.height);
    const canvas = document.createElement('canvas');
    canvas.width = size.width;
    canvas.height = size.height;
    const context = canvas.getContext('2d');
    if (!context) throw new PortraitError(t('Não deu para reduzir a imagem neste navegador.', 'Couldn\'t shrink the image in this browser.'));
    context.drawImage(bitmap, 0, 0, size.width, size.height);

    for (const quality of QUALITIES) {
      const blob = await encode(canvas, 'image/webp', quality) ?? await encode(canvas, 'image/jpeg', quality);
      if (blob && blob.size <= PORTRAIT_MAX_BYTES) {
        const extension = blob.type === 'image/webp' ? 'webp' : 'jpg';
        return new File([blob], t(`${file.name.replace(/\.[^.]+$/, '') || 'retrato'}.${extension}`, `${file.name.replace(/\.[^.]+$/, '') || 'portrait'}.${extension}`), { type: blob.type });
      }
    }
    throw new PortraitError(t('Mesmo reduzida, a imagem passou de 2 MB.', 'Even shrunk, the image is over 2 MB.'));
  } finally {
    bitmap.close();
  }
}

/** canvas.toBlob as a promise; null when the browser can't write that type (it silently falls back to PNG). */
function encode(canvas: HTMLCanvasElement, type: string, quality: number): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob((blob) => resolve(blob && blob.type === type ? blob : null), type, quality));
}
