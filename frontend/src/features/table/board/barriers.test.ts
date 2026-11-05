import { describe, expect, it } from 'vitest';
import { barrierLook, barrierState } from './barriers';

describe('barrierLook', () => {
  it('draws each book material with its own pattern', () => {
    expect(barrierLook('steel').pattern).toBe('plates');
    expect(barrierLook('stone').pattern).toBe('blocks');
    expect(barrierLook('concrete').pattern).toBe('blocks');
    expect(barrierLook('wood').pattern).toBe('grain');
    expect(barrierLook('bulletproof-glass').pattern).toBe('glass');
    expect(barrierLook('anything').pattern).toBe('plain');
  });
});

describe('barrierState', () => {
  it('tells how worn a barrier is, and when it is gone', () => {
    expect(barrierState({ hp: 20, maxHp: 20 })).toEqual({ ratio: 1, destroyed: false, text: '20/20 HP' });
    expect(barrierState({ hp: 5, maxHp: 20 })).toMatchObject({ ratio: 0.25, destroyed: false });
    expect(barrierState({ hp: 0, maxHp: 20 })).toMatchObject({ ratio: 0, destroyed: true });
  });
});
