import type { Graphics } from 'pixi.js';

/** The simple pictograms drawn around a token, one per Critical Injury (CP:R p.187-188). */
export type InjuryIcon = 'leg' | 'arm' | 'hand' | 'eye' | 'head' | 'torso' | 'foreign' | 'lung' | 'spine' | 'burn' | 'ear' | 'generic';

/** The body part an injury hurts, by the words of its name; anything else gets the generic mark. */
export function injuryIcon(name: string): InjuryIcon {
  const n = name.toLowerCase();
  if (n.includes('leg')) return 'leg';
  if (n.includes('hand') || n.includes('finger')) return 'hand';
  if (n.includes('arm')) return 'arm';
  if (n.includes('eye')) return 'eye';
  if (n.includes('ear')) return 'ear';
  if (n.includes('lung') || n.includes('windpipe')) return 'lung';
  if (n.includes('spinal') || n.includes('spine')) return 'spine';
  if (n.includes('foreign')) return 'foreign';
  if (n.includes('burn') || n.includes('fire')) return 'burn';
  if (n.includes('rib') || n.includes('muscle')) return 'torso';
  if (n.includes('brain') || n.includes('skull') || n.includes('concussion') || n.includes('jaw') || n.includes('whiplash')) return 'head';
  return 'generic';
}

/** Up to 8 slots on a circle of `radius`: the first on top, then alternating left and right downward. */
export function arcPositions(count: number, radius: number): { x: number; y: number }[] {
  const step = Math.PI / 5; // 36° between neighbours
  const out: { x: number; y: number }[] = [];
  for (let i = 0; i < Math.min(count, 8); i++) {
    // 0 -> top; 1 -> left; 2 -> right; 3 -> further left…
    const k = Math.ceil(i / 2);
    const angle = -Math.PI / 2 + (i === 0 ? 0 : (i % 2 === 1 ? -1 : 1) * k * step);
    out.push({ x: Math.cos(angle) * radius, y: Math.sin(angle) * radius });
  }
  return out;
}

const WHITE = 0xffffff;

/** Draws one pictogram in white on `g`, centered on (0, 0), fitting a circle of radius `r`. */
export function drawInjuryIcon(g: Graphics, icon: InjuryIcon, r: number): void {
  const s = r * 0.62;
  const line = { width: Math.max(1.5, r * 0.18), color: WHITE, cap: 'round' as const, join: 'round' as const };
  switch (icon) {
    case 'leg':
      g.moveTo(-s * 0.2, -s).lineTo(-s * 0.1, s * 0.4).lineTo(s * 0.6, s * 0.7).stroke(line);
      break;
    case 'arm':
      g.moveTo(-s, -s * 0.2).lineTo(0, s * 0.3).lineTo(s * 0.8, -s * 0.6).stroke(line);
      break;
    case 'hand':
      for (const x of [-0.5, -0.17, 0.17, 0.5]) g.moveTo(x * s, 0).lineTo(x * s, -s * 0.8);
      g.stroke(line);
      g.roundRect(-s * 0.65, -s * 0.05, s * 1.3, s * 0.85, s * 0.3).fill({ color: WHITE });
      break;
    case 'eye':
      g.ellipse(0, 0, s, s * 0.55).stroke(line);
      g.circle(0, 0, s * 0.28).fill({ color: WHITE });
      break;
    case 'ear':
      g.arc(0, 0, s * 0.7, -Math.PI * 0.9, Math.PI * 0.6).stroke(line);
      g.arc(0, s * 0.05, s * 0.3, -Math.PI * 0.9, Math.PI * 0.3).stroke(line);
      break;
    case 'head':
      g.circle(0, -s * 0.15, s * 0.7).stroke(line);
      g.moveTo(-s * 0.15, -s * 0.75).lineTo(s * 0.1, -s * 0.3).lineTo(-s * 0.05, s * 0.1).stroke({ ...line, width: line.width * 0.7 });
      break;
    case 'torso':
      for (const y of [-0.5, 0, 0.5]) g.moveTo(-s * 0.75, y * s).quadraticCurveTo(0, y * s - s * 0.25, s * 0.75, y * s);
      g.moveTo(0, -s).lineTo(0, s).stroke(line);
      break;
    case 'lung':
      g.ellipse(-s * 0.42, s * 0.1, s * 0.35, s * 0.7).stroke(line);
      g.ellipse(s * 0.42, s * 0.1, s * 0.35, s * 0.7).stroke(line);
      g.moveTo(0, -s).lineTo(0, -s * 0.3).stroke(line);
      break;
    case 'spine':
      for (const y of [-0.75, -0.25, 0.25, 0.75]) g.roundRect(-s * 0.3, y * s - s * 0.18, s * 0.6, s * 0.36, s * 0.1).fill({ color: WHITE });
      break;
    case 'foreign':
      g.moveTo(-s, s).lineTo(s * 0.6, -s * 0.6).stroke(line);
      g.poly([s * 0.95, -s * 0.95, s * 0.25, -s * 0.7, s * 0.7, -s * 0.25]).fill({ color: WHITE });
      break;
    case 'burn':
      g.poly([0, -s, s * 0.6, s * 0.1, s * 0.3, s, -s * 0.3, s, -s * 0.6, s * 0.1]).fill({ color: WHITE });
      break;
    default:
      g.moveTo(0, -s).lineTo(0, s * 0.25).stroke(line);
      g.circle(0, s * 0.75, line.width * 0.6).fill({ color: WHITE });
  }
}
