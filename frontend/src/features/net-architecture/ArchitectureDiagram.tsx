import type { Ref } from 'react';
import type { NetArchitectureDetail } from '../../api/netArchitectureTypes';
import { DIAGRAM, doorText, isBossFloor, layoutDiagram, type Lookups } from './netArchitecture';
import styles from './NetArchitecture.module.css';
import { t, tv } from '../../i18n';

// Literal token colors (styles/tokens.css): the exported .svg has no stylesheet.
const KIND_COLOR: Record<string, string> = {
  BlackIce: '#ff2a3d',
  Password: '#ffcf3a',
  File: '#3de0ff',
  ControlNode: '#3dff9a',
};

/**
 * The elevator as a tree (SVG): each floor is a "door"; the stripe on its left shows what is behind it (red Black ICE,
 * amber Password, cyan File, green Control Node). The Lobby, the bottom and Boss Floors are marked, and the Demons and
 * Watchers sit on their floors. Self-contained styles, so the exported .svg looks the same.
 */
export function ArchitectureDiagram({ detail, lookups, selected, onSelect, ref }: {
  detail: NetArchitectureDetail;
  lookups: Lookups;
  selected: string | null;
  onSelect: (floorId: string) => void;
  ref?: Ref<SVGSVGElement>;
}) {
  const { state } = detail;
  const layout = layoutDiagram(state.branches);
  const { nodeWidth, nodeHeight } = DIAGRAM;
  const watchersAt = (floorId: string) => detail.watchers.filter((w) => w.floorId === floorId);

  return (
    <div className={styles.diagramWrap}>
      <svg ref={ref} xmlns="http://www.w3.org/2000/svg" width={layout.width} height={layout.height} viewBox={`0 0 ${layout.width} ${layout.height}`}
        className={styles.diagram} role="img" aria-label={t(`Diagrama da Arquitetura ${tv(detail.name)}`, `Diagram of the Architecture ${tv(detail.name)}`)}
        style={{ fontFamily: "'Rajdhani', 'Segoe UI', sans-serif" }}>
        <rect width={layout.width} height={layout.height} fill="#0b0d12" />
        {layout.links.map((l) => (
          <polyline key={l.key} points={l.points.map((p) => p.join(',')).join(' ')} fill="none" stroke="#3de0ff" strokeOpacity={0.55} strokeWidth={2} />
        ))}
        {layout.nodes.map(({ floor, x, y }) => {
          const lobby = floor.table === 'Lobby';
          const bottom = floor.id === detail.bottomFloorId;
          const boss = isBossFloor(floor, state);
          const watchers = watchersAt(floor.id);
          const kinds = [...new Set(floor.contents.map((c) => tv(c.kind)))];
          const isSelected = floor.id === selected;
          return (
            <g key={floor.id} transform={`translate(${x},${y})`} className={styles.diagramNode} onClick={() => onSelect(floor.id)}
              role="button" tabIndex={0} aria-label={`${floor.id}: ${doorText(floor, lookups)}`}
              onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onSelect(floor.id); } }}>
              <rect width={nodeWidth} height={nodeHeight} rx={3} fill={bottom ? '#1d1016' : '#12151c'}
                stroke={isSelected ? '#ffffff' : bottom ? '#ff2a3d' : boss ? '#ffcf3a' : '#2a2f3a'} strokeWidth={isSelected ? 2 : 1.2} />
              {kinds.map((k, i) => (
                <rect key={k} x={0} y={(nodeHeight / kinds.length) * i} width={5} height={nodeHeight / kinds.length} fill={KIND_COLOR[k]} />
              ))}
              <text x={12} y={17} fontSize={11} fill="#7f8794" letterSpacing="0.08em">
                {floor.id}{lobby ? ' · LOBBY' : ''}{bottom ? ' · FUNDO' : ''}{boss ? ' · BOSS' : ''}{floor.locked ? ' · 🔒' : ''}
              </text>
              <text x={12} y={35} fontSize={13.5} fill="#e9edf2">{truncate(doorText(floor, lookups), 24)}</text>
              {watchers.length > 0 && (
                <text x={12} y={48} fontSize={10.5} fill="#ffcf3a">{truncate(watchers.map((w) => tv(w.name)).join(', '), 30)}</text>
              )}
            </g>
          );
        })}
      </svg>
    </div>
  );
}

const truncate = (text: string, max: number) => (text.length > max ? `${text.slice(0, max - 1)}…` : text);
