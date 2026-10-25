import { describe, expect, it } from 'vitest';
import type { WeaponView } from '../../api/encounterTypes';
import { diceCount, isArea, modesFor, needsRange, parseDice } from './combat';

const weapon = (patch: Partial<WeaponView>): WeaponView => ({
  key: 'w', name: 'w', class: 'HeavyPistol', skill: 'handgun', attackTotal: 12, autofireTotal: null, damage: '3d6', rof: 2,
  rounds: 8, magazine: 8, autofireMultiplier: null, melee: false, halvesArmor: false, ...patch,
});

describe('modesFor', () => {
  it('offers autofire and suppressive fire only with an Autofire value', () => {
    expect(modesFor(weapon({}))).not.toContain('Autofire');
    expect(modesFor(weapon({ class: 'AssaultRifle', autofireMultiplier: 4 }))).toEqual(expect.arrayContaining(['Autofire', 'Suppressive']));
  });

  it('offers shells to shotguns and area damage to launchers and thrown objects', () => {
    expect(modesFor(weapon({ class: 'Shotgun' }))).toContain('ShotgunShell');
    expect(modesFor(weapon({ class: 'GrenadeLauncher' }))).toContain('Explosive');
    expect(modesFor(null, true)).toEqual(['Single', 'Explosive']);
  });

  it('lets brawling attack without aiming', () => {
    expect(modesFor(weapon({ melee: true, skill: 'brawling', class: null }))).toEqual(['Single']);
  });
});

describe('attack form helpers', () => {
  it('knows which modes need a range and hit an area', () => {
    expect(needsRange(weapon({}), 'Single', false)).toBe(true);
    expect(needsRange(weapon({ melee: true }), 'Single', false)).toBe(false);
    expect(needsRange(weapon({ class: 'Shotgun' }), 'ShotgunShell', false)).toBe(false);
    expect(isArea('Explosive')).toBe(true);
    expect(isArea('AimedHead')).toBe(false);
  });

  it('parses typed dice', () => {
    expect(parseDice('5 5, 6')).toEqual([5, 5, 6]);
    expect(parseDice('')).toBeNull();
    expect(parseDice('5 x')).toBeNull();
    expect(diceCount('4d6')).toBe(4);
  });
});
