import { useMemo, useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Brain, Cpu, Glasses, HeartPulse, Plus, Repeat, Search, ShieldCheck, Trash2, Undo2, Wallet } from 'lucide-react';
import { characterApi, problemMessage, useCharacterCommand } from '../../api/characters';
import type {
  CharacterDetail, CharacterRules, CyberLine, CyberRegionReport, InstallRequestBody, StoreItem,
} from '../../api/characterTypes';
import { useItemDetail } from '../../api/hooks';
import { Button, Checkbox, Modal, Select, Spinner, Tooltip } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { useDebouncedValue } from '../../lib/hooks';
import { useToast } from '../../state/ToastContext';
import { AnatomyFigure } from './AnatomyFigure';
import { CYBER_TYPE_LABEL, fitsRegion, humanityLossLabel, LEFT_REGIONS, RIGHT_REGIONS, type RegionKey, wearableAccepts } from './anatomy';
import { CyberEffectsPanel, SheetPanel, StateTag } from './CharacterUi';
import type { Run } from './CombatPanels';
import type { SheetEditor } from './useSheetEditor';
import base from './Characters.module.css';
import styles from './Cyberware.module.css';

interface Props { detail: CharacterDetail; rules: CharacterRules; editor: SheetEditor }

type Selection =
  | { kind: 'catalog'; item: StoreItem }
  | { kind: 'inventory'; instanceId: string; itemId: number; name: string }
  | { kind: 'installed'; instanceId: string }
  | null;

export function CyberwareView({ detail, rules, editor }: Props) {
  const toast = useToast();
  const command = useCharacterCommand(detail.id);
  const [region, setRegion] = useState<RegionKey | null>(null);
  const [selection, setSelection] = useState<Selection>(null);
  const report = detail.report;
  const creating = !detail.sheet.creation.finalized;

  const run: Run = async (path, body = {}, success) => {
    await editor.flush();
    try {
      const result = await command.mutateAsync({ path, body });
      result.notices.forEach((n) => toast.notify(n, 'info'));
      if (success) toast.notify(success);
      return result;
    } catch (error) {
      toast.notify(problemMessage(error), 'error');
      return null;
    }
  };

  const regions = new Map(report.regions.map((r) => [r.key as RegionKey, r]));
  const lines = new Map(report.cyberware.map((c) => [c.instanceId, c]));
  const pickRegion = (key: RegionKey) => setRegion((current) => (current === key ? null : key));
  const ownedCyber = report.inventory.filter((i) => i.category === 'Cyberware');

  const regionColumn = (keys: RegionKey[]) => (
    <div className={styles.regions}>
      {keys.map((key) => {
        const r = regions.get(key);
        return r && <RegionPanel key={key} region={r} lines={lines} active={region === key} selection={selection}
          onRegion={() => pickRegion(key)} onPiece={(id) => setSelection({ kind: 'installed', instanceId: id })} />;
      })}
    </div>
  );

  return (
    <div className={base.column}>
      <StatusBar detail={detail} />

      <div className={styles.layout}>
        {regionColumn(LEFT_REGIONS)}
        <div className={styles.figureWrap}>
          <AnatomyFigure regions={report.regions} selected={region} onSelect={pickRegion} fbc={report.fullBodyConversion} />
          <p className={base.source} style={{ textAlign: 'center' }}>
            {region ? `Região: ${regions.get(region)?.label} — o catálogo mostra o que cabe nela.` : 'Clique numa região para filtrar o catálogo.'}
          </p>
        </div>
        {regionColumn(RIGHT_REGIONS)}
        <Clinic detail={detail} region={region} onClearRegion={() => setRegion(null)} selection={selection}
          onPick={(item) => setSelection({ kind: 'catalog', item })} />
      </div>

      <DetailsPanel detail={detail} rules={rules} selection={selection} run={run} busy={command.isPending} creating={creating}
        side={region?.endsWith('-left') ? 'Left' : region?.endsWith('-right') ? 'Right' : null}
        onDone={() => setSelection(null)} />

      <div className={base.formGrid} style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))' }}>
        <CyberEffectsPanel detail={detail} />
        <TherapyPanel detail={detail} rules={rules} run={run} busy={command.isPending} />
        <SheetPanel title="Cyberware guardado" icon={<Cpu size={16} />}>
          {ownedCyber.length === 0 ? <p className={`${base.small} ${base.muted}`}>Nenhum implante no inventário. Peças removidas ou compradas sem instalar aparecem aqui.</p> : (
            <ul className={base.list}>
              {ownedCyber.map((i) => (
                <li key={i.instanceId} className={base.listItem}>
                  <span className={base.grow}>{i.name}{i.quantity > 1 ? ` x${i.quantity}` : ''}{i.notes && <span className={base.source}> · {i.notes}</span>}</span>
                  <Button onClick={() => setSelection({ kind: 'inventory', instanceId: i.instanceId, itemId: i.itemId, name: i.name })}>Instalar</Button>
                </li>
              ))}
            </ul>
          )}
        </SheetPanel>
      </div>
    </div>
  );
}

// ------------------------------------------------------------ status bar

function StatusBar({ detail }: { detail: CharacterDetail }) {
  const h = detail.report.humanity;
  const v = detail.validation;
  const pct = h.max > 0 ? Math.max(0, (h.current / h.max) * 100) : 0;
  return (
    <div className={styles.status}>
      <div className={styles.statusItem}>
        <span className={styles.statusLabel}><Brain size={13} /> Humanidade</span>
        <span className={styles.statusValue}>{h.current} <small>/ {h.max} · EMP {h.empEffective}</small></span>
        <div className={base.meter} aria-hidden><div className={base.meterFill} style={{ width: `${pct}%` }} /></div>
        {h.cyberpsychosis && <StateTag tone="bad">Cyberpsicose</StateTag>}
      </div>
      <div className={styles.statusItem}>
        <span className={styles.statusLabel} title="Máximo que a terapia recupera com o cyberware instalado (CP:R p.230)"><HeartPulse size={13} /> Teto da terapia</span>
        <span className={styles.statusValue}>{h.therapyCap} <small>−2 × {h.piecesLoweringCap} peças, −4 × {h.borgwareLoweringCap} borgware</small></span>
      </div>
      <div className={styles.statusItem}>
        <span className={styles.statusLabel}><Wallet size={13} /> {v.gear ? 'Orçamento de criação' : 'Saldo'}</span>
        <span className={styles.statusValue}>
          {v.gear ? <>{formatEb(v.gear.remaining)} <small>geral · {formatEb(v.fashion?.remaining ?? 0)} moda</small></> : formatEb(detail.sheet.wallet.balance)}
        </span>
      </div>
      <div className={styles.statusItem}>
        <span className={styles.statusLabel}><ShieldCheck size={13} /> Implantes</span>
        <span className={styles.statusValue}>{detail.report.cyberware.length} <small>{detail.report.fullBodyConversion ? 'Full Body Conversion (IR3 p.93)' : 'corpo orgânico'}</small></span>
      </div>
    </div>
  );
}

// ------------------------------------------------------------ region panel

function RegionPanel({ region, lines, active, selection, onRegion, onPiece }: {
  region: CyberRegionReport; lines: Map<string, CyberLine>; active: boolean; selection: Selection;
  onRegion: () => void; onPiece: (instanceId: string) => void;
}) {
  const free = region.slotsTotal != null ? Math.max(0, region.slotsTotal - region.slotsUsed) : 0;
  const empty = region.pieces.length === 0;
  return (
    <section className={`${styles.region} ${active ? styles.regionActive : ''}`} aria-label={region.label}>
      <button type="button" className={styles.regionHead} onClick={onRegion} aria-pressed={active}>
        <span className={styles.regionTitle}>{region.label}</span>
        <span className={styles.regionSlots} title={region.source ?? undefined}>
          {region.slotsTotal != null ? `${region.slotsUsed}/${region.slotsTotal} slots` : empty ? 'vazio' : ''}
          {region.meatLimb && ' · carne'}
        </span>
      </button>
      <div className={styles.slots}>
        {region.pieces.map((id) => {
          const l = lines.get(id);
          if (!l) return null;
          const selected = selection?.kind === 'installed' && selection.instanceId === id;
          return (
            <Tooltip key={id} content={l.effect}>
              <button type="button" onClick={() => onPiece(id)} aria-pressed={selected}
                className={`${styles.piece} ${l.foundational ? styles.pieceFoundation : ''} ${selected ? styles.pieceSelected : ''}`}>
                {l.name}
                <small>HL {l.humanityLossApplied}{l.slotsProvided ? ` · ${l.slotsUsed}/${l.slotsProvided}` : ''}</small>
                {l.coveredBy && <small className={base.amber} title="Inacessível enquanto a luva estiver vestida">coberto por {l.coveredBy}</small>}
              </button>
            </Tooltip>
          );
        })}
        {Array.from({ length: Math.min(free, 4) }, (_, i) => (
          <button key={`free-${i}`} type="button" className={styles.emptySlot} onClick={onRegion} aria-label={`Slot livre em ${region.label}`}>
            <Plus size={14} />
          </button>
        ))}
        {empty && region.slotsTotal == null && (
          <button type="button" className={styles.emptySlot} onClick={onRegion} aria-label={`Instalar em ${region.label}`}><Plus size={14} /></button>
        )}
      </div>
    </section>
  );
}

// ------------------------------------------------------------ clinic catalog

function Clinic({ detail, region, onClearRegion, selection, onPick }: {
  detail: CharacterDetail; region: RegionKey | null; onClearRegion: () => void; selection: Selection; onPick: (item: StoreItem) => void;
}) {
  const [query, setQuery] = useState('');
  const [type, setType] = useState('');
  const q = useDebouncedValue(query, 250);
  const catalog = useQuery({
    queryKey: ['cyber-catalog', q],
    queryFn: ({ signal }) => characterApi.store(q, 'Cyberware', 1, 1000, signal),
    placeholderData: keepPreviousData,
    select: (page) => page.items,
  });
  const installed = new Set(detail.report.cyberware.map((c) => c.itemId));
  const budget = detail.validation.gear ? detail.validation.gear.remaining : detail.sheet.wallet.balance;
  const creating = !detail.sheet.creation.finalized;
  // The category list follows the selected region, so it never offers categories the region can't take.
  const regionItems = useMemo(() => (catalog.data ?? []).filter((i) => fitsRegion(i, region)), [catalog.data, region]);
  const types = useMemo(() => [...new Set(regionItems.map((i) => i.cyberType).filter(Boolean))] as string[], [regionItems]);
  const activeType = types.includes(type) ? type : '';
  const items = regionItems.filter((i) => !activeType || i.cyberType === activeType);
  const regionLabel = region ? detail.report.regions.find((r) => r.key === region)?.label ?? null : null;

  return (
    <SheetPanel title="Clínica de ripperdoc" className={styles.clinic}>
      <div className={styles.clinicToolbar}>
        <label className={base.search}>
          <Search size={15} aria-hidden />
          <span className="visually-hidden">Buscar cyberware</span>
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Buscar cyberware…" />
        </label>
        <Select value={activeType} onChange={(e) => setType(e.target.value)} aria-label="Categoria" highlighted={!!regionLabel}>
          <option value="">{regionLabel ? `Todas de ${regionLabel}` : 'Todas as categorias'}</option>
          {types.map((t) => <option key={t} value={t}>{CYBER_TYPE_LABEL[t] ?? t}</option>)}
        </Select>
      </div>
      {region && <div className={base.row}><span className={base.small}>Filtrando pela região selecionada.</span><Button variant="ghost" onClick={onClearRegion}>Ver tudo</Button></div>}
      {catalog.isLoading && <Spinner />}
      <div className={styles.catalogList} role="listbox" aria-label="Catálogo de cyberware">
        {items.map((i) => {
          const active = selection?.kind === 'catalog' && selection.item.id === i.id;
          const pricey = (i.price ?? 0) > budget;
          return (
            <Tooltip key={i.id} content={i.effect ?? i.summary}>
              <button type="button" role="option" aria-selected={active}
                className={`${styles.catalogItem} ${active ? styles.catalogItemActive : ''}`} onClick={() => onPick(i)}>
                <span className={styles.catalogName}>{i.name}</span>
                <span className={styles.price}>{formatEb(i.price)}</span>
                <span className={styles.catalogMeta}>
                  <span className={base.tag}>{CYBER_TYPE_LABEL[i.cyberType ?? ''] ?? i.cyberType}</span>
                  <span>{humanityLossLabel(i.humanityLoss, i.humanityLossDice, creating)}</span>
                  {installed.has(i.id) && <span className={`${base.tag} ${base.tagRole}`}>instalado</span>}
                  {pricey && <span className={`${base.tag}`} style={{ color: 'var(--amber)' }}>acima do saldo</span>}
                  {i.requirements && !/^(Mall|Clinic|Hospital)$/i.test(i.requirements) && <span className={base.source}>{i.requirements}</span>}
                </span>
              </button>
            </Tooltip>
          );
        })}
        {catalog.data && items.length === 0 && <p className={base.muted}>Nada nesta região com esse filtro.</p>}
      </div>
    </SheetPanel>
  );
}

// ------------------------------------------------------------ details, preview and actions

function DetailsPanel({ detail, rules, selection, run, busy, creating, side, onDone }: {
  detail: CharacterDetail; rules: CharacterRules; selection: Selection; run: Run; busy: boolean; creating: boolean;
  side: 'Left' | 'Right' | null; onDone: () => void;
}) {
  const [replacing, setReplacing] = useState<string | null>(null);
  if (!selection) {
    return (
      <SheetPanel title="Detalhes do item">
        <p className={`${base.small} ${base.muted}`}>Escolha um implante instalado ou um item da clínica para ver efeitos, requisitos e validar a instalação.</p>
      </SheetPanel>
    );
  }
  if (selection.kind === 'installed') {
    const line = detail.report.cyberware.find((c) => c.instanceId === selection.instanceId);
    if (!line) return null;
    return <InstalledDetails detail={detail} line={line} run={run} busy={busy} creating={creating} onDone={onDone}
      replacing={replacing} onReplace={setReplacing} />;
  }
  const itemId = selection.kind === 'catalog' ? selection.item.id : selection.itemId;
  return <InstallDetails key={`${selection.kind}-${itemId}`} detail={detail} rules={rules} selection={selection} run={run} busy={busy}
    replacing={replacing} side={side} onDone={() => { setReplacing(null); onDone(); }} />;
}

function ItemDescription({ itemId }: { itemId: number }) {
  const item = useItemDetail(itemId).data?.item;
  if (!item) return <Spinner />;
  const effect = item.attributes.effect as string | undefined;
  return (
    <div className={base.column} style={{ gap: 6 }}>
      {item.description && <p className={base.small}>{item.description}</p>}
      {effect && effect !== item.description && <p className={base.small}><strong>Efeito:</strong> {effect}</p>}
      <span className={base.source}>{item.source?.name}{item.page ? ` p.${item.page}` : ''}</span>
    </div>
  );
}

function InstallDetails({ detail, rules, selection, run, busy, replacing, side, onDone }: {
  detail: CharacterDetail; rules: CharacterRules; selection: Exclude<Selection, null | { kind: 'installed' }>;
  run: Run; busy: boolean; replacing: string | null; side: 'Left' | 'Right' | null; onDone: () => void;
}) {
  const item = selection.kind === 'catalog' ? selection.item : null;
  const slug = item?.slug ?? detail.report.inventory.find((i) => i.instanceId === (selection as { instanceId: string }).instanceId)?.slug ?? '';
  const name = item?.name ?? (selection as { name: string }).name;
  const c = rules.cyberware;
  const meatPossible = c.meatArmOptions.includes(slug) || c.meatLegOptions.includes(slug);
  const anyLimb = c.anyLimbOptions.includes(slug);
  const limbs = detail.report.cyberware.filter((l) => l.foundational && l.type === 'Cyberlimb');
  const [meatLimb, setMeatLimb] = useState(false);
  const [host, setHost] = useState('');
  const [ack, setAck] = useState(false);
  const itemId = item?.id ?? (selection as { itemId: number }).itemId;
  const fetched = useItemDetail(itemId).data?.item;
  const cyberType = item?.cyberType ?? (fetched?.attributes.cyberwareType as string | undefined);
  const accepts = wearableAccepts(cyberType);
  const targets = accepts ? detail.report.wearables.filter((w) => w.accepts === accepts) : [];
  const [target, setTarget] = useState('');
  const wearable = targets.find((w) => w.instanceId === target) ?? null;

  const body: InstallRequestBody = {
    itemId: selection.kind === 'catalog' ? selection.item.id : null,
    fromInventory: selection.kind === 'inventory' ? selection.instanceId : null,
    meatLimb, hostInstanceId: anyLimb && host ? host : null, acknowledge: ack,
    replacing: replacing && selection.kind === 'catalog' ? replacing : null,
    side,
  };
  const preview = useQuery({
    queryKey: ['cyber-preview', detail.id, detail.version, body],
    queryFn: ({ signal }) => characterApi.previewInstall(detail.id, body, signal),
    placeholderData: keepPreviousData,
    enabled: !wearable,
  });
  const p = preview.data;
  const replacedName = replacing ? detail.report.cyberware.find((l) => l.instanceId === replacing)?.name : null;
  const needsAck = (p?.textRequirements.length ?? 0) > 0;

  const putInWearable = async () => {
    const ok = await run('wearables/install', {
      wearableInstanceId: wearable!.instanceId,
      itemId: selection.kind === 'catalog' ? selection.item.id : null,
      optionInstanceId: selection.kind === 'inventory' ? selection.instanceId : null,
    }, `${name} posto em ${wearable!.name}.`);
    if (ok) onDone();
  };

  const install = async () => {
    const ok = replacing && selection.kind === 'catalog'
      ? await run('cyberware/replace', { instanceId: replacing, itemId: selection.item.id, meatLimb, hostInstanceId: body.hostInstanceId, acknowledge: ack, side },
        `${replacedName} substituído por ${name}.`)
      : await run('cyberware/install', { ...body }, `${name} instalado.`);
    if (ok) onDone();
  };

  return (
    <SheetPanel title="Detalhes e validação" actions={selection.kind === 'inventory' && <span className={base.tag}>do inventário</span>}>
      <div className={styles.details}>
        <div className={base.column} style={{ gap: 8 }}>
          <span className={styles.detailTitle}>{name}</span>
          {item && <span className={base.small}>{CYBER_TYPE_LABEL[item.cyberType ?? ''] ?? item.cyberType} · {formatEb(item.price)} · {item.summary}</span>}
          <ItemDescription itemId={itemId} />
          {targets.length > 0 && !replacing && (
            <label className={base.tableField}>
              <span className={base.idLabel}>Onde instalar</span>
              <Select value={target} onChange={(e) => setTarget(e.target.value)} aria-label="Destino">
                <option value="">No corpo</option>
                {targets.map((w) => (
                  <option key={w.instanceId} value={w.instanceId}>
                    {w.name} ({w.slotsUsed}/{w.slots} slots{w.worn ? '' : ', guardado'})
                  </option>
                ))}
              </Select>
            </label>
          )}
          {!wearable && meatPossible && <Checkbox checked={meatLimb} onChange={setMeatLimb} label="Instalar num membro de carne (única peça do membro, CP:R p.115)" />}
          {!wearable && anyLimb && (
            <label className={base.tableField}>
              <span className={base.idLabel}>Membro (Cyberarm ou Cyberleg)</span>
              <Select value={host} onChange={(e) => setHost(e.target.value)} aria-label="Membro hospedeiro">
                <option value="">Primeiro disponível</option>
                {limbs.map((l) => <option key={l.instanceId} value={l.instanceId}>{l.name} ({l.side === 'Right' ? 'direito' : 'esquerdo'})</option>)}
              </Select>
            </label>
          )}
        </div>

        {wearable ? (
          <div className={base.column} style={{ gap: 6 }}>
            <span className={base.idLabel}>No vestível</span>
            <p className={base.small}>
              {name} vai em <strong>{wearable.name}</strong>: {wearable.slotsUsed}/{wearable.slots} slots em uso
              {wearable.cyberfingers > 0 ? ` · Cyberfingers ${wearable.cyberfingersUsed}/${wearable.cyberfingers}` : ''}.
              Nada é implantado: sem Humanity Loss, e só funciona com o item vestido.
            </p>
            {selection.kind === 'catalog' && <span className={base.small}>Custo: {formatEb(selection.item.price)} (o mesmo de instalar uma vez num Cybereye ou Cyberarm).</span>}
            {wearable.requirement && <span className={`${base.small} ${base.amber}`}>Requisito: {wearable.requirement}</span>}
            <span className={base.source}>{wearable.source}</span>
          </div>
        ) : (
        <div className={base.column} style={{ gap: 6 }}>
          <span className={base.idLabel}>O que será instalado</span>
          {!p ? <Spinner /> : p.error && p.pieces.length === 0 ? <p className={`${base.small} ${base.red}`}>{p.error}</p> : (
            <table className={base.table}>
              <thead><tr><th>Peça</th><th>Onde</th><th className={base.num}>HL</th><th className={base.num}>Preço</th></tr></thead>
              <tbody>
                {p.pieces.map((x) => (
                  <tr key={x.instanceId}>
                    <td>{x.name}</td>
                    <td className={base.small}>{x.host ?? (x.meatLimb ? 'membro de carne' : x.region)}{x.side ? ` · ${x.side === 'Right' ? 'dir.' : 'esq.'}` : ''}</td>
                    <td className={base.num}>{x.humanityLossDice ? `${x.humanityLossDice}` : x.humanityLoss}</td>
                    <td className={base.total}>{formatEb(x.price)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {p && p.textRequirements.map((r) => <span key={r} className={`${base.small} ${base.amber}`}>Requisito: {r}</span>)}
          {needsAck && <Checkbox checked={ack} onChange={setAck} label="O personagem cumpre esses requisitos" />}
        </div>
        )}

        {wearable ? (
          <div className={base.column} style={{ gap: 8 }}>
            <Button variant="primary" className={styles.validateButton} icon={<Glasses size={18} />} disabled={busy} onClick={putInWearable}>
              Pôr em {wearable.name}
            </Button>
          </div>
        ) : (
        <div className={base.column} style={{ gap: 8 }}>
          {p && (
            <dl className={base.kv}>
              <dt>Custo</dt><dd>{formatEb(p.price)}</dd>
              <dt>{p.pool === 'Balance' ? 'Saldo' : p.pool === 'CreationFashion' ? 'Orçamento de moda' : 'Orçamento geral'}</dt>
              <dd>{formatEb(p.available)} → {formatEb(p.availableAfter)}</dd>
              <dt>Humanidade</dt>
              <dd>{p.humanityNow} → {p.humanityLossMin === p.humanityLossMax ? p.humanityNow - p.humanityLossMax : `${p.humanityNow - p.humanityLossMax}–${p.humanityNow - p.humanityLossMin}`}</dd>
              <dt>EMP (pior caso)</dt><dd>{p.empNow} → {p.empAfterWorst}</dd>
              <dt>Teto da terapia</dt><dd>{p.therapyCapAfter}</dd>
            </dl>
          )}
          {p?.humanityLossRolled && <span className={base.source}>Fora da criação a Humanity Loss é rolada na instalação (CP:R p.110).</span>}
          {p?.warnings.map((w) => <span key={w} className={`${base.small} ${base.amber}`}>{w}</span>)}
          {replacedName && <StateTag tone="warn">Substitui {replacedName}</StateTag>}
          <Button variant="primary" className={styles.validateButton} icon={<ShieldCheck size={18} />}
            disabled={busy || !p || !!p.error || (needsAck && !ack)} onClick={install}>
            {replacedName ? 'Validar e substituir' : 'Validar e instalar'}
          </Button>
        </div>
        )}
      </div>
    </SheetPanel>
  );
}

function InstalledDetails({ detail, line, run, busy, creating, onDone, replacing, onReplace }: {
  detail: CharacterDetail; line: CyberLine; run: Run; busy: boolean; creating: boolean; onDone: () => void;
  replacing: string | null; onReplace: (id: string | null) => void;
}) {
  const [confirm, setConfirm] = useState(false);
  const [keep, setKeep] = useState(true);
  const piece = detail.sheet.cyberware.find((c) => c.instanceId === line.instanceId);
  const parent = line.parentInstanceId ? detail.report.cyberware.find((c) => c.instanceId === line.parentInstanceId) : null;
  const children = detail.report.cyberware.filter((c) => c.parentInstanceId === line.instanceId);

  return (
    <SheetPanel title="Detalhes do implante">
      <div className={styles.details}>
        <div className={base.column} style={{ gap: 8 }}>
          <span className={styles.detailTitle}>{line.name}</span>
          <span className={base.small}>{CYBER_TYPE_LABEL[line.type ?? ''] ?? line.type}{line.side ? ` · ${line.side === 'Right' ? 'lado direito' : 'lado esquerdo'}` : ''}{line.meatLimb ? ' · membro de carne' : ''}</span>
          <ItemDescription itemId={line.itemId} />
        </div>
        <dl className={base.kv}>
          <dt>Humanity Loss aplicada</dt><dd>{line.humanityLossApplied}{piece?.humanityLossRolled ? ` (rolado ${line.humanityLossDice})` : ' (pré-definida)'}</dd>
          {line.slotsProvided != null && <><dt>Option Slots</dt><dd>{line.slotsUsed}/{line.slotsProvided}</dd></>}
          {parent && <><dt>Instalado em</dt><dd>{parent.name}</dd></>}
          {children.length > 0 && <><dt>Opções</dt><dd>{children.map((c) => c.name).join(', ')}</dd></>}
          <dt>Pago</dt><dd>{formatEb(piece?.pricePaid ?? 0)}</dd>
          {line.currentSp != null && <><dt>SP atual</dt><dd>{line.currentSp}</dd></>}
        </dl>
        <div className={base.column} style={{ gap: 8 }}>
          {line.requirements && <span className={`${base.small} ${base.amber}`}>Requisito: {line.requirements}</span>}
          {creating ? (
            piece?.origin === 'Purchase'
              ? <Button icon={<Undo2 size={15} />} disabled={busy} onClick={async () => { if (await run('purchase/undo', { instanceId: line.instanceId }, 'Compra desfeita.')) onDone(); }}>Desfazer compra</Button>
              : <span className={`${base.small} ${base.muted}`}>Peça do pacote inicial: muda depois de concluir a criação.</span>
          ) : (
            <>
              <Button icon={<Repeat size={15} />} active={replacing === line.instanceId}
                onClick={() => onReplace(replacing === line.instanceId ? null : line.instanceId)}>
                {replacing === line.instanceId ? 'Escolha o substituto na clínica' : 'Substituir'}
              </Button>
              <Button variant="danger" icon={<Trash2 size={15} />} disabled={busy} onClick={() => setConfirm(true)}>Remover</Button>
            </>
          )}
        </div>
      </div>

      <Modal open={confirm} onClose={() => setConfirm(false)} title={`Remover ${line.name}`}>
        <div className={base.panel} style={{ clipPath: 'none', border: 'none' }}>
          {children.length > 0 && <p className={base.small}>Sai junto: {children.map((c) => c.name).join(', ')}.</p>}
          <p className={base.small}>A Humanidade perdida não volta com a remoção: só o teto da terapia sobe (CP:R p.230).</p>
          <Checkbox checked={keep} onChange={setKeep} label="Guardar as peças no inventário" />
          <div className={base.row}>
            <Button variant="danger" data-autofocus disabled={busy} onClick={async () => {
              if (await run('cyberware/remove', { instanceId: line.instanceId, keep }, `${line.name} removido.`)) { setConfirm(false); onDone(); }
            }}>Remover</Button>
            <Button variant="ghost" onClick={() => setConfirm(false)}>Cancelar</Button>
          </div>
        </div>
      </Modal>
    </SheetPanel>
  );
}

// ------------------------------------------------------------ therapy

function TherapyPanel({ detail, rules, run, busy }: { detail: CharacterDetail; rules: CharacterRules; run: Run; busy: boolean }) {
  const therapies = rules.cyberware.therapies;
  const [kind, setKind] = useState(therapies[0]?.id ?? 'standard');
  const [payment, setPayment] = useState('clinic');
  const [roll, setRoll] = useState('');
  const h = detail.report.humanity;
  const t = therapies.find((x) => x.id === kind);
  const cost = payment === 'clinic' ? t?.cost ?? 0 : payment === 'medtech' ? t?.materials ?? 0 : 0;
  const room = h.therapyCap - h.current;
  const locked = !detail.sheet.creation.finalized;

  return (
    <SheetPanel title="Terapia" icon={<HeartPulse size={16} />} actions={<span className={base.source}>{rules.cyberware.therapySource}</span>}>
      <p className={base.small}>
        Uma semana de terapia recupera Humanidade até o teto de {h.therapyCap}
        {room > 0 ? ` (faltam ${room}).` : ': já está no teto. Remover cyberware libera mais.'}
      </p>
      <div className={base.row}>
        <Select value={kind} onChange={(e) => setKind(e.target.value)} aria-label="Tipo de terapia">
          {therapies.map((x) => <option key={x.id} value={x.id}>{x.label} ({x.recovery}, DV{x.dv})</option>)}
        </Select>
        <Select value={payment} onChange={(e) => setPayment(e.target.value)} aria-label="Pagamento">
          <option value="clinic">Clínica ({formatEb(t?.cost ?? 0)})</option>
          <option value="medtech">Medtech: só materiais ({formatEb(t?.materials ?? 0)})</option>
          <option value="free">Sem custo (Mestre)</option>
        </Select>
        <input className={`${base.input} ${base.inputSmall}`} type="number" min={1} value={roll} placeholder={t?.recovery}
          aria-label="Resultado dos dados (vazio para rolar)" onChange={(e) => setRoll(e.target.value)} />
        <Button disabled={busy || locked || room <= 0} title={locked ? 'Conclua a criação antes' : `Custo: ${formatEb(cost)}`}
          onClick={async () => { if (await run('therapy', { kind, payment, roll: roll ? Number(roll) : null })) setRoll(''); }}>
          {roll ? 'Registrar' : 'Rolar'} terapia
        </Button>
      </div>
      <span className={base.source}>Medtechs não fazem terapia em si mesmos (CP:R p.229).</span>
    </SheetPanel>
  );
}
