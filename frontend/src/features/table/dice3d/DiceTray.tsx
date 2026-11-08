import { useEffect, useImperativeHandle, useRef, useState, type Ref } from 'react';
import { t } from '../../../i18n';
import { playDice } from '../sound';
import type { DiceThrow } from './dice';
import type { DiceScene } from './diceScene';
import styles from '../Table.module.css';

export interface DiceTrayHandle { roll: (dice: DiceThrow) => void }

/** How long the total stays up after the dice stop. */
const HOLD_MS = 1600;

// Throws queue up. three.js only loads on the first throw, and without WebGL the tray just stays out of the way.
export function DiceTray({ ref, sound }: { ref?: Ref<DiceTrayHandle>; sound: boolean }) {
  const host = useRef<HTMLDivElement>(null);
  const canvas = useRef<HTMLCanvasElement>(null);
  const scene = useRef<DiceScene | null>(null);
  const queue = useRef<DiceThrow[]>([]);
  const playing = useRef(false);
  const broken = useRef(false);
  const soundOn = useRef(sound);
  const [shown, setShown] = useState<{ dice: DiceThrow; settled: boolean } | null>(null);
  const [count, setCount] = useState(0);

  useEffect(() => { soundOn.current = sound; }, [sound]);

  useEffect(() => {
    const el = host.current;
    if (!el) return;
    const observer = new ResizeObserver(() => scene.current?.resize(el.clientWidth, el.clientHeight));
    observer.observe(el);
    return () => { observer.disconnect(); scene.current?.dispose(); scene.current = null; };
  }, []);

  const next = async () => {
    if (playing.current || broken.current) return;
    const dice = queue.current.shift();
    if (!dice) return;
    playing.current = true;
    try {
      if (!scene.current) {
        const { DiceScene } = await import('./diceScene');
        scene.current = new DiceScene(canvas.current!);
        scene.current.resize(host.current!.clientWidth, host.current!.clientHeight);
      }
      setShown({ dice, settled: false });
      if (soundOn.current) window.setTimeout(() => playDice(dice.dice.length), 550);
      await scene.current.play(dice);
      setShown({ dice, settled: true });
      setCount((c) => c + 1);
      await new Promise((resolve) => window.setTimeout(resolve, HOLD_MS));
      scene.current.clear();
    } catch {
      broken.current = true; // no WebGL: the chat still has every roll
      queue.current = [];
    } finally {
      setShown(null);
      playing.current = false;
      void next();
    }
  };

  useImperativeHandle(ref, () => ({
    roll: (dice) => {
      // A burst of rolls (a whole table rolling Initiative) keeps only the last few.
      queue.current = [...queue.current, dice].slice(-3);
      void next();
    },
  }));

  const critical = shown?.dice.critical;
  return (
    <div ref={host} className={`${styles.diceTray} ${shown ? styles.diceTrayOn : ''}`} aria-hidden={!shown} data-dice={count}>
      <canvas ref={canvas} className={styles.diceCanvas} />
      {shown?.settled && (
        <div className={styles.diceTotal} role="status">
          <span className={styles.diceLabel}>{shown.dice.label}</span>
          <strong>{shown.dice.total}</strong>
          {critical === 'Success' && <span className={styles.diceCrit}>{t('Crítico!', 'Critical!')}</span>}
          {critical === 'Failure' && <span className={styles.diceFumble}>{t('Falha crítica', 'Critical failure')}</span>}
        </div>
      )}
    </div>
  );
}
