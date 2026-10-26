// Pure helpers for the NET Architecture page: labels and the tree diagram layout. No game rules here: tables, limits
// and costs all come from the server (GET /api/net-architectures/rules and each Architecture's quote).
import type {
  ArchitectureState, DefenseRule, IceInfo, NetArchitectureRulesDocument, NetBranch, NetContent, NetDifficulty, NetFloor,
} from '../../api/netArchitectureTypes';
import { locale, t, tv } from '../../i18n';

export const DIFFICULTY_TONE: Record<NetDifficulty, 'default' | 'cyan' | 'amber' | 'red'> = {
  Basic: 'default',
  Standard: 'cyan',
  Uncommon: 'amber',
  Advanced: 'red',
};

export const GROUP_LABEL: Record<DefenseRule['group'], string> = {
  Active: t('Defesa ativa (drone)', 'Active defense (drone)'),
  Emplaced: t('Defesa fixa', 'Emplaced defense'),
  Environmental: t('Defesa ambiental', 'Environmental defense'),
};

export interface Lookups {
  ice: Map<string, IceInfo>;
  defenses: Map<string, DefenseRule>;
  devices: Map<string, string>;
  demons: Map<string, string>;
}

export function lookups(rules: NetArchitectureRulesDocument, ice: IceInfo[]): Lookups {
  return {
    ice: new Map(ice.map((i) => [i.slug, i])),
    defenses: new Map(rules.defenses.value.map((d) => [d.id, d])),
    devices: new Map(rules.devices.value.map((d) => [d.id, d.label])),
    demons: new Map(rules.demons.value.map((d) => [d.id, d.name])),
  };
}

/** What a Control Node runs, in a few words. */
export function nodeLabel(content: NetContent, l: Lookups): string {
  const f = content.function;
  if (!f) return t('sem função', 'no function');
  const defense = f.defenseId ? l.defenses.get(f.defenseId) : undefined;
  if (defense) return tv(defense.label) + (f.label ? ` (${tv(f.label)})` : '');
  if (f.deviceId) return tv(f.label ?? l.devices.get(f.deviceId)) ?? f.deviceId;
  return tv(f.label) ?? t('sem função', 'no function');
}

/** One content as the floor list and the diagram show it ("Hellhound", "Password DV8", "Control Node DV8 · Torreta"). */
export function contentLabel(content: NetContent, l: Lookups): string {
  switch (content.kind) {
    case 'BlackIce':
      return l.ice.get(content.slug ?? '')?.name ?? content.slug ?? 'Black ICE';
    case 'Password':
      return `Password DV${content.dv}`;
    case 'File':
      return `File DV${content.dv}`;
    default:
      return `Control Node DV${content.dv} · ${nodeLabel(content, l)}`;
  }
}

/** Short text for a diagram door: Black ICE names, or the feature kind. */
export function doorText(floor: NetFloor, l: Lookups): string {
  if (floor.contents.length === 0) return '—';
  return floor.contents
    .map((c) => (c.kind === 'BlackIce' ? l.ice.get(c.slug ?? '')?.name ?? c.slug : c.kind === 'ControlNode' ? 'Control Node' : c.kind))
    .join(' · ');
}

export const isBossFloor = (floor: NetFloor, state: ArchitectureState) =>
  floor.table !== '' && floor.table !== 'Lobby' && floor.table !== state.difficulty;

/** "Andar 4 (ramo B)" */
export function floorName(floor: NetFloor, state: ArchitectureState): string {
  return state.branches.length > 1 && floor.branchId !== state.branches[0].id ? t(`Andar ${floor.depth} (ramo ${floor.branchId})`, `Floor ${floor.depth} (branch ${floor.branchId})`) : t(`Andar ${floor.depth}`, `Floor ${floor.depth}`);
}

// ---------------------------------------------------------------- diagram

export const DIAGRAM = { colWidth: 210, rowHeight: 74, nodeWidth: 180, nodeHeight: 52, padding: 16 } as const;

export interface DiagramNode {
  floor: NetFloor;
  column: number;
  x: number;
  y: number;
}

export interface DiagramLink {
  key: string;
  points: [number, number][];
}

export interface DiagramLayout {
  width: number;
  height: number;
  nodes: DiagramNode[];
  links: DiagramLink[];
}

/**
 * The elevator as a tree: the main branch is the left column, read top-down from the Lobby to the bottom; each side
 * branch gets its own column and hangs from the main floor it splits after.
 */
export function layoutDiagram(branches: NetBranch[]): DiagramLayout {
  const { colWidth, rowHeight, nodeWidth, nodeHeight, padding } = DIAGRAM;
  const pos = (column: number, depth: number) => ({ x: padding + column * colWidth, y: padding + (depth - 1) * rowHeight });
  const nodes: DiagramNode[] = [];
  const links: DiagramLink[] = [];

  branches.forEach((branch, column) => {
    branch.floors.forEach((floor, i) => {
      const { x, y } = pos(column, floor.depth);
      nodes.push({ floor, column, x, y });
      const cx = x + nodeWidth / 2;
      if (i > 0) {
        links.push({ key: `${floor.id}-up`, points: [[cx, y - (rowHeight - nodeHeight)], [cx, y]] });
      } else if (branch.fromFloor != null) {
        const from = pos(0, branch.fromFloor);
        const startX = from.x + nodeWidth;
        const startY = from.y + nodeHeight / 2;
        links.push({ key: `${floor.id}-split`, points: [[startX, startY], [cx, startY], [cx, y]] });
      }
    });
  });

  const maxDepth = Math.max(1, ...nodes.map((n) => n.floor.depth));
  return {
    width: padding * 2 + (branches.length - 1) * colWidth + nodeWidth,
    height: padding * 2 + (maxDepth - 1) * rowHeight + nodeHeight,
    nodes,
    links,
  };
}

export const formatEbShort = (value: number) => `${value.toLocaleString(locale)}eb`;
