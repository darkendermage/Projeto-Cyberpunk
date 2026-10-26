import type { CyberRegionReport } from '../../api/characterTypes';
import { HOTSPOTS, type RegionKey } from './anatomy';
import styles from './Cyberware.module.css';
import { t, tv } from '../../i18n';

const W = 400;

// A translucent "glass" body over its skeleton, on the 400 × 640 canvas. Limbs are drawn once for the viewer's left
// side (the character's right) and mirrored; an installed cyberlimb turns that limb and its bones to chrome.

const MIRROR = `translate(${W} 0) scale(-1 1)`;

const HEAD = 'M200 44 C214 44 226 54 226 74 C226 88 223 100 217 108 C212 115 206 119 200 119 C194 119 188 115 183 108 C177 100 174 88 174 74 C174 54 186 44 200 44 Z';
const EAR = 'M176 78 C168 78 167 97 177 101 Z';
const TORSO = 'M200 108 L191 108 C191 122 190 131 186 136 C176 142 160 144 146 150 C136 154 130 166 132 180 L140 196 '
  + 'C146 214 148 236 152 256 C156 276 160 290 160 300 C160 316 154 332 152 346 C152 360 160 372 172 380 C182 386 192 392 200 394 Z';
const ARM = 'M150 148 C134 150 126 164 126 182 C124 210 118 240 112 262 C106 290 96 330 88 362 '
  + 'C84 376 80 392 78 408 C77 416 80 424 86 426 C92 428 96 420 98 410 C100 398 100 386 102 374 '
  + 'C108 346 118 312 126 286 C132 266 138 248 142 226 C146 206 150 192 152 184 Z';
const LEG = 'M156 340 C150 370 150 400 154 430 C158 456 162 470 162 482 C156 505 158 540 164 570 C166 584 166 592 164 598 '
  + 'C158 604 150 610 156 614 L184 614 C188 610 188 602 186 596 C186 586 188 578 188 566 C190 540 192 510 190 486 '
  + 'C190 470 192 456 194 440 C196 420 198 400 200 388 L172 380 Z';

/** Bones along the middle of the body: skull, spine, sternum and sacrum. */
const SKULL = [
  'M200 52 C213 52 221 62 221 76 C221 88 217 96 213 100 L187 100 C183 96 179 88 179 76 C179 62 187 52 200 52 Z',
  'M184 98 C186 110 192 116 200 116 C208 116 214 110 216 98',
  'M190 106 L210 106', 'M200 90 L197 98 L203 98 Z',
];
const VERTEBRAE = Array.from({ length: 27 }, (_, i) => 120 + i * 9.4);
const STERNUM = 'M196 150 L204 150 L203 222 L200 230 L197 222 Z';
const SACRUM = 'M191 336 L209 336 L205 362 L200 368 L195 362 Z';

/** Bones of one side (drawn for the viewer's left, mirrored for the other). */
const RIB_WIDTH = [26, 34, 40, 44, 46, 46, 44, 40];
const RIBS = RIB_WIDTH.map((w, i) => {
  const y = 156 + i * 11;
  const x = 200 - w;
  const end = i < 6 ? 197 : 186 - i;
  return `M197 ${y} C${x + 6} ${y - 8} ${x - 4} ${y + 4} ${x} ${y + 16} C${x + 4} ${y + 26} ${190 - i} ${y + 24} ${end} ${y + 18}`;
});
const CLAVICLE = 'M198 146 C186 149 168 146 148 153';
const PELVIS = [
  'M197 330 C186 321 166 318 158 330 C153 343 162 356 174 362 C184 367 192 372 198 380',
  'M198 380 C190 382 184 378 180 370',
];
const ARM_BONES = {
  lines: ['M146 166 L120 260', 'M121 270 L97 358', 'M114 268 L90 356', 'M98 372 L103 394'],
  joints: [[146, 162, 7], [118, 265, 5], [93, 365, 3.5]] as const,
  fingers: [[84, 388, 80, 418], [88, 390, 85, 423], [92, 391, 90, 424], [96, 389, 95, 420]] as const,
};
const LEG_BONES = {
  lines: ['M176 356 L174 466', 'M178 482 L180 582', 'M168 484 L170 578'],
  joints: [[176, 351, 7], [175, 474, 5.5], [178, 590, 3.5]] as const,
  toes: [[176, 594, 160, 610], [178, 594, 168, 612], [180, 594, 176, 612], [182, 594, 183, 611]] as const,
};

/** Faint muscle lines on one side, as in an anatomical render. */
const MUSCLES = [
  'M198 170 C184 188 164 188 150 176', 'M150 154 C136 170 132 190 136 206', 'M156 256 C170 280 182 300 188 330',
  'M160 384 C168 420 176 450 178 468', 'M196 400 C190 430 184 455 182 470', 'M164 512 C170 530 176 540 178 554',
  'M128 200 C126 220 124 236 120 254',
];
const ABS = ['M200 232 L200 330', 'M186 248 L214 248', 'M187 268 L213 268', 'M188 288 L212 288'];

export function AnatomyFigure({ regions, selected, onSelect, fbc }: {
  regions: CyberRegionReport[];
  selected: RegionKey | null;
  onSelect: (key: RegionKey) => void;
  fbc: boolean;
}) {
  const byKey = new Map(regions.map((r) => [r.key as RegionKey, r]));
  const chrome = (key: RegionKey) => fbc || !!byKey.get(key)?.foundationInstanceId;
  const sides = [
    { arm: 'arm-right' as const, leg: 'leg-right' as const, transform: undefined },
    { arm: 'arm-left' as const, leg: 'leg-left' as const, transform: MIRROR },
  ];

  return (
    <svg className={styles.figure} viewBox={`0 0 ${W} 640`} role="group" aria-label={t('Modelo anatômico com as regiões de cyberware', 'Anatomical model with the cyberware regions')}>
      <defs>
        <linearGradient id="glassFill" gradientUnits="userSpaceOnUse" x1="0" y1="40" x2="0" y2="620">
          <stop offset="0" stopColor="#ff6f80" />
          <stop offset="0.32" stopColor="#d22843" />
          <stop offset="0.52" stopColor="#7a2350" />
          <stop offset="0.68" stopColor="#25407a" />
          <stop offset="1" stopColor="#3de0ff" />
        </linearGradient>
        <linearGradient id="boneStroke" gradientUnits="userSpaceOnUse" x1="0" y1="40" x2="0" y2="620">
          <stop offset="0" stopColor="#ffe3e7" />
          <stop offset="0.5" stopColor="#ffb3c0" />
          <stop offset="0.64" stopColor="#b9d2ff" />
          <stop offset="1" stopColor="#c8f7ff" />
        </linearGradient>
        <linearGradient id="chrome" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stopColor="#2b3b45" />
          <stop offset="0.35" stopColor="#a9c6d2" />
          <stop offset="0.55" stopColor="#1a2830" />
          <stop offset="0.8" stopColor="#7fb6c6" />
          <stop offset="1" stopColor="#22323a" />
        </linearGradient>
        <pattern id="scan" width="4" height="4" patternUnits="userSpaceOnUse">
          <rect width="4" height="1" fill="rgba(255,255,255,0.07)" />
        </pattern>
        {/* Glass: a faint body, brighter toward the silhouette, with a glowing rim. */}
        <filter id="glass" x="-10%" y="-5%" width="120%" height="110%">
          <feComponentTransfer in="SourceGraphic" result="body"><feFuncA type="linear" slope="0.22" /></feComponentTransfer>
          <feGaussianBlur in="SourceAlpha" stdDeviation="7" result="blur" />
          <feComposite in="SourceAlpha" in2="blur" operator="arithmetic" k2="1" k3="-1" result="band" />
          <feComposite in="SourceGraphic" in2="band" operator="in" result="fresnel" />
          <feMorphology in="SourceAlpha" operator="erode" radius="1.4" result="inner" />
          <feComposite in="SourceGraphic" in2="inner" operator="out" result="edge" />
          <feGaussianBlur in="edge" stdDeviation="3" result="edgeGlow" />
          <feMerge>
            <feMergeNode in="body" /><feMergeNode in="fresnel" /><feMergeNode in="edgeGlow" /><feMergeNode in="edge" />
          </feMerge>
        </filter>
        <filter id="glow" x="-50%" y="-50%" width="200%" height="200%">
          <feGaussianBlur stdDeviation="2.4" result="b" />
          <feMerge><feMergeNode in="b" /><feMergeNode in="SourceGraphic" /></feMerge>
        </filter>
        <radialGradient id="floor" cx="0.5" cy="0.5" r="0.5">
          <stop offset="0" stopColor="#3de0ff" stopOpacity="0.3" />
          <stop offset="1" stopColor="#3de0ff" stopOpacity="0" />
        </radialGradient>
      </defs>

      {/* platform */}
      <ellipse cx="200" cy="612" rx="120" ry="20" fill="url(#floor)" />
      <ellipse cx="200" cy="612" rx="96" ry="14" className={styles.ring} />
      <ellipse cx="200" cy="612" rx="64" ry="9" className={styles.ring} />

      {/* glass body: the parts are merged before the filter, so there are no seams between them */}
      <g filter="url(#glass)" fill="url(#glassFill)">
        <BodyShapes />
      </g>
      <g fill="url(#scan)" aria-hidden><BodyShapes /></g>

      {/* muscles and skeleton */}
      <g className={styles.muscles} aria-hidden>
        {ABS.map((d) => <path key={d} d={d} />)}
        {sides.map((s) => <g key={s.arm} transform={s.transform}>{MUSCLES.map((d) => <path key={d} d={d} />)}</g>)}
      </g>
      <g className={styles.bones} filter="url(#glow)" aria-hidden>
        {SKULL.map((d) => <path key={d} d={d} />)}
        <circle cx="190" cy="84" r="5" /><circle cx="210" cy="84" r="5" />
        {VERTEBRAE.map((y) => <rect key={y} x="196.5" y={y} width="7" height="6" rx="1.5" className={styles.vertebra} />)}
        <path d={STERNUM} /><path d={SACRUM} />
        {sides.map((s) => (
          <g key={s.arm} transform={s.transform}>
            <path d={CLAVICLE} />
            {RIBS.map((d) => <path key={d} d={d} />)}
            {PELVIS.map((d) => <path key={d} d={d} />)}
            <circle cx="182" cy="366" r="5" />
            <g className={chrome(s.arm) ? styles.boneChrome : undefined}>
              {ARM_BONES.lines.map((d) => <path key={d} d={d} />)}
              {ARM_BONES.joints.map(([x, y, r]) => <circle key={`${x}-${y}`} cx={x} cy={y} r={r} className={styles.joint} />)}
              {ARM_BONES.fingers.map(([x1, y1, x2, y2]) => <path key={x1} d={`M93 370 L${x1} ${y1} L${x2} ${y2}`} />)}
            </g>
            <g className={chrome(s.leg) ? styles.boneChrome : undefined}>
              {LEG_BONES.lines.map((d) => <path key={d} d={d} />)}
              {LEG_BONES.joints.map(([x, y, r]) => <circle key={`${x}-${y}`} cx={x} cy={y} r={r} className={styles.joint} />)}
              {LEG_BONES.toes.map(([x1, y1, x2, y2]) => <path key={x2} d={`M${x1} ${y1} L${x2} ${y2}`} />)}
            </g>
          </g>
        ))}
      </g>

      {/* cyberlimbs, Full Body Conversion and the selected limb; clicking a limb selects its region */}
      {fbc && <g className={styles.limbChrome} aria-hidden><path d={HEAD} /><path d={TORSO} /><path d={TORSO} transform={MIRROR} /></g>}
      {sides.map((s) => (
        <g key={s.arm} transform={s.transform}>
          {([[s.arm, ARM], [s.leg, LEG]] as const).map(([key, d]) => (
            <path key={key} d={d} aria-hidden onClick={() => onSelect(key)}
              className={`${styles.limb} ${chrome(key) ? styles.limbChrome : ''} ${selected === key ? styles.limbSelected : ''}`} />
          ))}
        </g>
      ))}

      {/* leader lines and hotspots */}
      {(Object.keys(HOTSPOTS) as RegionKey[]).map((key) => {
        const h = HOTSPOTS[key];
        const r = byKey.get(key);
        const count = r?.pieces.length ?? 0;
        const edge = h.panel === 'left' ? 0 : W;
        const elbow = h.panel === 'left' ? Math.min(h.x - 24, 60) : Math.max(h.x + 24, W - 60);
        const active = selected === key;
        return (
          <g key={key}>
            <polyline points={`${h.x},${h.y} ${elbow},${h.y} ${edge},${h.y}`}
              className={`${styles.leader} ${count > 0 ? styles.leaderOn : ''} ${active ? styles.leaderActive : ''}`} />
            <g role="button" tabIndex={0} className={styles.hotspot}
              aria-label={t(`${tv(r?.label) ?? key}: ${count} peça(s)${r?.slotsTotal != null ? `, ${r.slotsUsed} de ${r.slotsTotal} slots` : ''}`, `${tv(r?.label) ?? key}: ${count} piece(s)${r?.slotsTotal != null ? `, ${r.slotsUsed} of ${r.slotsTotal} slots` : ''}`)}
              aria-pressed={active}
              onClick={() => onSelect(key)}
              onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onSelect(key); } }}>
              <circle cx={h.x} cy={h.y} r={14} className={`${styles.hotRing} ${active ? styles.hotRingActive : ''}`} />
              <circle cx={h.x} cy={h.y} r={6} className={`${styles.hotDot} ${count > 0 ? styles.hotDotOn : ''}`} />
              {count > 0 && <text x={h.x + 10} y={h.y - 10} className={styles.hotCount}>{count}</text>}
            </g>
          </g>
        );
      })}
    </svg>
  );
}

/** The whole silhouette: head, ears, torso, arms and legs on both sides. */
function BodyShapes() {
  return (
    <>
      <path d={HEAD} />
      {[undefined, MIRROR].map((entry) => (
        <g key={entry ?? 'right'} transform={entry}>
          <path d={EAR} /><path d={TORSO} /><path d={ARM} /><path d={LEG} />
        </g>
      ))}
    </>
  );
}
