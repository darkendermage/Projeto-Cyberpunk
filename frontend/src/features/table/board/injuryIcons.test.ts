import { describe, expect, it } from 'vitest';
import { arcPositions, injuryIcon } from './injuryIcons';

// Every Critical Injury of the body and head tables (CP:R p.187-188, character-rules.json).
const TABLE = [
  'Brain Injury', 'Broken Arm', 'Broken Jaw', 'Broken Leg', 'Broken Ribs', 'Collapsed Lung', 'Concussion', 'Cracked Skull', 'Crushed Fingers',
  'Crushed Windpipe', 'Damaged Ear', 'Damaged Eye', 'Dismembered Arm', 'Dismembered Hand', 'Dismembered Leg', 'Foreign Object', 'Lost Ear',
  'Lost Eye', 'Spinal Injury', 'Torn Muscle', 'Whiplash',
];

describe('injuryIcon', () => {
  it('gives every injury of the tables its own icon', () => {
    for (const name of TABLE) expect(injuryIcon(name), name).not.toBe('generic');
  });

  it('picks the body part', () => {
    expect(injuryIcon('Broken Leg')).toBe('leg');
    expect(injuryIcon('Dismembered Hand')).toBe('hand');
    expect(injuryIcon('Lost Eye')).toBe('eye');
    expect(injuryIcon('Foreign Object')).toBe('foreign');
  });

  it('falls back to the generic icon', () => expect(injuryIcon('Something new')).toBe('generic'));
});

describe('arcPositions', () => {
  it('puts the first icon on top, at the radius', () => {
    const points = arcPositions(3, 10);
    expect(points).toHaveLength(3);
    expect(points[0].x).toBeCloseTo(0);
    expect(points[0].y).toBeCloseTo(-10);
    for (const p of points) expect(Math.hypot(p.x, p.y)).toBeCloseTo(10);
  });

  it('alternates sides and keeps at most 8 slots', () => {
    const points = arcPositions(3, 10);
    expect(Math.sign(points[1].x)).toBe(-Math.sign(points[2].x));
    expect(arcPositions(12, 10)).toHaveLength(8);
  });
});
