import { describe, expect, it } from 'vitest';
import { Vector3 } from 'three';
import type { TableMessage } from '../../../api/tableTypes';
import { inkFor, throwOf } from './dice';
import { D6_VALUES, d10Normal, d10Shape, d6Normal, faceNormal, faceUp, UP } from './faces';

const message = (change: Partial<TableMessage>): TableMessage => ({
  id: 1, at: '', authorSeatId: null, authorName: 'GM', authorColor: null, kind: 'Roll', visibility: 'Public', targetSeatId: null,
  text: 'Ataque', roll: null, concealed: false, ...change,
});

describe('throwOf', () => {
  it('throws the d6 and d10 of a roll, with the critical extra d10', () => {
    const m = message({
      roll: { formula: '1d10+12', kind: 'Check', total: 31, critical: 'Success', criticalInjury: false,
        terms: [{ expression: '1d10', sign: 1, dice: [10], extra: 9, value: 19 }, { expression: '12', sign: 1, dice: [], extra: null, value: 12 }] },
    });
    expect(throwOf(m)).toMatchObject({ total: 31, critical: 'Success', dice: [{ sides: 10, value: 10 }, { sides: 10, value: 9 }] });
  });

  it('throws nothing for a concealed roll or dice the tray has no model for', () => {
    expect(throwOf(message({ concealed: true }))).toBeNull();
    const d100 = message({ roll: { formula: '1d100', kind: 'Plain', total: 42, critical: null, criticalInjury: false, terms: [{ expression: '1d100', sign: 1, dice: [42], extra: null, value: 42 }] } });
    expect(throwOf(d100)).toBeNull();
  });

  it('throws an attack: its d10, then the damage dice the reader may see', () => {
    const attack = {
      encounterId: 1, seq: 1, attackerId: 'C1', attackerName: 'Kaze', attackerPc: true, weapon: 'Rifle', mode: 'Single' as const, melee: false,
      attackRoll: 7, attackTotal: 21, undone: false,
      targets: [{ id: 'C2', name: 'Ganger', pc: false, hit: true, damage: 12, through: 5, location: 'corpo', dv: 15, defenseRoll: null, damageDice: [4, 6, 2], criticalInjury: null }],
    };
    expect(throwOf(message({ kind: 'Attack', attack }))!.dice).toEqual([{ sides: 10, value: 7 }, { sides: 6, value: 4 }, { sides: 6, value: 6 }, { sides: 6, value: 2 }]);
    expect(throwOf(message({ kind: 'Attack', attack: { ...attack, targets: [{ ...attack.targets[0], damageDice: [] }] } }))!.dice).toHaveLength(1);
    expect(throwOf(message({ kind: 'Attack', attack: { ...attack, undone: true } }))).toBeNull();
    expect(throwOf(message({ kind: 'Attack', attack: { ...attack, attackRoll: 10, attackExtra: 6 } }))!.dice.slice(0, 2)).toEqual([{ sides: 10, value: 10 }, { sides: 10, value: 6 }]);
  });
});

describe('faces', () => {
  const close = (a: Vector3, b: Vector3) => expect(a.distanceTo(b)).toBeLessThan(1e-6);

  it('turns every d6 value up', () => {
    for (const value of D6_VALUES) close(d6Normal(value).applyQuaternion(faceUp(d6Normal(value), 1.2)), UP);
    expect(d6Normal(1).dot(d6Normal(6))).toBeCloseTo(-1);
  });

  it('builds a d10 of ten flat kites, each value once, opposite faces adding to 11', () => {
    const shape = d10Shape();
    expect(shape.faces).toHaveLength(10);
    expect([...shape.values].sort((a, b) => a - b)).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
    for (let f = 0; f < 10; f++) {
      const n = faceNormal(shape, f);
      const [a, ...rest] = shape.faces[f].map((i) => shape.vertices[i]);
      for (const p of rest) expect(Math.abs(n.dot(new Vector3().subVectors(p, a)))).toBeLessThan(1e-9);
    }
    for (let v = 1; v <= 10; v++) expect(d10Normal(shape, v).dot(d10Normal(shape, 11 - v))).toBeLessThan(-0.5);
  });

  it('turns every d10 value up', () => {
    const shape = d10Shape();
    for (let v = 1; v <= 10; v++) close(d10Normal(shape, v).applyQuaternion(faceUp(d10Normal(shape, v), v)), UP);
  });
});

describe('upright numbers', () => {
  it('stands each number upright on screen after turning its face up', async () => {
    const { faceUpright, SCREEN_UP, d10TextureUp } = await import('./faces');
    const shape = d10Shape();
    for (let v = 1; v <= 10; v++) {
      const q = faceUpright(d10Normal(shape, v), d10TextureUp(shape, v));
      expect(d10Normal(shape, v).applyQuaternion(q).distanceTo(UP)).toBeLessThan(1e-6);
      const up = d10TextureUp(shape, v).applyQuaternion(q);
      up.y = 0;
      expect(up.normalize().distanceTo(SCREEN_UP)).toBeLessThan(1e-6);
    }
  });
});

describe('dice color', () => {
  it('picks dark numbers on light dice and light ones on dark dice', () => {
    expect(inkFor('#ffffff')).toBe('#0b0d10');
    expect(inkFor('#101010')).toBe('#f4f6f8');
  });

  it('passes the roller\'s color to the tray', () => {
    const m = message({ roll: { formula: '1d6', kind: 'Plain', total: 4, critical: null, criticalInjury: false, diceColor: '#12ab34',
      terms: [{ expression: '1d6', sign: 1, dice: [4], extra: null, value: 4 }] } });
    expect(throwOf(m)!.color).toBe('#12ab34');
  });
});
