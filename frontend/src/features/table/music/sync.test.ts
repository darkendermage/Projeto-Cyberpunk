import { describe, expect, it } from 'vitest';
import type { TableMusic } from '../../../api/tableTypes';
import { expectedPosition, musicAction, needsSeek, YT_PAUSED, YT_PLAYING, YT_BUFFERING, YT_CUED } from './sync';

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

describe('musicAction', () => {
  it('pauses a player that kept going after the GM paused', () => expect(musicAction(YT_PLAYING, music({ playing: false }), true)).toBe('pause'));
  it('pauses one that was still loading when the pause came', () => expect(musicAction(YT_BUFFERING, music({ playing: false }), true)).toBe('pause'));
  it('plays a paused or cued player when the table plays', () => {
    expect(musicAction(YT_PAUSED, music({ playing: true }), true)).toBe('play');
    expect(musicAction(YT_CUED, music({ playing: true }), true)).toBe('play');
  });
  it('waits for the click that enables sound', () => expect(musicAction(YT_PAUSED, music({ playing: true }), false)).toBe('none'));
  it('leaves a player that already matches', () => {
    expect(musicAction(YT_PLAYING, music({ playing: true }), true)).toBe('none');
    expect(musicAction(YT_PAUSED, music({ playing: false }), true)).toBe('none');
  });
});
