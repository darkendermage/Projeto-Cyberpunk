import { describe, expect, it } from 'vitest';
import { hpBar } from './hpBar';

describe('hpBar', () => {
  it('fills only HP when there is no shield', () => {
    expect(hpBar(20, 40)).toEqual({ hpPct: 50, shieldPct: 0 });
    expect(hpBar(-3, 40).hpPct).toBe(0);
  });

  it('puts the shield after the HP on one scale (HP max + shield max)', () => {
    const bar = hpBar(40, 40, 10, 10);
    expect(bar.hpPct).toBe(80);
    expect(bar.shieldPct).toBe(20);
    const hurt = hpBar(20, 40, 5, 10);
    expect(hurt.hpPct).toBe(40);
    expect(hurt.shieldPct).toBe(10);
  });

  it('ignores a broken shield', () => {
    expect(hpBar(30, 40, 0, 10)).toEqual({ hpPct: 75, shieldPct: 0 });
  });
});
