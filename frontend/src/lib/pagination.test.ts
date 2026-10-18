import { describe, expect, it } from 'vitest';
import { pageWindow } from './pagination';

describe('pageWindow', () => {
  it('lists every page when there are few', () => {
    expect(pageWindow(2, 5)).toEqual([1, 2, 3, 4, 5]);
  });

  it('keeps first, last and neighbours with gaps in between', () => {
    expect(pageWindow(10, 20)).toEqual([1, '…', 9, 10, 11, '…', 20]);
  });

  it('expands the edges so the window never looks lopsided', () => {
    expect(pageWindow(1, 9)).toEqual([1, 2, 3, 4, '…', 9]);
    expect(pageWindow(9, 9)).toEqual([1, '…', 6, 7, 8, 9]);
  });
});
