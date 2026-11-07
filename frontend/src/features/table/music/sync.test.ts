import { describe, expect, it } from 'vitest';
import type { TableMusic } from '../../../api/tableTypes';
import { expectedPosition, needsSeek } from './sync';

const music = (change: Partial<TableMusic>): TableMusic => ({
  videoId: 'dQw4w9WgXcQ', playlistId: null, title: null, playing: false, positionSeconds: 30, updatedAt: '2026-11-07T20:00:00Z', volume: 60, index: 0, ...change,
});

describe('expectedPosition', () => {
  const at = Date.parse('2026-11-07T20:00:05Z');
  it('stays put while paused', () => expect(expectedPosition(music({}), at)).toBe(30));
  it('moves with the clock while playing', () => expect(expectedPosition(music({ playing: true }), at)).toBe(35));
});

describe('needsSeek', () => {
  it('seeks past the drift', () => expect(needsSeek(10, 12.5)).toBe(true));
  it('leaves small drifts alone', () => expect(needsSeek(10, 11.9)).toBe(false));
});
