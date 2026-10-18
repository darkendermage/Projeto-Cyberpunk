import { describe, expect, it } from 'vitest';
import {
  AUTOFIRE, bandIndexFor, bandInTiles, bandLabel, METERS_PER_TILE, RANGE_BANDS, rangeRowsFor, SINGLE_SHOT,
} from './rangeTable';

describe('range bands in tiles', () => {
  it('converts the book bands to 2 m tiles', () => {
    expect(RANGE_BANDS.map((_, i) => bandLabel(i, 'tiles')))
      .toEqual(['0–3', '4–6', '7–12', '13–25', '26–50', '51–100', '101–200', '201–400']);
    expect(bandLabel(2, 'meters')).toBe('13–25');
  });

  it('puts every tile in exactly one band, the same band as its distance in meters', () => {
    for (let tiles = 0; tiles <= 400; tiles++) {
      const meters = tiles * METERS_PER_TILE;
      expect(bandIndexFor(tiles, 'tiles')).toBe(bandIndexFor(meters, 'meters'));
      expect(RANGE_BANDS.filter((_, i) => tiles >= bandInTiles(i).min && tiles <= bandInTiles(i).max)).toHaveLength(1);
    }
  });

  it('has a DV or N/A for every band', () => {
    for (const row of SINGLE_SHOT) expect(row.dv).toHaveLength(RANGE_BANDS.length);
    for (const row of AUTOFIRE) expect(row.dv).toHaveLength(5);
  });
});

describe('rangeRowsFor', () => {
  it('maps weapon classes to the book rows', () => {
    expect(rangeRowsFor('HeavyPistol').single?.dv[0]).toBe(13);
    expect(rangeRowsFor('Smg').autofire?.dv).toEqual([20, 17, 20, 25, 30]);
    expect(rangeRowsFor('Crossbow').single?.key).toBe('bow');
    expect(rangeRowsFor('Shotgun').autofire).toBeUndefined();
    expect(rangeRowsFor('MediumMelee')).toEqual({});
  });
});
