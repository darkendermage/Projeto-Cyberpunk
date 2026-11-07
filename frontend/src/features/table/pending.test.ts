import { describe, expect, it } from 'vitest';
import type { PendingView } from '../../api/encounterTypes';
import { pendingPrompt } from './pending';

const shot: PendingView = {
  seq: 1, attackerId: 'G', attackerName: 'Ganger', weapon: 'Heavy Pistol', targetId: 'P', targetName: 'V', step: 'Defense',
  rangeDv: 13, evasion: 14, attackerCharacterId: null, targetCharacterId: 7,
};

describe('pendingPrompt', () => {
  it('lets the defending player choose', () => expect(pendingPrompt(shot, true, false)!.actions).toEqual(['dodge', 'noDodge']));
  it('shows nothing to a player who does not answer', () => expect(pendingPrompt(shot, false, false)).toBeNull());
  it('gives the GM the step and the cancel', () => expect(pendingPrompt(shot, false, true)!.actions).toEqual(['dodge', 'noDodge', 'cancel']));
  it('asks the attacker to roll the Critical Injury', () =>
    expect(pendingPrompt({ ...shot, step: 'CriticalInjury', rangeDv: null, evasion: null }, true, false)!.actions).toEqual(['critical']));
});
