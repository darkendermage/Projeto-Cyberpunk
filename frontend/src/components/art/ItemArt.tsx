import { memo, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import clsx from 'clsx';
import type { Item } from '../../api/types';
import styles from './ItemArt.module.css';

/**
 * Procedural silhouettes for items without an image. Each class/subtype maps to a small
 * vector drawing on a 240x120 canvas, filled with a metallic gradient and red accents.
 */

const M = 'url(#art-metal)';
const D = 'url(#art-dark)';
const B = 'url(#art-brass)';
const R = '#ff2a3d';
const EDGE = { stroke: 'rgba(255,255,255,0.18)', strokeWidth: 0.8 };

type Draw = () => ReactNode;

const guard = (x: number, y: number, w = 20) => (
  <path d={`M${x} ${y} q2 16 ${w} 12 v-12`} fill="none" stroke="#2b3038" strokeWidth={3} />
);
const vents = (x: number, y: number, n: number, gap = 8) =>
  Array.from({ length: n }, (_, i) => <rect key={i} x={x + i * gap} y={y} width={4} height={6} rx={1} fill="#050506" />);
const serrations = (x: number, y: number, n: number) =>
  Array.from({ length: n }, (_, i) => <line key={i} x1={x + i * 3.5} y1={y} x2={x + i * 3.5} y2={y + 12} stroke="#07080a" strokeWidth={1.2} />);

// ---------------------------------------------------------------- weapons

const pistol = (scale = 1, heavy = false): Draw => () => (
  <g transform={`translate(${120 - 120 * scale} ${60 - 62 * scale}) scale(${scale})`}>
    <polygon points="70,92 76,58 110,58 104,70 100,100 72,100" fill={D} {...EDGE} />
    {Array.from({ length: 6 }, (_, i) => <line key={i} x1={78} y1={70 + i * 5} x2={100} y2={70 + i * 5} stroke="#000" strokeOpacity={0.5} />)}
    <polygon points="72,56 178,56 178,63 122,63 116,68 104,68" fill={D} {...EDGE} />
    {guard(106, 63, 18)}
    <polygon points={`64,${heavy ? 36 : 40} 184,${heavy ? 36 : 40} 190,${heavy ? 42 : 45} 190,56 64,56`} fill={M} {...EDGE} />
    {serrations(70, heavy ? 40 : 43, 6)}
    <rect x={190} y={45} width={6} height={7} fill="#0b0c0e" />
    <rect x={146} y={heavy ? 40 : 43} width={22} height={3} fill={R} />
    <circle cx={176} cy={49} r={1.5} fill={R} />
  </g>
);

const revolver: Draw = () => (
  <g>
    <polygon points="70,58 98,60 94,100 64,100 62,88" fill={D} {...EDGE} />
    <polygon points="76,36 124,36 124,62 108,64 84,64" fill={M} {...EDGE} />
    <polygon points="70,32 80,36 78,42 72,40" fill={D} />
    <rect x={92} y={34} width={36} height={26} rx={4} fill={M} {...EDGE} />
    {[0, 1, 2].map((i) => <line key={i} x1={96} y1={40 + i * 7} x2={124} y2={40 + i * 7} stroke="#000" strokeOpacity={0.6} />)}
    <rect x={128} y={38} width={84} height={13} fill={M} {...EDGE} />
    <rect x={128} y={34} width={84} height={4} fill={D} />
    <rect x={204} y={30} width={4} height={6} fill={R} />
    {guard(98, 62, 16)}
    <rect x={150} y={44} width={40} height={2} fill={R} opacity={0.8} />
  </g>
);

const smg = (heavy: boolean): Draw => () => (
  <g>
    <rect x={30} y={heavy ? 44 : 47} width={30} height={5} fill={D} />
    <rect x={30} y={heavy ? 44 : 47} width={5} height={18} fill={D} />
    <polygon points={`60,${heavy ? 38 : 42} 172,${heavy ? 38 : 42} 178,${heavy ? 44 : 47} 178,64 60,64`} fill={M} {...EDGE} />
    <rect x={72} y={heavy ? 33 : 37} width={88} height={5} fill={D} />
    {vents(140, 50, 4, 8)}
    <rect x={178} y={heavy ? 46 : 49} width={heavy ? 44 : 24} height={heavy ? 11 : 8} fill={D} {...EDGE} />
    <polygon points="116,64 132,64 130,108 114,108" fill={D} {...EDGE} />
    <polygon points="84,64 102,64 96,96 78,96" fill={D} {...EDGE} />
    {heavy && <polygon points="150,64 160,64 158,88 150,88" fill={D} />}
    {guard(100, 64, 14)}
    <rect x={64} y={56} width={44} height={3} fill={R} />
  </g>
);

const shotgun: Draw = () => (
  <g>
    <polygon points="6,56 58,46 66,46 66,66 42,66 12,80 4,74" fill={D} {...EDGE} />
    <rect x={66} y={42} width={58} height={25} fill={M} {...EDGE} />
    <rect x={124} y={44} width={108} height={8} fill={M} {...EDGE} />
    <rect x={124} y={54} width={94} height={7} fill={D} />
    <rect x={140} y={51} width={48} height={13} rx={3} fill={D} {...EDGE} />
    {[0, 1, 2, 3, 4].map((i) => <line key={i} x1={146 + i * 8} y1={52} x2={146 + i * 8} y2={63} stroke="#000" strokeOpacity={0.6} />)}
    {guard(80, 67, 18)}
    <rect x={70} y={48} width={30} height={3} fill={R} />
    <rect x={226} y={42} width={4} height={4} fill={R} />
  </g>
);

const rifle: Draw = () => (
  <g>
    <polygon points="8,50 52,46 58,46 58,64 42,64 14,74 6,68" fill={D} {...EDGE} />
    <polygon points="58,42 142,42 142,64 58,64" fill={M} {...EDGE} />
    <rect x={84} y={31} width={42} height={9} rx={2} fill={D} {...EDGE} />
    <circle cx={126} cy={35.5} r={3} fill={R} opacity={0.85} />
    <rect x={90} y={39} width={4} height={4} fill={D} />
    <rect x={116} y={39} width={4} height={4} fill={D} />
    <polygon points="142,44 198,44 198,62 142,62" fill={D} {...EDGE} />
    {vents(150, 50, 5, 9)}
    <rect x={198} y={50} width={28} height={5} fill={M} />
    <rect x={222} y={47} width={10} height={11} fill={D} />
    <path d="M110 64 L126 64 Q130 84 140 98 L124 104 Q114 88 110 64 Z" fill={D} {...EDGE} />
    <polygon points="80,64 95,64 89,92 75,92" fill={D} {...EDGE} />
    {guard(95, 64, 12)}
    <rect x={60} y={52} width={44} height={3} fill={R} />
  </g>
);

const sniper: Draw = () => (
  <g>
    <polygon points="4,52 62,48 62,64 34,64 12,74 2,70" fill={D} {...EDGE} />
    <ellipse cx={32} cy={58} rx={10} ry={3.5} fill="#050506" />
    <rect x={62} y={46} width={66} height={17} fill={M} {...EDGE} />
    <rect x={128} y={51} width={106} height={5} fill={M} />
    <rect x={226} y={49} width={8} height={9} fill={D} />
    <rect x={76} y={30} width={62} height={10} rx={3} fill={D} {...EDGE} />
    <polygon points="138,27 150,25 150,45 138,43" fill={D} {...EDGE} />
    <polygon points="76,29 66,27 66,43 76,41" fill={D} />
    <circle cx={149} cy={35} r={3} fill={R} />
    <rect x={88} y={40} width={4} height={6} fill={D} />
    <rect x={120} y={40} width={4} height={6} fill={D} />
    <rect x={96} y={63} width={12} height={14} fill={D} />
    <polygon points="76,63 90,63 84,90 72,90" fill={D} {...EDGE} />
    <line x1={168} y1={57} x2={156} y2={98} stroke="#2b3038" strokeWidth={3} />
    <line x1={170} y1={57} x2={182} y2={98} stroke="#2b3038" strokeWidth={3} />
    <rect x={66} y={55} width={40} height={2.5} fill={R} />
  </g>
);

const machineGun: Draw = () => (
  <g>
    <polygon points="6,50 50,46 56,46 56,66 40,66 12,74 4,70" fill={D} {...EDGE} />
    <rect x={56} y={40} width={92} height={28} fill={M} {...EDGE} />
    <path d="M78 40 Q100 22 122 40" fill="none" stroke="#2b3038" strokeWidth={4} />
    <rect x={148} y={45} width={52} height={17} fill={D} {...EDGE} />
    {vents(152, 50, 5, 9)}
    <rect x={200} y={50} width={34} height={7} fill={M} />
    <rect x={92} y={68} width={38} height={28} fill={D} {...EDGE} />
    {[0, 1, 2].map((i) => <line key={i} x1={92} y1={76 + i * 7} x2={130} y2={76 + i * 7} stroke="#000" strokeOpacity={0.6} />)}
    <polygon points="68,68 82,68 76,94 64,94" fill={D} />
    <line x1={190} y1={62} x2={178} y2={100} stroke="#2b3038" strokeWidth={3} />
    <line x1={192} y1={62} x2={206} y2={100} stroke="#2b3038" strokeWidth={3} />
    <rect x={60} y={50} width={60} height={3} fill={R} />
  </g>
);

const bow: Draw = () => (
  <g>
    <path d="M128 8 C100 14 92 30 98 42 C84 54 84 66 98 78 C92 90 100 106 128 112" fill="none" stroke={M} strokeWidth={7} strokeLinecap="round" />
    <path d="M128 8 C100 14 92 30 98 42 C84 54 84 66 98 78 C92 90 100 106 128 112" fill="none" stroke="rgba(255,255,255,0.15)" strokeWidth={1} />
    <line x1={128} y1={8} x2={128} y2={112} stroke="#9aa3b0" strokeWidth={0.8} />
    <rect x={86} y={52} width={11} height={16} rx={2} fill={D} />
    <line x1={56} y1={60} x2={214} y2={60} stroke="#c9ced6" strokeWidth={2} />
    <polygon points="214,55 230,60 214,65" fill={R} />
    <polygon points="56,60 46,53 52,60 46,67" fill={R} opacity={0.85} />
  </g>
);

const crossbow: Draw = () => (
  <g>
    <polygon points="16,58 150,52 150,66 64,68 30,80 12,74" fill={D} {...EDGE} />
    <path d="M150 22 Q166 59 150 96" fill="none" stroke={M} strokeWidth={7} strokeLinecap="round" />
    <line x1={150} y1={22} x2={108} y2={58} stroke="#9aa3b0" strokeWidth={0.8} />
    <line x1={150} y1={96} x2={108} y2={58} stroke="#9aa3b0" strokeWidth={0.8} />
    <line x1={108} y1={56} x2={206} y2={56} stroke="#c9ced6" strokeWidth={2} />
    <polygon points="206,52 220,56 206,60" fill={R} />
    <rect x={92} y={40} width={36} height={8} rx={2} fill={D} {...EDGE} />
    <polygon points="70,66 84,66 80,90 66,90" fill={D} />
  </g>
);

const grenadeLauncher: Draw = () => (
  <g>
    <polygon points="20,52 64,48 64,64 40,66 22,74 16,70" fill={D} {...EDGE} />
    <rect x={64} y={44} width={24} height={22} fill={M} />
    <rect x={88} y={34} width={58} height={40} rx={6} fill={M} {...EDGE} />
    {[0, 1, 2, 3, 4].map((i) => <line key={i} x1={96 + i * 10} y1={36} x2={96 + i * 10} y2={72} stroke="#000" strokeOpacity={0.55} strokeWidth={2} />)}
    <rect x={146} y={42} width={58} height={24} fill={D} {...EDGE} />
    <rect x={204} y={40} width={10} height={28} fill={M} />
    <polygon points="76,66 92,66 86,96 72,96" fill={D} />
    <polygon points="164,66 176,66 174,90 164,90" fill={D} />
    <rect x={150} y={50} width={40} height={3} fill={R} />
  </g>
);

const rocketLauncher: Draw = () => (
  <g>
    <rect x={18} y={40} width={190} height={26} rx={6} fill={M} {...EDGE} />
    <polygon points="208,36 230,32 230,74 208,70" fill={D} {...EDGE} />
    <polygon points="18,42 6,38 6,68 18,64" fill={D} />
    <rect x={60} y={40} width={14} height={26} fill={R} opacity={0.85} />
    <rect x={98} y={28} width={30} height={12} fill={D} {...EDGE} />
    <polygon points="110,66 124,66 120,94 106,94" fill={D} />
    <polygon points="150,66 162,66 160,88 150,88" fill={D} />
    {vents(160, 50, 4, 10)}
  </g>
);

const knife: Draw = () => (
  <g>
    <rect x={50} y={54} width={46} height={12} rx={3} fill={D} {...EDGE} />
    {[0, 1, 2, 3].map((i) => <line key={i} x1={58 + i * 9} y1={54} x2={62 + i * 9} y2={66} stroke={R} strokeOpacity={0.7} />)}
    <rect x={96} y={48} width={6} height={24} fill={M} />
    <polygon points="102,54 192,52 210,59 192,66 102,64" fill={M} {...EDGE} />
    <line x1={106} y1={56} x2={196} y2={56} stroke="rgba(255,255,255,0.35)" strokeWidth={0.8} />
  </g>
);

const machete: Draw = () => (
  <g>
    <rect x={40} y={54} width={50} height={13} rx={4} fill={D} {...EDGE} />
    <rect x={90} y={50} width={6} height={21} fill={M} />
    <polygon points="96,52 198,44 220,52 206,68 96,66" fill={M} {...EDGE} />
    <line x1={100} y1={55} x2={204} y2={49} stroke="rgba(255,255,255,0.35)" strokeWidth={0.8} />
    <rect x={44} y={58} width={40} height={2} fill={R} />
  </g>
);

const sword: Draw = () => (
  <g>
    <circle cx={34} cy={60} r={6} fill={M} />
    <rect x={40} y={55} width={44} height={10} fill={D} {...EDGE} />
    <rect x={84} y={42} width={7} height={36} fill={M} {...EDGE} />
    <polygon points="91,55 212,55 228,60 212,65 91,65" fill={M} {...EDGE} />
    <line x1={92} y1={60} x2={214} y2={60} stroke="rgba(255,255,255,0.3)" strokeWidth={0.8} />
    <rect x={44} y={59} width={36} height={2} fill={R} />
  </g>
);

const katana: Draw = () => (
  <g>
    <polygon points="18,58 76,64 74,74 16,68" fill={D} {...EDGE} />
    {Array.from({ length: 6 }, (_, i) => <path key={i} d={`M${22 + i * 9} 60 l4 8 l4 -8`} fill="none" stroke={R} strokeOpacity={0.8} />)}
    <ellipse cx={80} cy={68} rx={4} ry={12} fill={M} {...EDGE} />
    <path d="M84 64 Q160 66 232 38 L234 43 Q164 74 84 72 Z" fill={M} {...EDGE} />
    <path d="M86 66 Q162 67 230 41" fill="none" stroke="rgba(255,255,255,0.45)" strokeWidth={0.8} />
  </g>
);

// ---------------------------------------------------------------- ammo & explosives

const rounds: Draw = () => (
  <g>
    {[0, 1, 2].map((i) => {
      const x = 84 + i * 26;
      const h = 44 - i * 4;
      const y = 96 - h;
      return (
        <g key={i}>
          <rect x={x} y={y} width={18} height={h} fill={B} {...EDGE} />
          <rect x={x - 1} y={94} width={20} height={4} fill={B} />
          <path d={`M${x} ${y} Q${x + 9} ${y - 30} ${x + 18} ${y} Z`} fill={i === 1 ? R : M} {...EDGE} />
        </g>
      );
    })}
    <line x1={60} y1={99} x2={180} y2={99} stroke={R} strokeOpacity={0.4} />
  </g>
);

const grenade: Draw = () => (
  <g>
    <ellipse cx={118} cy={66} rx={30} ry={34} fill={M} {...EDGE} />
    {[-14, 0, 14].map((d) => <line key={d} x1={88} y1={66 + d} x2={148} y2={66 + d} stroke="#000" strokeOpacity={0.5} strokeWidth={2} />)}
    {[-12, 4, 18].map((d) => <line key={d} x1={118 + d} y1={34} x2={118 + d} y2={98} stroke="#000" strokeOpacity={0.35} strokeWidth={2} />)}
    <rect x={108} y={24} width={20} height={12} fill={D} {...EDGE} />
    <path d="M128 28 Q150 30 150 64" fill="none" stroke="#2b3038" strokeWidth={4} />
    <circle cx={100} cy={26} r={8} fill="none" stroke={R} strokeWidth={2.5} />
  </g>
);

const explosive: Draw = () => (
  <g>
    <rect x={60} y={44} width={120} height={46} rx={3} fill={M} {...EDGE} />
    <rect x={70} y={52} width={100} height={6} fill="#000" opacity={0.35} />
    <rect x={98} y={36} width={44} height={20} fill={D} {...EDGE} />
    <circle cx={132} cy={46} r={3} fill={R} />
    <rect x={104} y={41} width={20} height={6} fill="#0a0" opacity={0.6} />
    <path d="M142 46 Q170 20 196 40" fill="none" stroke={R} strokeWidth={1.5} />
    <path d="M98 46 Q70 22 46 38" fill="none" stroke="#c9ced6" strokeWidth={1.5} />
  </g>
);

const detonator: Draw = () => (
  <g>
    <rect x={92} y={38} width={56} height={62} rx={6} fill={M} {...EDGE} />
    <rect x={100} y={46} width={40} height={16} fill="#050506" />
    <text x={120} y={58} textAnchor="middle" fontFamily="Share Tech Mono" fontSize={10} fill={R}>00:30</text>
    <circle cx={120} cy={80} r={10} fill={R} />
    <line x1={140} y1={38} x2={156} y2={12} stroke="#c9ced6" strokeWidth={2} />
  </g>
);

// ---------------------------------------------------------------- gear

const phone: Draw = () => (
  <g>
    <rect x={92} y={16} width={56} height={92} rx={8} fill={M} {...EDGE} />
    <rect x={98} y={24} width={44} height={70} rx={3} fill="#120306" />
    {[0, 1, 2, 3].map((i) => <rect key={i} x={104} y={32 + i * 14} width={32 - i * 6} height={4} fill={R} opacity={0.8 - i * 0.15} />)}
    <circle cx={120} cy={100} r={3} fill={D} />
  </g>
);

const tools: Draw = () => (
  <g>
    <g transform="rotate(-30 120 60)">
      <rect x={70} y={55} width={110} height={10} rx={3} fill={M} {...EDGE} />
      <path d="M180 48 a14 14 0 1 1 0 24 l-6 -6 v-12 z" fill={M} {...EDGE} />
    </g>
    <g transform="rotate(30 120 60)">
      <rect x={60} y={56} width={50} height={9} rx={3} fill={R} opacity={0.9} />
      <rect x={110} y={58} width={80} height={4} fill={M} />
    </g>
  </g>
);

const medkit: Draw = () => (
  <g>
    <rect x={64} y={36} width={112} height={66} rx={6} fill={M} {...EDGE} />
    <path d="M104 36 v-10 h32 v10" fill="none" stroke="#2b3038" strokeWidth={5} />
    <rect x={112} y={50} width={16} height={40} fill={R} />
    <rect x={100} y={62} width={40} height={16} fill={R} />
  </g>
);

const backpack: Draw = () => (
  <g>
    <path d="M84 30 Q120 10 156 30 L162 104 H78 Z" fill={M} {...EDGE} />
    <rect x={92} y={64} width={56} height={30} rx={4} fill={D} {...EDGE} />
    <line x1={92} y1={72} x2={148} y2={72} stroke={R} strokeWidth={2} />
    <path d="M100 30 Q120 18 140 30" fill="none" stroke="#2b3038" strokeWidth={4} />
  </g>
);

const padlock: Draw = () => (
  <g>
    <path d="M98 54 V38 a22 22 0 0 1 44 0 V54" fill="none" stroke={M} strokeWidth={9} />
    <rect x={84} y={52} width={72} height={54} rx={5} fill={M} {...EDGE} />
    <circle cx={120} cy={74} r={6} fill={R} />
    <rect x={117} y={78} width={6} height={14} fill={R} />
  </g>
);

const headset: Draw = () => (
  <g>
    <path d="M76 70 Q76 22 120 22 Q164 22 164 70" fill="none" stroke={M} strokeWidth={8} />
    <rect x={62} y={60} width={26} height={38} rx={8} fill={M} {...EDGE} />
    <rect x={152} y={60} width={26} height={38} rx={8} fill={M} {...EDGE} />
    <rect x={88} y={70} width={64} height={14} rx={6} fill={R} opacity={0.85} />
  </g>
);

const terminal: Draw = () => (
  <g>
    <rect x={56} y={22} width={128} height={80} rx={4} fill="#0a0c10" stroke={R} strokeOpacity={0.7} />
    <rect x={56} y={22} width={128} height={12} fill={R} opacity={0.75} />
    {[0, 1, 2, 3, 4].map((i) => (
      <rect key={i} x={66 + (i % 2) * 10} y={44 + i * 11} width={[70, 44, 86, 30, 58][i]} height={4} fill={i === 2 ? '#3de0ff' : '#c9ced6'} opacity={0.7} />
    ))}
    <rect x={150} y={88} width={8} height={6} fill={R}>
      <animate attributeName="opacity" values="1;0;1" dur="1.1s" repeatCount="indefinite" />
    </rect>
  </g>
);

const ice: Draw = () => (
  <g>
    <polygon points="120,14 166,38 166,86 120,110 74,86 74,38" fill="#0a0c10" stroke={R} strokeWidth={2} />
    <polygon points="120,28 152,46 152,78 120,96 88,78 88,46" fill={R} opacity={0.15} />
    <circle cx={106} cy={58} r={7} fill={R} />
    <circle cx={134} cy={58} r={7} fill={R} />
    <path d="M104 80 h32 M110 80 v8 M120 80 v8 M130 80 v8" stroke={R} strokeWidth={2} />
    <path d="M78 30 l20 22 l-8 10 l18 16" fill="none" stroke="#3de0ff" strokeWidth={1.5} opacity={0.8} />
  </g>
);

const wheel: Draw = () => (
  <g>
    <circle cx={120} cy={60} r={42} fill={D} stroke="#2b3038" strokeWidth={6} />
    <circle cx={120} cy={60} r={30} fill={M} {...EDGE} />
    {Array.from({ length: 6 }, (_, i) => (
      <rect key={i} x={117} y={32} width={6} height={28} fill="#0a0b0d" transform={`rotate(${i * 60} 120 60)`} />
    ))}
    <circle cx={120} cy={60} r={8} fill={R} />
  </g>
);

// ---------------------------------------------------------------- armor

const vest: Draw = () => (
  <g>
    <path d="M86 18 L104 18 Q120 34 136 18 L154 18 L170 40 L164 104 L76 104 L70 40 Z" fill={M} {...EDGE} />
    {[0, 1, 2].map((r) => [0, 1].map((c) => (
      <rect key={`${r}${c}`} x={88 + c * 34} y={44 + r * 19} width={30} height={15} rx={2} fill={D} stroke="#000" strokeOpacity={0.5} />
    )))}
    <rect x={118} y={40} width={4} height={62} fill={R} opacity={0.8} />
  </g>
);

const shield: Draw = () => (
  <g>
    <path d="M78 12 H162 L170 24 V92 L120 114 L70 92 V24 Z" fill={M} {...EDGE} />
    <rect x={92} y={30} width={56} height={9} rx={2} fill="#050506" />
    <rect x={92} y={33} width={56} height={2} fill={R} />
    <text x={120} y={74} textAnchor="middle" fontFamily="Chakra Petch" fontWeight={700} fontSize={14} fill={R} opacity={0.75}>NCPD</text>
  </g>
);

const jacket: Draw = () => (
  <g>
    <path d="M94 16 L120 30 L146 16 L178 32 L190 92 L170 96 L164 60 L164 106 L76 106 L76 60 L70 96 L50 92 L62 32 Z" fill={M} {...EDGE} />
    <path d="M94 16 L112 50 L120 30 L128 50 L146 16" fill={D} />
    <line x1={120} y1={36} x2={120} y2={106} stroke={R} strokeWidth={2} />
    <rect x={84} y={70} width={20} height={3} fill={R} opacity={0.7} />
  </g>
);

const frame: Draw = () => (
  <g>
    <rect x={116} y={12} width={8} height={70} fill={M} />
    {Array.from({ length: 6 }, (_, i) => (
      <path key={i} d={`M120 ${20 + i * 10} q-${30 - i * 2} 2 -${36 - i * 3} 10 M120 ${20 + i * 10} q${30 - i * 2} 2 ${36 - i * 3} 10`} fill="none" stroke={M} strokeWidth={3} />
    ))}
    <path d="M120 82 L96 112 M120 82 L144 112" stroke={M} strokeWidth={6} />
    <circle cx={96} cy={112} r={4} fill={R} />
    <circle cx={144} cy={112} r={4} fill={R} />
    <circle cx={120} cy={82} r={6} fill={R} />
  </g>
);

const mech: Draw = () => (
  <g>
    <path d="M60 108 L66 62 L92 48 L148 48 L174 62 L180 108 Z" fill={M} {...EDGE} />
    <path d="M96 52 L100 18 Q120 8 140 18 L144 52 Z" fill={M} {...EDGE} />
    <rect x={102} y={28} width={36} height={8} fill="#050506" />
    <rect x={104} y={30} width={32} height={4} fill={R} />
    <rect x={44} y={58} width={26} height={30} rx={4} fill={D} {...EDGE} />
    <rect x={170} y={58} width={26} height={30} rx={4} fill={D} {...EDGE} />
    <circle cx={120} cy={78} r={9} fill={R} opacity={0.85} />
  </g>
);

// ---------------------------------------------------------------- cyberware & fashion

const chip: Draw = () => (
  <g>
    <rect x={92} y={32} width={56} height={56} rx={4} fill={M} {...EDGE} />
    <rect x={104} y={44} width={32} height={32} fill="#120306" stroke={R} />
    {Array.from({ length: 5 }, (_, i) => (
      <g key={i}>
        <line x1={98 + i * 11} y1={32} x2={98 + i * 11} y2={20} stroke="#9aa3b0" strokeWidth={2} />
        <line x1={98 + i * 11} y1={88} x2={98 + i * 11} y2={100} stroke="#9aa3b0" strokeWidth={2} />
      </g>
    ))}
    <path d="M148 50 h30 l10 -14 h20 M148 70 h40 l8 12 h16 M92 56 h-32 l-10 12 h-16" fill="none" stroke={R} strokeOpacity={0.7} />
  </g>
);

const eye = (colorful = false): Draw => () => (
  <g>
    <path d="M50 60 Q120 6 190 60 Q120 114 50 60 Z" fill={D} {...EDGE} />
    <circle cx={120} cy={60} r={28} fill={colorful ? 'url(#art-rainbow)' : M} {...EDGE} />
    <circle cx={120} cy={60} r={18} fill="none" stroke={colorful ? '#fff' : R} strokeWidth={2} strokeDasharray="4 3" />
    <circle cx={120} cy={60} r={9} fill={colorful ? '#0a0b0d' : R} />
    <circle cx={114} cy={54} r={3} fill="#fff" opacity={0.7} />
    <path d="M50 60 h-24 M190 60 h24" stroke={R} strokeOpacity={0.6} />
  </g>
);

const audio: Draw = () => (
  <g>
    <path d="M92 30 Q60 30 60 62 Q60 92 92 96 Q104 96 104 84 Q104 74 94 70 Q84 66 88 54 Q92 44 104 46" fill="none" stroke={M} strokeWidth={9} strokeLinecap="round" />
    {Array.from({ length: 11 }, (_, i) => {
      const h = [10, 22, 34, 18, 44, 28, 50, 24, 36, 14, 8][i];
      return <rect key={i} x={120 + i * 8} y={60 - h / 2} width={4} height={h} fill={R} opacity={0.55 + (i % 3) * 0.15} />;
    })}
  </g>
);

const torso: Draw = () => (
  <g>
    <path d="M90 14 Q120 4 150 14 L164 40 L156 106 H84 L76 40 Z" fill={D} {...EDGE} />
    <path d="M100 40 Q120 30 140 40 M98 54 Q120 46 142 54 M100 68 Q120 62 140 68" fill="none" stroke={M} strokeWidth={4} />
    <circle cx={120} cy={86} r={11} fill={R} opacity={0.85} />
    <path d="M120 86 h28 l8 -10 M120 86 h-28 l-8 10" stroke="#3de0ff" strokeWidth={1.4} opacity={0.7} fill="none" />
  </g>
);

const hexSkin: Draw = () => (
  <g>
    {Array.from({ length: 5 }, (_, r) => Array.from({ length: 7 }, (_, c) => {
      const x = 66 + c * 18 + (r % 2) * 9;
      const y = 22 + r * 18;
      return <polygon key={`${r}-${c}`} points={`${x},${y - 9} ${x + 8},${y - 4} ${x + 8},${y + 5} ${x},${y + 10} ${x - 8},${y + 5} ${x - 8},${y - 4}`}
        fill={(r * 7 + c) % 5 === 0 ? R : M} opacity={(r * 7 + c) % 5 === 0 ? 0.8 : 0.9} stroke="#000" strokeOpacity={0.6} />;
    }))}
  </g>
);

const cyberArm: Draw = () => (
  <g>
    <rect x={20} y={46} width={82} height={26} rx={10} fill={M} {...EDGE} />
    <circle cx={108} cy={59} r={14} fill={D} stroke={R} strokeWidth={2} />
    <polygon points="118,48 196,52 196,66 118,70" fill={M} {...EDGE} />
    {vents(130, 56, 5, 11)}
    <path d="M196 50 l18 -8 l10 6 M196 58 l26 0 M196 66 l20 8" stroke={M} strokeWidth={5} strokeLinecap="round" fill="none" />
    <line x1={30} y1={59} x2={92} y2={59} stroke={R} strokeWidth={2} opacity={0.7} />
  </g>
);

const cyberLeg: Draw = () => (
  <g>
    <rect x={106} y={8} width={28} height={50} rx={10} fill={M} {...EDGE} />
    <circle cx={120} cy={62} r={12} fill={D} stroke={R} strokeWidth={2} />
    <polygon points="110,70 130,70 128,104 112,104" fill={M} {...EDGE} />
    <path d="M108 104 h40 l6 8 h-48 z" fill={D} {...EDGE} />
    <line x1={120} y1={16} x2={120} y2={50} stroke={R} strokeWidth={2} opacity={0.7} />
  </g>
);

const hand: Draw = () => (
  <g>
    <rect x={92} y={60} width={56} height={46} rx={8} fill={M} {...EDGE} />
    {[0, 1, 2, 3].map((i) => (
      <g key={i}>
        <rect x={93 + i * 14} y={22 + (i === 1 || i === 2 ? -6 : 0)} width={11} height={40} rx={5} fill={M} {...EDGE} />
        <circle cx={98.5 + i * 14} cy={40 + (i === 1 || i === 2 ? -6 : 0)} r={2.5} fill={R} />
      </g>
    ))}
    <rect x={146} y={64} width={30} height={11} rx={5} fill={M} transform="rotate(-30 146 70)" {...EDGE} />
  </g>
);

const borg: Draw = () => (
  <g>
    <path d="M86 98 L92 40 Q120 12 148 40 L154 98 Z" fill={M} {...EDGE} />
    <rect x={98} y={46} width={44} height={14} fill="#050506" />
    {[0, 1, 2].map((i) => <circle key={i} cx={106 + i * 14} cy={53} r={4} fill={R} />)}
    <path d="M92 70 h-24 l-10 -10 M148 70 h24 l10 -10 M120 22 v-12" stroke={R} strokeOpacity={0.7} fill="none" />
    <rect x={104} y={72} width={32} height={20} fill={D} />
  </g>
);

const hair: Draw = () => (
  <g>
    <circle cx={120} cy={66} r={28} fill={D} {...EDGE} />
    {Array.from({ length: 9 }, (_, i) => (
      <path key={i} d={`M${96 + i * 6} 48 Q${80 + i * 10} ${10 + (i % 3) * 6} ${60 + i * 16} ${20 + (i % 2) * 10}`} fill="none"
        stroke={i % 2 ? R : '#3de0ff'} strokeWidth={2.5} opacity={0.85} />
    ))}
  </g>
);

const tattoo: Draw = () => (
  <g>
    <path d="M30 50 Q120 36 214 46 L214 76 Q120 86 30 72 Z" fill={D} {...EDGE} />
    <path d="M70 62 q12 -14 24 0 t24 0 t24 0 t24 0" fill="none" stroke={R} strokeWidth={2.5} />
    <circle cx={182} cy={60} r={6} fill="none" stroke="#3de0ff" strokeWidth={2} />
  </g>
);

const wristDisplay: Draw = () => (
  <g>
    <path d="M30 48 Q120 40 214 48 L214 74 Q120 82 30 74 Z" fill={D} {...EDGE} />
    <rect x={92} y={46} width={56} height={30} rx={4} fill="#120306" stroke={R} />
    <text x={120} y={66} textAnchor="middle" fontFamily="Share Tech Mono" fontSize={14} fill={R}>23:59</text>
  </g>
);

// ---------------------------------------------------------------- drugs

const injector: Draw = () => (
  <g>
    <rect x={56} y={50} width={110} height={22} rx={8} fill={M} {...EDGE} />
    <rect x={86} y={54} width={50} height={14} rx={3} fill={R} opacity={0.75} />
    <rect x={166} y={56} width={30} height={10} fill={D} />
    <line x1={196} y1={61} x2={222} y2={61} stroke="#c9ced6" strokeWidth={2} />
    <rect x={40} y={46} width={16} height={30} rx={3} fill={D} {...EDGE} />
  </g>
);

const pills: Draw = () => (
  <g>
    {[[86, 60, -30], [130, 48, 20], [150, 80, -10]].map(([x, y, a], i) => (
      <g key={i} transform={`rotate(${a} ${x} ${y})`}>
        <rect x={x - 24} y={y - 10} width={48} height={20} rx={10} fill={M} {...EDGE} />
        <rect x={x} y={y - 10} width={24} height={20} rx={10} fill={i === 1 ? '#3de0ff' : R} opacity={0.85} />
      </g>
    ))}
  </g>
);

const vial: Draw = () => (
  <g>
    <rect x={108} y={18} width={24} height={12} fill={D} />
    <path d="M104 30 h32 v64 a16 16 0 0 1 -32 0 Z" fill="rgba(255,255,255,0.06)" stroke="#9aa3b0" />
    <path d="M106 60 h28 v34 a14 14 0 0 1 -28 0 Z" fill={R} opacity={0.75} />
  </g>
);

// ---------------------------------------------------------------- vehicles & HQ

const car = (tactical = false): Draw => () => (
  <g>
    <path d={tactical
      ? 'M14 84 L20 58 L64 50 L92 30 L170 30 L196 52 L226 58 L230 84 Z'
      : 'M12 84 L18 66 L70 56 L102 36 L160 36 L188 56 L226 62 L230 84 Z'} fill={M} {...EDGE} />
    <path d={tactical ? 'M96 36 L166 36 L184 52 L76 52 Z' : 'M106 42 L156 42 L176 56 L84 58 Z'} fill="#120306" />
    <circle cx={64} cy={86} r={16} fill="#0a0b0d" stroke="#2b3038" strokeWidth={4} />
    <circle cx={186} cy={86} r={16} fill="#0a0b0d" stroke="#2b3038" strokeWidth={4} />
    <rect x={208} y={66} width={20} height={4} fill={R} />
    <rect x={14} y={70} width={14} height={4} fill={R} />
    <line x1={30} y1={74} x2={210} y2={74} stroke={R} strokeOpacity={0.5} />
  </g>
);

const motorcycle: Draw = () => (
  <g>
    <circle cx={58} cy={84} r={22} fill="none" stroke="#2b3038" strokeWidth={6} />
    <circle cx={184} cy={84} r={22} fill="none" stroke="#2b3038" strokeWidth={6} />
    <path d="M58 84 L96 52 L150 48 L184 84 M150 48 L170 30 L186 34" fill="none" stroke={M} strokeWidth={6} strokeLinejoin="round" />
    <path d="M92 52 Q120 30 156 46 L150 60 L98 62 Z" fill={M} {...EDGE} />
    <rect x={118} y={44} width={24} height={4} fill={R} />
  </g>
);

const bicycle: Draw = () => (
  <g>
    <circle cx={64} cy={80} r={26} fill="none" stroke="#2b3038" strokeWidth={4} />
    <circle cx={178} cy={80} r={26} fill="none" stroke="#2b3038" strokeWidth={4} />
    <path d="M64 80 L104 44 L156 44 L178 80 M104 44 L120 80 L156 44 M120 80 L64 80 M150 30 L156 44 M96 36 h18" fill="none" stroke={M} strokeWidth={4} />
    <circle cx={120} cy={80} r={5} fill={R} />
  </g>
);

const aerodyne: Draw = () => (
  <g>
    <path d="M24 64 L60 44 L120 34 L176 38 L222 56 L214 72 L40 76 Z" fill={M} {...EDGE} />
    <path d="M118 38 L172 42 L190 54 L110 54 Z" fill="#120306" />
    <ellipse cx={64} cy={86} rx={26} ry={6} fill={D} stroke={R} />
    <ellipse cx={180} cy={86} rx={26} ry={6} fill={D} stroke={R} />
    <ellipse cx={64} cy={92} rx={20} ry={3} fill={R} opacity={0.5} />
    <ellipse cx={180} cy={92} rx={20} ry={3} fill={R} opacity={0.5} />
    <rect x={200} y={58} width={16} height={4} fill={R} />
  </g>
);

const helicopter: Draw = () => (
  <g>
    <rect x={30} y={22} width={180} height={4} rx={2} fill="#2b3038" />
    <rect x={116} y={24} width={8} height={14} fill={D} />
    <path d="M70 52 Q74 38 112 38 L150 40 Q168 44 170 60 L166 74 L84 76 Q70 70 70 52 Z" fill={M} {...EDGE} />
    <path d="M80 52 Q84 44 108 44 L112 60 L82 62 Z" fill="#120306" />
    <path d="M168 56 L222 50 L222 58 L168 66 Z" fill={D} {...EDGE} />
    <rect x={214} y={38} width={6} height={22} fill={M} />
    <path d="M86 76 L82 90 M154 76 L158 90" stroke="#2b3038" strokeWidth={3} />
    <rect x={70} y={89} width={100} height={4} rx={2} fill={D} stroke={R} strokeWidth={0.8} />
    <circle cx={160} cy={66} r={2.5} fill={R} />
  </g>
);

const aerozep: Draw = () => (
  <g>
    <ellipse cx={120} cy={50} rx={98} ry={30} fill={M} {...EDGE} />
    {[0, 1, 2, 3, 4].map((i) => <line key={i} x1={52 + i * 34} y1={24} x2={52 + i * 34} y2={76} stroke="#07080a" strokeOpacity={0.5} />)}
    <path d="M208 50 L232 32 L232 68 Z" fill={D} {...EDGE} />
    <rect x={84} y={78} width={72} height={18} rx={4} fill={D} {...EDGE} />
    {[0, 1, 2, 3, 4].map((i) => <rect key={i} x={92 + i * 13} y={83} width={8} height={6} fill={R} opacity={0.8} />)}
    <rect x={60} y={44} width={120} height={10} fill="#0a0c10" stroke={R} strokeOpacity={0.6} />
  </g>
);

const boat: Draw = () => (
  <g>
    <path d="M18 66 L222 62 L200 92 L44 92 Z" fill={M} {...EDGE} />
    <path d="M86 62 L100 40 L150 40 L162 62 Z" fill={D} {...EDGE} />
    <path d="M106 46 L146 46 L152 58 L102 58 Z" fill="#120306" />
    <rect x={124} y={26} width={3} height={14} fill="#2b3038" />
    <path d="M30 98 Q60 92 90 98 T150 98 T210 98" fill="none" stroke={R} strokeOpacity={0.6} strokeWidth={2} />
    <rect x={190} y={68} width={18} height={4} fill={R} />
  </g>
);

const building: Draw = () => (
  <g>
    <rect x={70} y={20} width={60} height={88} fill={M} {...EDGE} />
    <rect x={130} y={44} width={44} height={64} fill={D} {...EDGE} />
    {Array.from({ length: 6 }, (_, r) => Array.from({ length: 3 }, (_, c) => (
      <rect key={`${r}${c}`} x={78 + c * 17} y={28 + r * 13} width={10} height={6} fill={(r + c) % 3 === 0 ? R : '#0a0b0d'} opacity={0.85} />
    )))}
    <rect x={140} y={86} width={24} height={22} fill="#050506" />
  </g>
);

const workbench: Draw = () => (
  <g>
    <rect x={40} y={60} width={160} height={10} fill={M} {...EDGE} />
    <rect x={50} y={70} width={8} height={38} fill={D} />
    <rect x={182} y={70} width={8} height={38} fill={D} />
    <rect x={60} y={24} width={120} height={30} fill="#0a0c10" stroke={R} strokeOpacity={0.6} />
    {[0, 1, 2, 3, 4].map((i) => <line key={i} x1={72 + i * 22} y1={30} x2={72 + i * 22} y2={48} stroke="#9aa3b0" strokeWidth={3} />)}
    <rect x={120} y={50} width={30} height={10} fill={R} opacity={0.8} />
  </g>
);

const camera: Draw = () => (
  <g>
    <rect x={60} y={40} width={100} height={36} rx={4} fill={M} {...EDGE} />
    <circle cx={160} cy={58} r={14} fill={D} stroke="#2b3038" strokeWidth={3} />
    <circle cx={160} cy={58} r={6} fill={R} />
    <path d="M90 76 L90 100 L60 100" stroke={M} strokeWidth={6} fill="none" />
    <circle cx={72} cy={50} r={3} fill={R}>
      <animate attributeName="opacity" values="1;0.2;1" dur="1.6s" repeatCount="indefinite" />
    </circle>
  </g>
);

const scope: Draw = () => (
  <g>
    <rect x={50} y={44} width={140} height={22} rx={6} fill={M} {...EDGE} />
    <rect x={36} y={38} width={22} height={34} rx={5} fill={D} {...EDGE} />
    <rect x={182} y={40} width={26} height={30} rx={6} fill={D} {...EDGE} />
    <circle cx={195} cy={55} r={9} fill="#0a0c10" stroke={R} strokeWidth={2} />
    <circle cx={195} cy={55} r={3} fill={R} />
    <rect x={104} y={30} width={20} height={14} fill={D} />
    <rect x={88} y={66} width={16} height={12} fill={D} />
    <rect x={136} y={66} width={16} height={12} fill={D} />
  </g>
);

const magazine: Draw = () => (
  <g>
    <path d="M90 20 L140 20 L148 100 L98 104 Z" fill={M} {...EDGE} />
    <rect x={96} y={12} width={38} height={10} fill={D} />
    {[0, 1, 2, 3].map((i) => <line key={i} x1={104} y1={36 + i * 16} x2={136} y2={36 + i * 16} stroke="#2b3038" strokeWidth={3} />)}
    <rect x={106} y={84} width={24} height={6} fill={R} opacity={0.85} />
  </g>
);

const silencer: Draw = () => (
  <g>
    <rect x={40} y={46} width={160} height={28} rx={12} fill={M} {...EDGE} />
    <rect x={24} y={52} width={20} height={16} fill={D} />
    {[0, 1, 2, 3, 4, 5].map((i) => <line key={i} x1={70 + i * 20} y1={48} x2={70 + i * 20} y2={72} stroke="#2b3038" strokeWidth={2} />)}
    <circle cx={200} cy={60} r={5} fill="#0a0c10" stroke={R} strokeWidth={2} />
  </g>
);

const cog: Draw = () => (
  <g>
    {Array.from({ length: 8 }, (_, i) => (
      <rect key={i} x={112} y={14} width={16} height={22} fill={M} transform={`rotate(${i * 45} 120 60)`} />
    ))}
    <circle cx={120} cy={60} r={34} fill={M} {...EDGE} />
    <circle cx={120} cy={60} r={12} fill="#0a0c10" stroke={R} strokeWidth={3} />
  </g>
);

// ---------------------------------------------------------------- mapping

const WEAPON_ART: Record<string, Draw> = {
  MediumPistol: pistol(1), HeavyPistol: pistol(1.08, true), VeryHeavyPistol: revolver,
  Smg: smg(false), HeavySmg: smg(true), Shotgun: shotgun, AssaultRifle: rifle, SniperRifle: sniper,
  MachineGun: machineGun, Bow: bow, Crossbow: crossbow, GrenadeLauncher: grenadeLauncher, RocketLauncher: rocketLauncher,
  LightMelee: knife, MediumMelee: machete, HeavyMelee: sword, VeryHeavyMelee: katana,
};

function pickArt(item: Pick<Item, 'category' | 'subtype' | 'attributes'>): Draw {
  const subtype = item.subtype?.value ?? '';
  const attr = (key: string) => item.attributes?.[key];
  switch (item.category) {
    case 'weapons':
      return WEAPON_ART[subtype] ?? rifle;
    case 'ammunition':
      return { Round: rounds, Grenade: grenade, Explosive: explosive, Accessory: detonator }[subtype] ?? rounds;
    case 'weapon-attachments':
      return ({
        Scope: scope, Magazine: magazine, Underbarrel: grenadeLauncher, Bayonet: knife, SmartLink: chip, Muzzle: silencer,
        Barrel: silencer, Internals: cog, Rebuild: tools, TechUpgrade: tools,
      } as Record<string, Draw>)[subtype] ?? cog;
    case 'gear':
      return ({
        Electronics: phone, Tool: tools, Medical: medkit, Survival: backpack, Security: padlock, Lifestyle: headset,
        VehicleUpgrade: wheel,
      } as Record<string, Draw>)[subtype] ?? backpack;
    case 'armor':
      return ({ BodyArmor: vest, Shield: shield, Clothing: jacket, LinearFrame: frame, PoweredArmor: mech } as Record<string, Draw>)[subtype] ?? vest;
    case 'cyberware': {
      if (subtype === 'Cyberfinger') return hand;
      if (subtype === 'Fashionware') {
        return ({ Hair: hair, Eyes: eye(true), Hand: wristDisplay } as Record<string, Draw>)[String(attr('bodyLocation') ?? '')] ?? tattoo;
      }
      if (subtype === 'Borgware' || subtype === 'Bioexotic') return borg;
      const location = String(attr('bodyLocation') ?? '');
      return ({
        NervousSystem: chip, Eyes: eye(), Ears: audio, Internal: torso, Skin: hexSkin, Arm: cyberArm, Leg: cyberLeg,
        Hand: hand, FullBody: borg, Head: borg,
      } as Record<string, Draw>)[location] ?? chip;
    }
    case 'netrunning':
      return ({ Cyberdeck: terminal, Program: terminal, BlackIce: ice, Hardware: chip } as Record<string, Draw>)[subtype] ?? terminal;
    case 'drugs':
      return ({ Combat: injector, Street: pills, Netrunning: chip, Social: vial } as Record<string, Draw>)[subtype] ?? pills;
    case 'vehicles':
      return ({
        Bicycle: bicycle, Motorcycle: motorcycle, Car: car(), SportsCar: car(), Tactical: car(true), Aerodyne: aerodyne, Boat: boat,
        Helicopter: helicopter, Aerozep: aerozep, Gyrocopter: helicopter, Hoverboard: bicycle,
      } as Record<string, Draw>)[subtype] ?? car();
    case 'vehicle-upgrades':
      return ({ Vehicle: wheel, Room: building, Bicycle: bicycle, Consumable: vial } as Record<string, Draw>)[subtype] ?? wheel;
    case 'hq-upgrades':
      return ({ Workshop: workbench, Armory: workbench, Security: camera } as Record<string, Draw>)[subtype] ?? building;
    default:
      return chip;
  }
}

interface ItemArtProps {
  item: Pick<Item, 'id' | 'category' | 'subtype' | 'attributes' | 'manufacturer' | 'imageUrl'>;
  size?: 'card' | 'row' | 'detail' | 'thumb';
  className?: string;
  children?: ReactNode;
}

function ItemArtImpl({ item, size = 'card', className, children }: ItemArtProps) {
  const draw = pickArt(item);
  const figure = useRef<SVGGElement>(null);
  const [viewBox, setViewBox] = useState('0 0 240 120');

  // Drawings have different extents (a pistol is much smaller than a sniper rifle):
  // fit the view box to the real bounding box so every silhouette fills the frame.
  useLayoutEffect(() => {
    const box = figure.current?.getBBox?.();
    if (!box || box.width === 0) return;
    const pad = Math.max(box.width, box.height) * 0.04;
    setViewBox(`${box.x - pad} ${box.y - pad} ${box.width + pad * 2} ${box.height + pad * 2}`);
  }, [draw]);
  return (
    <div className={clsx(styles.art, styles[size], className)}>
      <div className={styles.backdrop} aria-hidden />
      {item.imageUrl ? (
        <img className={styles.image} src={item.imageUrl} alt="" loading="lazy" />
      ) : (
        <svg className={styles.svg} viewBox={viewBox} aria-hidden preserveAspectRatio="xMidYMid meet">
          <defs>
            <linearGradient id="art-metal" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stopColor="#4a515c" />
              <stop offset="0.45" stopColor="#1f2329" />
              <stop offset="1" stopColor="#0b0c0f" />
            </linearGradient>
            <linearGradient id="art-dark" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stopColor="#262a31" />
              <stop offset="1" stopColor="#08090b" />
            </linearGradient>
            <linearGradient id="art-brass" x1="0" y1="0" x2="1" y2="0">
              <stop offset="0" stopColor="#6b5216" />
              <stop offset="0.5" stopColor="#d4ab3c" />
              <stop offset="1" stopColor="#5a4410" />
            </linearGradient>
            <radialGradient id="art-rainbow">
              <stop offset="0" stopColor="#ff2a3d" />
              <stop offset="0.5" stopColor="#b84dff" />
              <stop offset="1" stopColor="#3de0ff" />
            </radialGradient>
          </defs>
          <g ref={figure} className={styles.figure}>{draw()}</g>
        </svg>
      )}
      {(size === 'card' || size === 'detail') && item.manufacturer && (
        <span className={styles.maker}>{item.manufacturer.name}</span>
      )}
      {children}
    </div>
  );
}

export const ItemArt = memo(ItemArtImpl);
