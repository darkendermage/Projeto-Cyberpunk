import { memo, useMemo } from 'react';
import styles from './CitySkyline.module.css';

/** Deterministic PRNG so the skyline is identical on every render. */
function mulberry32(seed: number) {
  return () => {
    seed |= 0;
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

interface Building { x: number; w: number; h: number; windows: { x: number; y: number; on: number }[]; antenna: boolean }

function buildLayer(seed: number, count: number, minH: number, maxH: number, width: number, windowDensity: number): Building[] {
  const rand = mulberry32(seed);
  const buildings: Building[] = [];
  let x = -20;
  while (x < width && buildings.length < count) {
    const w = 26 + rand() * 60;
    const h = minH + rand() * (maxH - minH);
    const windows: Building['windows'] = [];
    for (let wy = 8; wy < h - 6; wy += 7) {
      for (let wx = 5; wx < w - 6; wx += 6) {
        if (rand() < windowDensity) windows.push({ x: wx, y: wy, on: rand() });
      }
    }
    buildings.push({ x, w, h, windows, antenna: rand() > 0.75 });
    x += w + rand() * 6;
  }
  return buildings;
}

const WIDTH = 1600;
const HEIGHT = 170;

function CitySkylineImpl() {
  const back = useMemo(() => buildLayer(7, 60, 60, 130, WIDTH, 0.08), []);
  const front = useMemo(() => buildLayer(42, 50, 40, 150, WIDTH, 0.16), []);

  return (
    <svg className={styles.skyline} viewBox={`0 0 ${WIDTH} ${HEIGHT}`} preserveAspectRatio="xMidYMax slice" aria-hidden>
      <defs>
        <linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#0a0204" />
          <stop offset="0.7" stopColor="#2a0710" />
          <stop offset="1" stopColor="#4a0b16" />
        </linearGradient>
        <linearGradient id="bld-back" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#1a0a0f" />
          <stop offset="1" stopColor="#0d0507" />
        </linearGradient>
        <linearGradient id="bld-front" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#120a0d" />
          <stop offset="1" stopColor="#050304" />
        </linearGradient>
        <filter id="neon" x="-50%" y="-50%" width="200%" height="200%">
          <feGaussianBlur stdDeviation="3" result="blur" />
          <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
        </filter>
      </defs>

      <rect width={WIDTH} height={HEIGHT} fill="url(#sky)" />
      <ellipse cx={1050} cy={150} rx={520} ry={70} fill="#ff2a3d" opacity={0.12} />

      <g opacity={0.9}>
        {back.map((b, i) => (
          <g key={i} transform={`translate(${b.x} ${HEIGHT - b.h - 10})`}>
            <rect width={b.w} height={b.h + 10} fill="url(#bld-back)" />
            {b.windows.map((w, j) => (
              <rect key={j} x={w.x} y={w.y} width={2.5} height={2} fill={w.on > 0.85 ? '#ff6b78' : '#5a1520'} opacity={0.6} />
            ))}
          </g>
        ))}
      </g>

      {/* billboards and vertical neon signs */}
      <g filter="url(#neon)">
        <g className={styles.flicker}>
          <rect x={1060} y={28} width={104} height={62} fill="#2a050b" stroke="#ff2a3d" strokeWidth={2} />
          <circle cx={1112} cy={52} r={14} fill="none" stroke="#ff2a3d" strokeWidth={3} />
          <path d="M1104 52 l8 -8 l8 8 l-8 8 z" fill="#ff2a3d" />
          <text x={1112} y={83} textAnchor="middle" fontFamily="Chakra Petch" fontWeight={700} fontSize={12} fill="#ff4d5e" letterSpacing="2">NC·CORP</text>
        </g>
        <rect x={720} y={40} width={16} height={92} fill="#1a0408" stroke="#ff2a3d" strokeWidth={1.2} />
        <text x={728} y={56} textAnchor="middle" fontSize={11} fill="#ff4d5e" style={{ writingMode: 'vertical-rl' }}>ナイトシティ</text>
        <rect x={1290} y={58} width={70} height={20} fill="#1a0408" stroke="#ff2a3d" />
        <text x={1325} y={73} textAnchor="middle" fontFamily="Share Tech Mono" fontSize={12} fill="#ff4d5e" className={styles.flickerSlow}>EDDIES</text>
        <rect x={880} y={70} width={12} height={60} fill="#1a0408" stroke="#ff2a3d" />
        <rect x={500} y={84} width={60} height={14} fill="#1a0408" stroke="#ff2a3d" />
        <text x={530} y={95} textAnchor="middle" fontFamily="Share Tech Mono" fontSize={10} fill="#ff4d5e">24/7</text>
      </g>

      <g>
        {front.map((b, i) => (
          <g key={i} transform={`translate(${b.x} ${HEIGHT - b.h * 0.7})`}>
            <rect width={b.w} height={b.h} fill="url(#bld-front)" stroke="#2a0a10" strokeWidth={0.5} />
            {b.antenna && <line x1={b.w / 2} y1={0} x2={b.w / 2} y2={-14} stroke="#3a0c14" strokeWidth={1.5} />}
            {b.antenna && <circle cx={b.w / 2} cy={-14} r={1.6} fill="#ff2a3d" className={styles.blink} />}
            {b.windows.map((w, j) => (
              <rect key={j} x={w.x} y={w.y} width={2.5} height={2}
                fill={w.on > 0.9 ? '#ffcf3a' : w.on > 0.6 ? '#ff4d5e' : '#30101a'} opacity={w.on > 0.6 ? 0.8 : 0.5} />
            ))}
          </g>
        ))}
      </g>

      <rect y={HEIGHT - 2} width={WIDTH} height={2} fill="#ff2a3d" opacity={0.7} />
    </svg>
  );
}

export const CitySkyline = memo(CitySkylineImpl);
