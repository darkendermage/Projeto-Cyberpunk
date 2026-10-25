import { useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { ArrowLeft, Copy, Dices, Download, FileText, Lock, LockOpen, Pencil, Plus, RefreshCw, Trash2 } from 'lucide-react';
import clsx from 'clsx';
import { problemMessage } from '../../api/characters';
import { netApi, useArchitecture, useNetCommand } from '../../api/netArchitecture';
import type { NetArchitectureDetail, NetArchitectureRules, NetFloor } from '../../api/netArchitectureTypes';
import { Badge, Button, ErrorState, IconButton, Modal, NumberInput, Select, Spinner } from '../../components/ui/ui';
import { downloadJson } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import page from '../../styles/Page.module.css';
import { ArchitectureDiagram } from './ArchitectureDiagram';
import { FloorEditor } from './FloorEditor';
import { contentLabel, DIFFICULTY_TONE, formatEbShort, isBossFloor, lookups as buildLookups, type Lookups } from './netArchitecture';
import styles from './NetArchitecture.module.css';

export function ArchitectureView({ id, rules }: { id: number; rules: NetArchitectureRules }) {
  const query = useArchitecture(id);
  if (query.isLoading) return <div className={page.page}><Spinner /></div>;
  if (query.error || !query.data) return <div className={page.page}><ErrorState error={query.error} onRetry={() => query.refetch()} /></div>;
  return <Loaded detail={query.data} rules={rules} />;
}

function Loaded({ detail, rules }: { detail: NetArchitectureDetail; rules: NetArchitectureRules }) {
  const toast = useToast();
  const navigate = useNavigate();
  const command = useNetCommand(detail.id);
  const svg = useRef<SVGSVGElement>(null);
  const [selected, setSelected] = useState<string | null>(null);
  const [renaming, setRenaming] = useState(false);
  const l = useMemo(() => buildLookups(rules.rules, [...rules.ice, ...detail.ice]), [rules, detail.ice]);
  const { state } = detail;
  const floor = selected ? state.branches.flatMap((b) => b.floors).find((f) => f.id === selected) ?? null : null;

  const run = (path: string, body: Record<string, unknown> = {}, method?: 'POST' | 'PUT', done?: string) =>
    command.mutate({ path, body, method }, {
      onSuccess: () => { if (done) toast.notify(done); },
      onError: (error) => toast.notify(problemMessage(error), 'error'),
    });

  const copyText = async (format: 'text' | 'markdown') => {
    try {
      await navigator.clipboard.writeText(await netApi.exportText(detail.id, format));
      toast.notify(format === 'text' ? 'Tabela copiada.' : 'Markdown copiado.');
    } catch (error) {
      toast.notify(problemMessage(error, 'Não deu para copiar.'), 'error');
    }
  };

  const downloadSvg = () => {
    if (!svg.current) return;
    const blob = new Blob([new XMLSerializer().serializeToString(svg.current)], { type: 'image/svg+xml' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${detail.name}.svg`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  };

  const remove = async () => {
    if (!window.confirm(`Excluir "${detail.name}"? Esta ação não pode ser desfeita.`)) return;
    try {
      await netApi.remove(detail.id);
      toast.notify('Arquitetura excluída.');
      navigate('/net-architecture');
    } catch (error) {
      toast.notify(problemMessage(error), 'error');
    }
  };

  return (
    <div className={page.page}>
      <Link to="/net-architecture" className={styles.back}><ArrowLeft size={14} /> Arquiteturas</Link>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>{detail.name}</h1>
          <span className={styles.row}>
            <Badge tone={DIFFICULTY_TONE[state.difficulty]}>{detail.difficulty.label} · DV{detail.difficulty.dv}</Badge>
            <span className={styles.muted}>
              {detail.location.label} · {state.branches.reduce((n, b) => n + b.floors.length, 0)} andares
              {state.branches.length > 1 ? ` · ${state.branches.length - 1} ramo(s)` : ''} · seed {state.seed}
            </span>
          </span>
          <span className={styles.source}>
            Netrunner com chance: Interface {detail.difficulty.fightingChance}+
            {detail.difficulty.mightDie != null ? ` · pode morrer antes do fundo com Interface ${detail.difficulty.mightDie} ou menos` : ''}
          </span>
        </div>
        <div className={styles.actions}>
          <Button icon={<Pencil size={15} />} onClick={() => setRenaming(true)}>Nome e notas</Button>
          <Button icon={<RefreshCw size={15} />} disabled={command.isPending}
            onClick={() => run('regenerate', { keepShape: true }, 'POST', 'Andares destravados rolados de novo.')}>Rolar andares</Button>
          <Button icon={<Dices size={15} />} disabled={command.isPending}
            onClick={() => window.confirm('Gerar uma Arquitetura nova no lugar desta (mesma dificuldade e local)?') && run('regenerate', { keepShape: false }, 'POST', 'Arquitetura gerada de novo.')}>
            Gerar outra
          </Button>
          <Button variant="ghost" icon={<Trash2 size={15} />} onClick={remove}>Excluir</Button>
        </div>
      </header>

      <div className={styles.layout}>
        <section className={styles.panel}>
          <h2 className={styles.panelTitle}>Mapa</h2>
          <p className={styles.muted}>Clique num andar para editar. Faixa: <span className={styles.ice}>Black ICE</span> · <span className={styles.pwd}>Password</span> ·{' '}
            <span className={styles.file}>File</span> · <span className={styles.node}>Control Node</span></p>
          <ArchitectureDiagram ref={svg} detail={detail} lookups={l} selected={selected} onSelect={setSelected} />
          <div className={styles.row}>
            <Button icon={<Copy size={15} />} onClick={() => copyText('text')}>Copiar tabela</Button>
            <Button icon={<FileText size={15} />} onClick={() => copyText('markdown')}>Copiar Markdown</Button>
            <a className={styles.linkButton} href={netApi.exportUrl(detail.id, 'text')} download><Download size={14} /> .txt</a>
            <a className={styles.linkButton} href={netApi.exportUrl(detail.id, 'markdown')} download><Download size={14} /> .md</a>
            <Button variant="ghost" icon={<Download size={15} />} onClick={downloadSvg}>Diagrama .svg</Button>
            <Button variant="ghost" icon={<Download size={15} />} onClick={() => downloadJson(`${detail.name}.json`, detail)}>.json</Button>
          </div>
        </section>

        <aside className={styles.aside}>
          <CostPanel detail={detail} />
          <DemonsPanel detail={detail} rules={rules} run={run} busy={command.isPending} />
          <WatchersPanel detail={detail} rules={rules} run={run} busy={command.isPending} />
        </aside>
      </div>

      {state.branches.map((branch) => (
        <section key={branch.id} className={styles.panel}>
          <h2 className={styles.panelTitle}>
            {branch.fromFloor == null ? `Tronco ${branch.id}` : `Ramo ${branch.id} · sai depois do andar ${branch.fromFloor}`}
          </h2>
          <div className={page.tableWrap}>
            <table className={page.table}>
              <thead><tr><th>Andar</th><th>Do outro lado da “porta”</th><th>DV</th><th><span className="visually-hidden">Ações</span></th></tr></thead>
              <tbody>
                {branch.floors.map((f) => (
                  <FloorRow key={f.id} floor={f} detail={detail} lookups={l} selected={f.id === selected}
                    onEdit={() => setSelected(f.id)} onLock={() => run(`floors/${f.id}/lock`, { locked: !f.locked })}
                    onReroll={() => run(`floors/${f.id}/reroll`)} busy={command.isPending} />
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ))}

      {state.notes && <section className={styles.panel}><h2 className={styles.panelTitle}>Notas</h2><p className={styles.notes}>{state.notes}</p></section>}

      <FloorEditor floor={floor} detail={detail} rules={rules} lookups={l} busy={command.isPending} run={run} onClose={() => setSelected(null)} />
      <RenameDialog key={detail.version} open={renaming} detail={detail} onClose={() => setRenaming(false)} run={run} />
    </div>
  );
}

type Run = (path: string, body?: Record<string, unknown>, method?: 'POST' | 'PUT', done?: string) => void;

function FloorRow({ floor, detail, lookups, selected, onEdit, onLock, onReroll, busy }: {
  floor: NetFloor; detail: NetArchitectureDetail; lookups: Lookups; selected: boolean;
  onEdit: () => void; onLock: () => void; onReroll: () => void; busy: boolean;
}) {
  const here = detail.watchers.filter((w) => w.floorId === floor.id);
  return (
    <tr className={clsx(selected && styles.selectedRow)}>
      <td className={styles.floorCell}>
        <strong>{floor.depth}</strong>
        <span className={styles.source}>{floor.id}</span>
        {floor.table === 'Lobby' && <Badge>Lobby</Badge>}
        {floor.id === detail.bottomFloorId && <Badge tone="red">Fundo</Badge>}
        {isBossFloor(floor, detail.state) && <Badge tone="amber">Boss Floor</Badge>}
      </td>
      <td>
        <ul className={styles.contents}>
          {floor.contents.map((c) => {
            const ice = c.kind === 'BlackIce' ? lookups.ice.get(c.slug ?? '') : undefined;
            const defense = c.function?.defenseId ? lookups.defenses.get(c.function.defenseId) : undefined;
            return (
              <li key={c.id}>
                <span className={clsx(styles.kind, styles[`kind${c.kind}`])}>{contentLabel(c, lookups)}</span>
                {ice && (
                  <span className={styles.stats}>
                    {ice.class === 'AntiProgram' ? 'Anti-Program' : 'Anti-Personnel'} · PER {ice.per} · SPD {ice.spd} · ATK {ice.atk} · DEF {ice.def} · REZ {ice.rez}
                    {ice.effect ? ` · ${ice.effect}` : ''}
                  </span>
                )}
                {c.kind === 'File' && c.fileIdea && <span className={styles.stats}>{c.fileIdea}</span>}
                {defense && (
                  <span className={styles.stats}>
                    {c.function!.loadout.length > 0 ? `${c.function!.loadout.join(' + ')} · ` : ''}{defense.data} · neutralizar: Electronics/Security Tech DV{defense.counterDv} ({defense.counterTime})
                  </span>
                )}
                {c.function?.demonId && <span className={styles.stats}>Operado por {detail.watchers.find((w) => w.id === c.function!.demonId)?.name ?? c.function.demonId}</span>}
              </li>
            );
          })}
          {here.length > 0 && <li className={styles.watcherHere}>Aqui: {here.map((w) => `${w.name} (Interface ${w.interface})`).join(', ')}</li>}
        </ul>
      </td>
      <td className={styles.dvCell}>
        {floor.contents.filter((c) => c.dv != null).map((c) => (
          <span key={c.id}>{c.kind === 'Password' ? 'Backdoor' : c.kind === 'File' ? 'Eye-Dee' : 'Control'} DV{c.dv}</span>
        ))}
        {floor.contents.every((c) => c.dv == null) && <span className={styles.muted}>N/A</span>}
      </td>
      <td className={styles.rowActions}>
        <IconButton label={`Editar ${floor.id}`} onClick={onEdit}><Pencil size={14} /></IconButton>
        <IconButton label={floor.locked ? `Destravar ${floor.id}` : `Travar ${floor.id}`} onClick={onLock} active={floor.locked}>
          {floor.locked ? <Lock size={14} /> : <LockOpen size={14} />}
        </IconButton>
        <IconButton label={`Rolar ${floor.id} de novo`} onClick={onReroll} disabled={floor.locked || busy}><Dices size={14} /></IconButton>
      </td>
    </tr>
  );
}

function CostPanel({ detail }: { detail: NetArchitectureDetail }) {
  const q = detail.quote;
  const groups = [...new Set(q.lines.map((l) => l.group))];
  return (
    <section className={styles.panel}>
      <h2 className={styles.panelTitle}>Custo de compra</h2>
      <p className={styles.total}>{formatEbShort(q.total)}</p>
      <p className={styles.muted}>
        {q.portable ? 'Servidor portátil (tamanho de mochila)' : 'Servidor não portátil'} · Control Nodes {q.controlNodes}/{q.maxControlNodes ?? '∞'}
        {' '}· Demons {detail.state.demons.length}/{q.maxDemons}
      </p>
      {q.warnings.length > 0 && <ul className={styles.warnings}>{q.warnings.map((w) => <li key={w}>{w}</li>)}</ul>}
      {groups.map((g) => {
        const lines = q.lines.filter((l) => l.group === g);
        return (
          <details key={g} className={styles.costGroup}>
            <summary><span>{g}</span><span>{formatEbShort(lines.reduce((n, l) => n + l.total, 0))}</span></summary>
            <ul>{lines.map((l, i) => <li key={i}><span>{l.label}</span><span>{formatEbShort(l.total)}</span></li>)}</ul>
            <span className={styles.source}>{lines[0].source}</span>
          </details>
        );
      })}
    </section>
  );
}

function DemonsPanel({ detail, rules, run, busy }: { detail: NetArchitectureDetail; rules: NetArchitectureRules; run: Run; busy: boolean }) {
  const demons = rules.rules.demons.value;
  const floors = detail.state.branches.flatMap((b) => b.floors);
  const [type, setType] = useState(demons[0].id);
  const full = detail.state.demons.length >= detail.quote.maxDemons;
  return (
    <section className={styles.panel}>
      <h2 className={styles.panelTitle}>Demons</h2>
      <p className={styles.source}>{rules.rules.demonAbilities.value} ({rules.rules.demonAbilities.source})</p>
      {detail.state.demons.length === 0 && <p className={styles.muted}>Nenhum Demon. Active Defenses (drones) precisam de um.</p>}
      {detail.state.demons.map((d) => {
        const rule = demons.find((x) => x.id === d.type);
        const nodes = floors.filter((f) => f.contents.some((c) => c.function?.demonId === d.id)).map((f) => f.id);
        return (
          <div key={d.id} className={styles.demon}>
            <div className={styles.row}>
              <strong>{rule?.name ?? d.type} ({d.id})</strong>
              <Select aria-label={`Tipo de ${d.id}`} value={d.type} disabled={busy}
                onChange={(e) => run(`demons/${d.id}`, { type: e.target.value, floorId: d.floorId }, 'PUT')}>
                {demons.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
              </Select>
              <Select aria-label={`Andar de ${d.id}`} value={d.floorId ?? ''} disabled={busy}
                onChange={(e) => run(`demons/${d.id}`, { type: d.type, floorId: e.target.value }, 'PUT')}>
                {floors.map((f) => <option key={f.id} value={f.id}>{f.id}</option>)}
              </Select>
              <IconButton label={`Remover ${d.id}`} onClick={() => run(`demons/${d.id}/remove`)} disabled={busy}><Trash2 size={14} /></IconButton>
            </div>
            {rule && <span className={styles.stats}>REZ {rule.rez} · Interface {rule.interface} · {rule.netActions} NET Actions · Combat Number {rule.combatNumber} · {formatEbShort(rule.cost)}</span>}
            {rule && <span className={styles.stats}>{rule.icon}</span>}
            <span className={styles.stats}>{nodes.length > 0 ? `Opera os Control Nodes de ${nodes.join(', ')}` : 'Sem Control Node atribuído'}</span>
          </div>
        );
      })}
      <div className={styles.row}>
        <Select aria-label="Tipo do novo Demon" value={type} onChange={(e) => setType(e.target.value)}>
          {demons.map((x) => <option key={x.id} value={x.id}>{x.name} ({formatEbShort(x.cost)})</option>)}
        </Select>
        <Button icon={<Plus size={15} />} disabled={busy || full} title={full ? `Limite de ${detail.quote.maxDemons} Demon(s) para ${floors.length} andares` : undefined}
          onClick={() => run('demons', { type, floorId: null })}>Adicionar Demon</Button>
      </div>
      <span className={styles.source}>Máximo de 1 a cada 6 andares ({rules.rules.floorsPerDemon.source}).</span>
    </section>
  );
}

function WatchersPanel({ detail, rules, run, busy }: { detail: NetArchitectureDetail; rules: NetArchitectureRules; run: Run; busy: boolean }) {
  const s = rules.rules.stealth;
  const floors = detail.state.branches.flatMap((b) => b.floors);
  const [name, setName] = useState('Netrunner de plantão');
  const [iface, setIface] = useState<number | null>(detail.difficulty.fightingChance);
  return (
    <section className={styles.panel}>
      <h2 className={styles.panelTitle}>Watchers e stealth</h2>
      <p className={styles.source}>{s.watchers.value} ({s.watchers.source})</p>
      {detail.watchers.length === 0 && <p className={styles.muted}>Ninguém vigiando: o Quiet Jack In passa direto.</p>}
      <ul className={styles.watchers}>
        {detail.watchers.map((w) => (
          <li key={w.id}>
            <span><strong>{w.name}</strong> · {w.kind === 'Demon' ? 'Demon' : 'Netrunner'} · andar {w.floorId}</span>
            <span className={styles.stats}>Interface {w.interface}{w.pathfinderBonus > 0 ? ` · Pathfinder +${w.pathfinderBonus}` : ''}</span>
            {w.kind === 'Netrunner' && (
              <span className={styles.row}>
                <Select aria-label={`Andar de ${w.name}`} value={w.floorId ?? ''} disabled={busy}
                  onChange={(e) => run(`netrunners/${w.id}`, { name: w.name, interface: w.interface, pathfinderBonus: w.pathfinderBonus, floorId: e.target.value }, 'PUT')}>
                  {floors.map((f) => <option key={f.id} value={f.id}>{f.id}</option>)}
                </Select>
                <IconButton label={`Remover ${w.name}`} onClick={() => run(`netrunners/${w.id}/remove`)} disabled={busy}><Trash2 size={14} /></IconButton>
              </span>
            )}
          </li>
        ))}
      </ul>
      <div className={styles.row}>
        <input aria-label="Nome do Netrunner" value={name} maxLength={60} onChange={(e) => setName(e.target.value)} />
        <NumberInput aria-label="Interface" min={1} max={10} value={iface} onChange={setIface} className={styles.narrow} />
        <Button icon={<Plus size={15} />} disabled={busy || !name.trim() || iface == null}
          onClick={() => run('netrunners', { name, interface: iface, pathfinderBonus: 0, floorId: null })}>Netrunner</Button>
      </div>
      <span className={styles.source}>{s.enemyNetrunnerInterface.note}</span>
      <details className={styles.costGroup}>
        <summary><span>Checagens do Going Quiet</span><span className={styles.source}>{s.checks.source}</span></summary>
        <ul className={styles.checks}>{s.checks.value.map((c) => <li key={c.label}><strong>{c.label}:</strong> {c.roll}</li>)}</ul>
        <span className={styles.source}>{s.placement.value} ({s.placement.source})</span>
      </details>
    </section>
  );
}

function RenameDialog({ open, detail, onClose, run }: { open: boolean; detail: NetArchitectureDetail; onClose: () => void; run: Run }) {
  const [name, setName] = useState(detail.name);
  const [notes, setNotes] = useState(detail.state.notes ?? '');
  return (
    <Modal open={open} onClose={onClose} title="Nome e notas">
      <div className={styles.dialog}>
        <label className={styles.label} htmlFor="na-rename">Nome</label>
        <input id="na-rename" value={name} maxLength={80} onChange={(e) => setName(e.target.value)} data-autofocus />
        <label className={styles.label} htmlFor="na-notes">Notas do Mestre</label>
        <textarea id="na-notes" value={notes} maxLength={4000} onChange={(e) => setNotes(e.target.value)}
          placeholder="Quem sabe as Passwords, onde ficam os pontos de acesso, o que acontece se o alarme tocar…" />
        <div className={styles.row}>
          <Button variant="primary" disabled={!name.trim()} onClick={() => { run('', { name, notes: notes || null }, 'PUT', 'Arquitetura atualizada.'); onClose(); }}>Salvar</Button>
          <Button variant="ghost" onClick={onClose}>Cancelar</Button>
        </div>
      </div>
    </Modal>
  );
}
