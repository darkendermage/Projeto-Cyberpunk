import clsx from 'clsx';
import { LOGO_PATH, LOGO_VIEWBOX } from './logoPath';
import styles from './CyberpunkLogo.module.css';

/** The Cyberpunk RED logo, with cyan/red glitch layers driven by the visual-effects settings. */
export function CyberpunkLogo({ className }: { className?: string }) {
  const layer = (extra?: string) => (
    <svg viewBox={LOGO_VIEWBOX} className={clsx(styles.mark, extra)} aria-hidden="true" focusable="false">
      <path fillRule="evenodd" d={LOGO_PATH} />
    </svg>
  );
  return (
    <span className={clsx(styles.logo, className)} role="img" aria-label="Cyberpunk RED">
      {layer()}
      {layer(styles.glitchA)}
      {layer(styles.glitchB)}
    </span>
  );
}
