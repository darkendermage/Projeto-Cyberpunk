import { describe, expect, it } from 'vitest';
import {
  calloutBox, firstFilledRegion, fitsRegion, HOTSPOTS, humanityLossLabel, REGION_ORDER, regionEffects, regionSlotsLabel, wearableAccepts,
} from './anatomy';

describe('anatomy', () => {
  it('suggests catalog items by the region selected', () => {
    expect(fitsRegion({ cyberType: 'Neuralware', bodyLocation: 'NervousSystem' }, 'neural')).toBe(true);
    expect(fitsRegion({ cyberType: 'Cyberoptics', bodyLocation: 'Eyes' }, 'eye-left')).toBe(true);
    expect(fitsRegion({ cyberType: 'Cyberoptics', bodyLocation: 'Eyes' }, 'neural')).toBe(false);
    expect(fitsRegion({ cyberType: 'LimbOption', bodyLocation: 'Arm' }, 'arm-right')).toBe(true);
    expect(fitsRegion({ cyberType: 'LimbOption', bodyLocation: 'Leg' }, 'arm-right')).toBe(false);
    expect(fitsRegion({ cyberType: 'LimbOption', bodyLocation: 'Leg' }, 'leg-left')).toBe(true);
    // Hardened Shielding fits an arm or a leg (CP:R p.116).
    expect(fitsRegion({ cyberType: 'LimbOption', bodyLocation: 'FullBody' }, 'arm-left')).toBe(true);
    expect(fitsRegion({ cyberType: 'LimbOption', bodyLocation: 'FullBody' }, 'leg-left')).toBe(true);
    expect(fitsRegion({ cyberType: 'Fashionware', bodyLocation: 'Skin' }, null)).toBe(true);
  });

  it('lists every region of the figure once', () => {
    expect(new Set(REGION_ORDER).size).toBe(REGION_ORDER.length);
    expect([...REGION_ORDER].sort()).toEqual(Object.keys(HOTSPOTS).sort());
  });

  it('shows the preset loss during creation and the dice afterwards', () => {
    expect(humanityLossLabel(7, '2d6', true)).toBe('HL 7');
    expect(humanityLossLabel(7, '2d6', false)).toBe('HL 7 (2d6)');
    expect(humanityLossLabel(0, null, false)).toBe('HL 0');
  });
});

describe('wearable hosts', () => {
  it('maps an option to the kind of worn host that takes it', () => {
    expect(wearableAccepts('Cyberoptics')).toBe('Cybereye');
    expect(wearableAccepts('Cyberaudio')).toBe('Cyberaudio');
    expect(wearableAccepts('LimbOption')).toBe('Cyberarm');
    expect(wearableAccepts('Cyberfinger')).toBe('Cyberarm');
    expect(wearableAccepts('Neuralware')).toBeNull();
  });
});

describe('region list and callout', () => {
  it('shows slots when the region has them, else the piece count', () => {
    expect(regionSlotsLabel({ slotsTotal: 3, slotsUsed: 2, pieces: ['a', 'b'] })).toBe('2/3');
    expect(regionSlotsLabel({ slotsTotal: null, slotsUsed: 0, pieces: ['a'] })).toBe('1');
    expect(regionSlotsLabel({ slotsTotal: null, slotsUsed: 0, pieces: [] })).toBe('—');
  });

  it('opens on the first region with something installed', () => {
    expect(firstFilledRegion([{ key: 'fashion', pieces: ['x'] }, { key: 'audio', pieces: ['y'] }, { key: 'neural', pieces: [] }])).toBe('audio');
    expect(firstFilledRegion([{ key: 'neural', pieces: [] }])).toBe('neural');
  });

  it('collects the effect text of each piece', () => {
    expect(regionEffects([{ name: 'Cybereye', effect: ' Better sight ' }, { name: 'Chyron', effect: null }])).toEqual([{ name: 'Cybereye', effect: 'Better sight' }]);
  });

  it('keeps the callout inside the figure', () => {
    // no room on its own side: flips instead of covering the hotspot
    expect(calloutBox({ x: 292, y: 300, panel: 'right' }, 120)).toEqual({ x: 138, y: 254 });
    expect(calloutBox({ x: 108, y: 300, panel: 'left' }, 120)).toEqual({ x: 142, y: 254 });
    expect(calloutBox({ x: 214, y: 86, panel: 'right' }, 120)).toEqual({ x: 248, y: 40 });
    expect(calloutBox({ x: 200, y: 30, panel: 'left' }, 120)).toEqual({ x: 46, y: 4 });
  });
});
