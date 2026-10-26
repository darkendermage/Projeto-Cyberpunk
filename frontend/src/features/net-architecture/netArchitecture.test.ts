import { describe, expect, it } from 'vitest';
import type { NetBranch, NetContent, NetFloor } from '../../api/netArchitectureTypes';
import { contentLabel, DIAGRAM, layoutDiagram, type Lookups } from './netArchitecture';

const floor = (branchId: string, depth: number, contents: NetContent[] = []): NetFloor => ({
  id: `${branchId}${depth}`, branchId, depth, table: 'Standard', roll: null, locked: false, contents,
});

const branch = (id: string, fromFloor: number | null, depths: number[]): NetBranch => ({
  id, fromFloor, floors: depths.map((d) => floor(id, d)),
});

describe('layoutDiagram', () => {
  it('puts the main branch in the first column, one row per depth', () => {
    const layout = layoutDiagram([branch('A', null, [1, 2, 3])]);
    expect(layout.nodes.map((n) => [n.floor.id, n.column, n.y])).toEqual([
      ['A1', 0, DIAGRAM.padding],
      ['A2', 0, DIAGRAM.padding + DIAGRAM.rowHeight],
      ['A3', 0, DIAGRAM.padding + 2 * DIAGRAM.rowHeight],
    ]);
    expect(layout.links).toHaveLength(2);
    expect(layout.width).toBe(DIAGRAM.padding * 2 + DIAGRAM.nodeWidth);
  });

  it('hangs a side branch from the main floor it splits after', () => {
    const layout = layoutDiagram([branch('A', null, [1, 2, 3, 4, 5]), branch('B', 2, [3, 4])]);
    const b3 = layout.nodes.find((n) => n.floor.id === 'B3')!;
    expect(b3.column).toBe(1);
    expect(b3.y).toBe(DIAGRAM.padding + 2 * DIAGRAM.rowHeight);
    const split = layout.links.find((l) => l.key === 'B3-split')!;
    expect(split.points[0]).toEqual([DIAGRAM.padding + DIAGRAM.nodeWidth, DIAGRAM.padding + DIAGRAM.rowHeight + DIAGRAM.nodeHeight / 2]);
    expect(layout.width).toBe(DIAGRAM.padding * 2 + DIAGRAM.colWidth + DIAGRAM.nodeWidth);
  });
});

describe('contentLabel', () => {
  const l: Lookups = {
    ice: new Map([['hellhound', { slug: 'hellhound', name: 'Hellhound' } as never]]),
    defenses: new Map([['automated-turret', { id: 'automated-turret', label: 'Torreta automática' } as never]]),
    devices: new Map([['lighting', 'Iluminação']]),
    demons: new Map(),
  };

  it('names each kind of content', () => {
    expect(contentLabel({ id: '1', kind: 'BlackIce', slug: 'hellhound' }, l)).toBe('Hellhound');
    expect(contentLabel({ id: '2', kind: 'Password', dv: 8 }, l)).toBe('Password DV8');
    expect(contentLabel({ id: '3', kind: 'ControlNode', dv: 10, function: { defenseId: 'automated-turret', loadout: [] } }, l))
      .toBe('Control Node DV10 · Automated turret');
    expect(contentLabel({ id: '4', kind: 'ControlNode', dv: 6, function: { deviceId: 'lighting', loadout: [] } }, l))
      .toBe('Control Node DV6 · Lighting');
  });
});
