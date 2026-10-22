import { describe, expect, it } from 'vitest';
import { fitsRegion, HOTSPOTS, humanityLossLabel, LEFT_REGIONS, RIGHT_REGIONS, wearableAccepts } from './anatomy';

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

  it('places every region on exactly one side of the figure', () => {
    const all = [...LEFT_REGIONS, ...RIGHT_REGIONS];
    expect(new Set(all).size).toBe(all.length);
    expect(all.sort()).toEqual(Object.keys(HOTSPOTS).sort());
    for (const key of LEFT_REGIONS) expect(HOTSPOTS[key].panel).toBe('left');
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
