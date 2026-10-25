import { useState } from 'react';
import type { DefenseRule, NetArchitectureDetail, NetArchitectureRules, NetContent, NetFloor, TableRow } from '../../api/netArchitectureTypes';
import { Badge, Button, Checkbox, Modal, Select } from '../../components/ui/ui';
import { contentLabel, floorName, formatEbShort, GROUP_LABEL, type Lookups } from './netArchitecture';
import styles from './NetArchitecture.module.css';

type Run = (path: string, body?: Record<string, unknown>, method?: 'POST' | 'PUT', done?: string) => void;

/**
 * Edits one floor: pick a row of its table (or of the next Difficulty Rating up, as a Boss Floor), swap a Black ICE for
 * another of the same cost, and set what each Control Node runs. The server checks every rule.
 */
export function FloorEditor({ floor, detail, rules, lookups, busy, run, onClose }: {
  floor: NetFloor | null; detail: NetArchitectureDetail; rules: NetArchitectureRules; lookups: Lookups; busy: boolean; run: Run; onClose: () => void;
}) {
  return (
    <Modal open={floor != null} onClose={onClose} title={floor ? `${floorName(floor, detail.state)} · ${floor.id}` : ''} wide>
      {floor && <Editor key={`${floor.id}-${detail.version}`} floor={floor} detail={detail} rules={rules} lookups={lookups} busy={busy} run={run} />}
    </Modal>
  );
}

function Editor({ floor, detail, rules, lookups, busy, run }: {
  floor: NetFloor; detail: NetArchitectureDetail; rules: NetArchitectureRules; lookups: Lookups; busy: boolean; run: Run;
}) {
  const r = rules.rules;
  const lobby = floor.table === 'Lobby';
  const order = r.difficulties.value.map((d) => d.id);
  const own = detail.state.difficulty;
  const harder = order[order.indexOf(own) + 1];
  const tables = lobby
    ? [{ id: 'Lobby', label: `Lobby (${r.lobbyTable.source})`, rows: r.lobbyTable.value }]
    : [own, harder].filter(Boolean).map((d) => ({
      id: d!,
      label: d === own ? `Tabela ${r.difficulties.value.find((x) => x.id === d)!.label}` : `Boss Floor: ${r.difficulties.value.find((x) => x.id === d)!.label}`,
      rows: r.bodyTables.value.find((t) => t.difficulty === d)!.rows,
    }));
  const [table, setTable] = useState(floor.table && tables.some((t) => t.id === floor.table) ? floor.table : tables[0].id);
  const rows = tables.find((t) => t.id === table)!.rows;

  return (
    <div className={styles.dialog}>
      <section>
        <h3 className={styles.label}>Conteúdo atual</h3>
        {floor.contents.length === 0 && <p className={styles.muted}>Andar vazio.</p>}
        {floor.contents.map((c) => (
          <ContentEditor key={c.id} content={c} detail={detail} rules={rules} lookups={lookups} busy={busy} run={run} />
        ))}
      </section>

      <section>
        <h3 className={styles.label}>Trocar pela linha de uma tabela</h3>
        {tables.length > 1 && (
          <div className={styles.row}>
            <Select aria-label="Tabela" value={table} onChange={(e) => setTable(e.target.value)}>
              {tables.map((t) => <option key={t.id} value={t.id}>{t.label}</option>)}
            </Select>
            <span className={styles.source}>{table === own ? r.bodyTables.source : r.bossFloor.source}</span>
          </div>
        )}
        <div className={styles.rowPicker}>
          {rows.map((row) => (
            <button key={row.roll} type="button" className={styles.rowOption} disabled={busy}
              aria-pressed={floor.table === table && floor.roll === row.roll}
              onClick={() => run(`floors/${floor.id}/set`, { table, roll: row.roll }, 'POST', `${floor.id}: linha ${row.roll}.`)}>
              <span className={styles.rollNumber}>{row.roll}</span>
              <span>{rowLabel(row, lookups)}</span>
            </button>
          ))}
        </div>
      </section>
    </div>
  );
}

const rowLabel = (row: TableRow, l: Lookups) =>
  row.contents.map((c) => (c.kind === 'BlackIce' ? l.ice.get(c.slug ?? '')?.name ?? c.slug : `${c.kind === 'ControlNode' ? 'Control Node' : c.kind} DV${c.dv}`)).join(', ');

function ContentEditor({ content, detail, rules, lookups, busy, run }: {
  content: NetContent; detail: NetArchitectureDetail; rules: NetArchitectureRules; lookups: Lookups; busy: boolean; run: Run;
}) {
  if (content.kind === 'BlackIce') {
    const current = lookups.ice.get(content.slug ?? '');
    const same = rules.ice.filter((i) => i.price === current?.price);
    return (
      <div className={styles.contentEditor}>
        <span className={styles.kind}>{contentLabel(content, lookups)}</span>
        <Select aria-label="Trocar Black ICE" value={content.slug ?? ''} disabled={busy}
          onChange={(e) => run(`contents/${content.id}/ice`, { slug: e.target.value }, 'POST', 'Black ICE trocada.')}>
          {same.map((i) => <option key={i.slug} value={i.slug}>{i.name} ({i.class === 'AntiProgram' ? 'Anti-Program' : 'Anti-Personnel'})</option>)}
        </Select>
        <span className={styles.source}>Só Programs de mesmo custo ({current?.price != null ? formatEbShort(current.price) : '?'}): {rules.rules.sameCostSwap.source}</span>
      </div>
    );
  }
  if (content.kind !== 'ControlNode') {
    return (
      <div className={styles.contentEditor}>
        <span className={styles.kind}>{contentLabel(content, lookups)}</span>
        {content.fileIdea && <span className={styles.stats}>{content.fileIdea}</span>}
      </div>
    );
  }
  return <NodeEditor content={content} detail={detail} rules={rules} lookups={lookups} busy={busy} run={run} />;
}

function NodeEditor({ content, detail, rules, lookups, busy, run }: {
  content: NetContent; detail: NetArchitectureDetail; rules: NetArchitectureRules; lookups: Lookups; busy: boolean; run: Run;
}) {
  const f = content.function;
  const [choice, setChoice] = useState(f?.defenseId ? `d:${f.defenseId}` : f?.deviceId ? `v:${f.deviceId}` : '');
  const defense: DefenseRule | undefined = choice.startsWith('d:') ? lookups.defenses.get(choice.slice(2)) : undefined;
  const [loadout, setLoadout] = useState<string[]>(f?.defenseId === defense?.id ? f?.loadout ?? [] : []);
  const [demonId, setDemonId] = useState(f?.demonId ?? '');
  const [label, setLabel] = useState(f?.label ?? '');
  const demons = detail.state.demons;
  const groups = (['Active', 'Emplaced', 'Environmental'] as const).map((g) => ({ g, items: rules.rules.defenses.value.filter((d) => d.group === g) }));

  const pick = (value: string) => {
    setChoice(value);
    const next = value.startsWith('d:') ? lookups.defenses.get(value.slice(2)) : undefined;
    setLoadout(next ? next.loadoutOptions.slice(0, next.loadoutPicks) : []);
    if (next?.requiresDemon && !demonId && demons[0]) setDemonId(demons[0].id);
  };

  const toggle = (option: string) =>
    setLoadout((l) => (l.includes(option) ? l.filter((x) => x !== option) : [...l, option].slice(-(defense?.loadoutPicks ?? 0))));

  const save = () => run(`contents/${content.id}/node`, {
    defenseId: defense?.id ?? null,
    loadout,
    deviceId: choice.startsWith('v:') ? choice.slice(2) : null,
    label: label.trim() || null,
    demonId: demonId || null,
  }, 'POST', 'Control Node atualizado.');

  return (
    <div className={styles.contentEditor}>
      <span className={styles.kind}>Control Node DV{content.dv}</span>
      <div className={styles.row}>
        <Select aria-label="Função do Control Node" value={choice} onChange={(e) => pick(e.target.value)}>
          <option value="">Só descrição livre</option>
          {groups.map(({ g, items }) => (
            <optgroup key={g} label={GROUP_LABEL[g]}>
              {items.map((d) => <option key={d.id} value={`d:${d.id}`}>{d.label} ({d.name})</option>)}
            </optgroup>
          ))}
          <optgroup label="Dispositivo civil (CP:R p.199, p.210)">
            {rules.rules.devices.value.map((d) => <option key={d.id} value={`v:${d.id}`}>{d.label}</option>)}
          </optgroup>
        </Select>
        <Select aria-label="Demon que opera" value={demonId} onChange={(e) => setDemonId(e.target.value)}>
          <option value="">Sem Demon</option>
          {demons.map((d) => <option key={d.id} value={d.id}>{lookups.demons.get(d.type) ?? d.type} ({d.id})</option>)}
        </Select>
      </div>
      {defense && (
        <div className={styles.defenseInfo}>
          <span className={styles.row}>
            <Badge tone={defense.requiresDemon ? 'red' : 'default'}>{GROUP_LABEL[defense.group]}</Badge>
            <span className={styles.source}>CP:R p.{defense.page} · neutralizar: DV{defense.counterDv} ({defense.counterTime})</span>
          </span>
          <span className={styles.stats}>{defense.description}</span>
          <span className={styles.stats}><strong>Gatilho:</strong> {defense.trigger} · {defense.data}</span>
          {defense.requiresDemon && demons.length === 0 && <span className={styles.warn}>Precisa de um Demon: adicione um no painel de Demons.</span>}
          {defense.loadoutPicks > 0 && (
            <>
              <span className={styles.label}>Armamento: escolha {defense.loadoutPicks}</span>
              {defense.loadoutOptions.map((o) => (
                <Checkbox key={o} checked={loadout.includes(o)} onChange={() => toggle(o)} label={o} />
              ))}
            </>
          )}
        </div>
      )}
      <input aria-label="Descrição livre" value={label} maxLength={120} onChange={(e) => setLabel(e.target.value)}
        placeholder="Detalhe opcional: 'portas do cofre', 'câmeras do saguão'…" />
      <div className={styles.row}>
        <Button variant="primary" disabled={busy || (!choice && !label.trim())} onClick={save}>Salvar Control Node</Button>
      </div>
    </div>
  );
}
