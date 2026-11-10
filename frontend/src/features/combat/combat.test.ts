import { describe, expect, it } from 'vitest';
import type { WeaponView } from '../../api/encounterTypes';
import { diceCount, expectedDiceFor, extraMagazines, isArea, modesFor, needsRange, parseDice, pickMode, reloadsMain, switchedModes, usableModes, weaponName } from './combat';

const weapon = (patch: Partial<WeaponView>): WeaponView => ({
  key: 'w', name: 'w', class: 'HeavyPistol', skill: 'handgun', attackTotal: 12, autofireTotal: null, damage: '3d6', rof: 2,
  rounds: 8, magazine: 8, autofireMultiplier: null, melee: false, halvesArmor: false, ...patch,
});

describe('modesFor', () => {
  it('offers autofire and suppressive fire only with an Autofire value', () => {
    expect(modesFor(weapon({}))).not.toContain('Autofire');
    expect(modesFor(weapon({ class: 'AssaultRifle', autofireMultiplier: 4 }))).toEqual(expect.arrayContaining(['Autofire', 'Suppressive']));
  });

  it('offers only Autofire and Suppressive Fire to an Autofire-only weapon, Autofire first', () => {
    const modes = modesFor(weapon({ class: 'MachineGun', autofireMultiplier: 5, autofireOnly: true }));
    expect(modes[0]).toBe('Autofire');
    expect(modes).toEqual(['Autofire', 'Suppressive']);
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
    expect(diceCount('5d6 / 3d6')).toBe(5);
  });
});

describe('weapon-rules.json traits', () => {
  it('drops single and aimed shots from shell-only weapons and adds shells to them', () => {
    const crusher = weapon({ class: 'VeryHeavyPistol', traits: { shells: 'only' } });
    expect(modesFor(crusher)).toEqual(['ShotgunShell']);
    expect(modesFor(weapon({ class: 'Shotgun', traits: { shells: 'none' } }))).not.toContain('ShotgunShell');
  });

  it('honors no aimed shots and an explosive mode', () => {
    expect(modesFor(weapon({ traits: { noAimed: true } }))).toEqual(['Single']);
    expect(modesFor(weapon({ class: 'AssaultRifle', traits: { explosive: true } }))).toContain('Explosive');
  });

  it('counts the dice of the mode in use', () => {
    expect(expectedDiceFor(weapon({ damage: '5d6' }), 'Single')).toBe(5);
    expect(expectedDiceFor(weapon({ traits: { shellDamage: '4d6' } }), 'ShotgunShell')).toBe(4);
    expect(expectedDiceFor(weapon({ traits: { effect: 'Cybertech DV15' } }), 'Single')).toBe(0);
    expect(expectedDiceFor(weapon({ traits: { aimedDamage: '4d6' } }), 'AimedHead')).toBe(4);
    expect(expectedDiceFor(null, 'Single', '2d6')).toBe(2);
  });

  it('lists free modes plus the switched one that is on', () => {
    const normal = weapon({ modeId: 'normal', modeLabel: 'Normal', modeSwitch: 'action', modeActive: true });
    const burst = weapon({ modeId: 'hypurrburst', modeLabel: 'Hypurrburst', modeSwitch: 'action', modeActive: false, damage: '4d6' });
    const throwIt = weapon({ modeId: 'throw', modeLabel: 'Arremessar', modeSwitch: 'free', modeActive: true });
    const cutie = { ...normal, modes: [normal, burst, throwIt] };
    expect(usableModes(cutie).map((m) => m.modeId)).toEqual(['normal', 'throw']);
    expect(switchedModes(cutie).map((m) => m.modeId)).toEqual(['normal', 'hypurrburst']);
    expect(pickMode(cutie, 'hypurrburst').damage).toBe('4d6');
    expect(pickMode(cutie, 'nope')).toBe(cutie);
    expect(weaponName(cutie)).toBe('w [Normal]');
  });

  it('reloads the main magazine and each extra one apart', () => {
    const tommy = weapon({ magazines: [{ id: null, label: 'Pente', rounds: 8, capacity: 8, refill: false }, { id: 'shotgun', label: 'Escopeta', rounds: 0, capacity: 2, refill: false }] });
    expect(extraMagazines(tommy).map((m) => m.id)).toEqual(['shotgun']);
    expect(reloadsMain(tommy)).toBe(true);
    expect(reloadsMain(weapon({ melee: true, magazine: null, rounds: null }))).toBe(false);
    expect(reloadsMain(weapon({ melee: true, traits: { meleeCharges: true } }))).toBe(true);
  });
});
