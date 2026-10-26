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
import { t, tv } from '../../i18n';

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
            {region ? t(`Região: ${tv(regions.get(region)?.label)} — o catálogo mostra o que cabe nela.`, `Region: ${tv(regions.get(region)?.label)} — the catalog shows what fits there.`) : t('Clique numa região para filtrar o catálogo.', 'Click a region to filter the catalog.')}
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
        <SheetPanel title={t('Cyberware guardado', 'Stored cyberware')} icon={<Cpu size={16} />}>
          {ownedCyber.length === 0 ? <p className={`${base.small} ${base.muted}`}>{t('Nenhum implante no inventário. Peças removidas ou compradas sem instalar aparecem aqui.', 'No implants in the inventory. Pieces removed or bought without installing show up here.')}</p> : (
            <ul className={base.list}>
              {ownedCyber.map((i) => (
                <li key={i.instanceId} className={base.listItem}>
                  <span className={base.grow}>{tv(i.name)}{i.quantity > 1 ? ` x${i.quantity}` : ''}{i.notes && <span className={base.source}> · {tv(i.notes)}</span>}</span>
                  <Button onClick={() => setSelection({ kind: 'inventory', instanceId: i.instanceId, itemId: i.itemId, name: i.name })}>{t('Instalar', 'Install')}</Button>
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
        <span className={styles.statusLabel}><Brain size={13} /> {t('Humanidade', 'Humanity')}</span>
        <span className={styles.statusValue}>{h.current} <small>/ {h.max} · EMP {h.empEffective}</small></span>
        <div className={base.meter} aria-hidden><div className={base.meterFill} style={{ width: `${pct}%` }} /></div>
        {h.cyberpsychosis && <StateTag tone="bad">{t('Cyberpsicose', 'Cyberpsychosis')}</StateTag>}
      </div>
      <div className={styles.statusItem}>
        <span className={styles.statusLabel} title={t('Máximo que a terapia recupera com o cyberware instalado (CP:R p.230)', 'Most that therapy recovers with the installed cyberware (CP:R p.230)')}><HeartPulse size={13} /> {t('Teto da terapia', 'Therapy cap')}</span>
        <span className={styles.statusValue}>{h.therapyCap} <small>−2 × {h.piecesLoweringCap} {t('peças, −4 × ', 'pieces, −4 × ')}{h.borgwareLoweringCap} borgware</small></span>
      </div>
      <div className={styles.statusItem}>
        <span className={styles.statusLabel}><Wallet size={13} /> {v.gear ? t('Orçamento de criação', 'Creation budget') : t('Saldo', 'Balance')}</span>
        <span className={styles.statusValue}>
          {v.gear ? <>{formatEb(v.gear.remaining)} <small>{t('geral · ', 'general · ')}{formatEb(v.fashion?.remaining ?? 0)} {t('moda', 'fashion')}</small></> : formatEb(detail.sheet.wallet.balance)}
        </span>
      </div>
      <div className={styles.statusItem}>
        <span className={styles.statusLabel}><ShieldCheck size={13} /> {t('Implantes', 'Implants')}</span>
        <span className={styles.statusValue}>{detail.report.cyberware.length} <small>{detail.report.fullBodyConversion ? 'Full Body Conversion (IR3 p.93)' : t('corpo orgânico', 'organic body')}</small></span>
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
    <section className={`${styles.region} ${active ? styles.regionActive : ''}`} aria-label={tv(region.label)}>
      <button type="button" className={styles.regionHead} onClick={onRegion} aria-pressed={active}>
        <span className={styles.regionTitle}>{tv(region.label)}</span>
        <span className={styles.regionSlots} title={region.source ?? undefined}>
          {region.slotsTotal != null ? `${region.slotsUsed}/${region.slotsTotal} slots` : empty ? t('vazio', 'empty') : ''}
          {region.meatLimb && t(' · carne', ' · meat')}
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
                {tv(l.name)}
                <small>HL {l.humanityLossApplied}{l.slotsProvided ? ` · ${l.slotsUsed}/${l.slotsProvided}` : ''}</small>
                {l.coveredBy && <small className={base.amber} title={t('Inacessível enquanto a luva estiver vestida', 'Inaccessible while the glove is worn')}>{t('coberto por ', 'covered by ')}{l.coveredBy}</small>}
              </button>
            </Tooltip>
          );
        })}
        {Array.from({ length: Math.min(free, 4) }, (_, i) => (
          <button key={`free-${i}`} type="button" className={styles.emptySlot} onClick={onRegion} aria-label={t(`Slot livre em ${tv(region.label)}`, `Free slot in ${tv(region.label)}`)}>
            <Plus size={14} />
          </button>
        ))}
        {empty && region.slotsTotal == null && (
          <button type="button" className={styles.emptySlot} onClick={onRegion} aria-label={t(`Instalar em ${tv(region.label)}`, `Install in ${tv(region.label)}`)}><Plus size={14} /></button>
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
    <SheetPanel title={t('Clínica de ripperdoc', 'Ripperdoc clinic')} className={styles.clinic}>
      <div className={styles.clinicToolbar}>
        <label className={base.search}>
          <Search size={15} aria-hidden />
          <span className="visually-hidden">{t('Buscar cyberware', 'Search cyberware')}</span>
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t('Buscar cyberware…', 'Search cyberware…')} />
        </label>
        <Select value={activeType} onChange={(e) => setType(e.target.value)} aria-label={t('Categoria', 'Category')} highlighted={!!regionLabel}>
          <option value="">{regionLabel ? t(`Todas de ${regionLabel}`, `All for ${regionLabel}`) : t('Todas as categorias', 'All categories')}</option>
          {types.map((entry) => <option key={entry} value={entry}>{CYBER_TYPE_LABEL[entry] ?? entry}</option>)}
        </Select>
      </div>
      {region && <div className={base.row}><span className={base.small}>{t('Filtrando pela região selecionada.', 'Filtering by the selected region.')}</span><Button variant="ghost" onClick={onClearRegion}>{t('Ver tudo', 'See all')}</Button></div>}
      {catalog.isLoading && <Spinner />}
      <div className={styles.catalogList} role="listbox" aria-label={t('Catálogo de cyberware', 'Cyberware catalog')}>
        {items.map((i) => {
          const active = selection?.kind === 'catalog' && selection.item.id === i.id;
          const pricey = (i.price ?? 0) > budget;
          return (
            <Tooltip key={i.id} content={i.effect ?? i.summary}>
              <button type="button" role="option" aria-selected={active}
                className={`${styles.catalogItem} ${active ? styles.catalogItemActive : ''}`} onClick={() => onPick(i)}>
                <span className={styles.catalogName}>{tv(i.name)}</span>
                <span className={styles.price}>{formatEb(i.price)}</span>
                <span className={styles.catalogMeta}>
                  <span className={base.tag}>{CYBER_TYPE_LABEL[i.cyberType ?? ''] ?? i.cyberType}</span>
                  <span>{humanityLossLabel(i.humanityLoss, i.humanityLossDice, creating)}</span>
                  {installed.has(i.id) && <span className={`${base.tag} ${base.tagRole}`}>{t('instalado', 'installed')}</span>}
                  {pricey && <span className={`${base.tag}`} style={{ color: 'var(--amber)' }}>{t('acima do saldo', 'over the balance')}</span>}
                  {i.requirements && !/^(Mall|Clinic|Hospital)$/i.test(i.requirements) && <span className={base.source}>{tv(i.requirements)}</span>}
                </span>
              </button>
            </Tooltip>
          );
        })}
        {catalog.data && items.length === 0 && <p className={base.muted}>{t('Nada nesta região com esse filtro.', 'Nothing in this region with this filter.')}</p>}
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
      <SheetPanel title={t('Detalhes do item', 'Item details')}>
        <p className={`${base.small} ${base.muted}`}>{t('Escolha um implante instalado ou um item da clínica para ver efeitos, requisitos e validar a instalação.', 'Pick an installed implant or a clinic item to see effects and requirements and validate the install.')}</p>
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
      {item.description && <p className={base.small}>{tv(item.description)}</p>}
      {effect && effect !== item.description && <p className={base.small}><strong>{t('Efeito:', 'Effect:')}</strong> {effect}</p>}
      <span className={base.source}>{tv(item.source?.name)}{item.page ? ` p.${item.page}` : ''}</span>
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
    }, t(`${name} posto em ${wearable!.name}.`, `${name} put in ${wearable!.name}.`));
    if (ok) onDone();
  };

  const install = async () => {
    const ok = replacing && selection.kind === 'catalog'
      ? await run('cyberware/replace', { instanceId: replacing, itemId: selection.item.id, meatLimb, hostInstanceId: body.hostInstanceId, acknowledge: ack, side },
        t(`${replacedName} substituído por ${name}.`, `${replacedName} replaced by ${name}.`))
      : await run('cyberware/install', { ...body }, t(`${name} instalado.`, `${name} installed.`));
    if (ok) onDone();
  };

  return (
    <SheetPanel title={t('Detalhes e validação', 'Details and validation')} actions={selection.kind === 'inventory' && <span className={base.tag}>{t('do inventário', 'from the inventory')}</span>}>
      <div className={styles.details}>
        <div className={base.column} style={{ gap: 8 }}>
          <span className={styles.detailTitle}>{name}</span>
          {item && <span className={base.small}>{CYBER_TYPE_LABEL[item.cyberType ?? ''] ?? item.cyberType} · {formatEb(item.price)} · {tv(item.summary)}</span>}
          <ItemDescription itemId={itemId} />
          {targets.length > 0 && !replacing && (
            <label className={base.tableField}>
              <span className={base.idLabel}>{t('Onde instalar', 'Where to install')}</span>
              <Select value={target} onChange={(e) => setTarget(e.target.value)} aria-label={t('Destino', 'Destination')}>
                <option value="">{t('No corpo', 'In the body')}</option>
                {targets.map((w) => (
                  <option key={w.instanceId} value={w.instanceId}>
                    {tv(w.name)} ({w.slotsUsed}/{w.slots} slots{w.worn ? '' : t(', guardado', ', stored')})
                  </option>
                ))}
              </Select>
            </label>
          )}
          {!wearable && meatPossible && <Checkbox checked={meatLimb} onChange={setMeatLimb} label={t('Instalar num membro de carne (única peça do membro, CP:R p.115)', 'Install in a meat limb (the limb\'s only piece, CP:R p.115)')} />}
          {!wearable && anyLimb && (
            <label className={base.tableField}>
              <span className={base.idLabel}>{t('Membro (Cyberarm ou Cyberleg)', 'Limb (Cyberarm or Cyberleg)')}</span>
              <Select value={host} onChange={(e) => setHost(e.target.value)} aria-label={t('Membro hospedeiro', 'Host limb')}>
                <option value="">{t('Primeiro disponível', 'First available')}</option>
                {limbs.map((l) => <option key={l.instanceId} value={l.instanceId}>{tv(l.name)} ({l.side === 'Right' ? t('direito', 'right') : t('esquerdo', 'left')})</option>)}
              </Select>
            </label>
          )}
        </div>

        {wearable ? (
          <div className={base.column} style={{ gap: 6 }}>
            <span className={base.idLabel}>{t('No vestível', 'In the wearable')}</span>
            <p className={base.small}>
              {name} {t('vai em ', 'goes in ')}<strong>{tv(wearable.name)}</strong>: {wearable.slotsUsed}/{wearable.slots} {t('slots em uso', 'slots in use')}
              {wearable.cyberfingers > 0 ? ` · Cyberfingers ${wearable.cyberfingersUsed}/${wearable.cyberfingers}` : ''}{t('. Nada é implantado: sem Humanity Loss, e só funciona com o item vestido.', '. Nothing is implanted: no Humanity Loss, and it only works with the item worn.')}
            </p>
            {selection.kind === 'catalog' && <span className={base.small}>{t('Custo: ', 'Cost: ')}{formatEb(selection.item.price)} {t('(o mesmo de instalar uma vez num Cybereye ou Cyberarm).', '(the same as installing it once in a Cybereye or Cyberarm).')}</span>}
            {wearable.requirement && <span className={`${base.small} ${base.amber}`}>{t('Requisito: ', 'Requirement: ')}{tv(wearable.requirement)}</span>}
            <span className={base.source}>{wearable.source}</span>
          </div>
        ) : (
        <div className={base.column} style={{ gap: 6 }}>
          <span className={base.idLabel}>{t('O que será instalado', 'What will be installed')}</span>
          {!p ? <Spinner /> : p.error && p.pieces.length === 0 ? <p className={`${base.small} ${base.red}`}>{p.error}</p> : (
            <table className={base.table}>
              <thead><tr><th>{t('Peça', 'Piece')}</th><th>{t('Onde', 'Where')}</th><th className={base.num}>HL</th><th className={base.num}>{t('Preço', 'Price')}</th></tr></thead>
              <tbody>
                {p.pieces.map((x) => (
                  <tr key={x.instanceId}>
                    <td>{tv(x.name)}</td>
                    <td className={base.small}>{x.host ?? (x.meatLimb ? t('membro de carne', 'meat limb') : x.region)}{x.side ? t(` · ${x.side === 'Right' ? 'dir.' : 'esq.'}`, ` · ${x.side === 'Right' ? 'R' : 'L'}`) : ''}</td>
                    <td className={base.num}>{x.humanityLossDice ? `${x.humanityLossDice}` : x.humanityLoss}</td>
                    <td className={base.total}>{formatEb(x.price)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {p && p.textRequirements.map((r) => <span key={r} className={`${base.small} ${base.amber}`}>{t('Requisito: ', 'Requirement: ')}{r}</span>)}
          {needsAck && <Checkbox checked={ack} onChange={setAck} label={t('O personagem cumpre esses requisitos', 'The character meets these requirements')} />}
        </div>
        )}

        {wearable ? (
          <div className={base.column} style={{ gap: 8 }}>
            <Button variant="primary" className={styles.validateButton} icon={<Glasses size={18} />} disabled={busy} onClick={putInWearable}>
              {t('Pôr em ', 'Put in ')}{tv(wearable.name)}
            </Button>
          </div>
        ) : (
        <div className={base.column} style={{ gap: 8 }}>
          {p && (
            <dl className={base.kv}>
              <dt>{t('Custo', 'Cost')}</dt><dd>{formatEb(p.price)}</dd>
              <dt>{p.pool === 'Balance' ? t('Saldo', 'Balance') : p.pool === 'CreationFashion' ? t('Orçamento de moda', 'Fashion budget') : t('Orçamento geral', 'General budget')}</dt>
              <dd>{formatEb(p.available)} → {formatEb(p.availableAfter)}</dd>
              <dt>{t('Humanidade', 'Humanity')}</dt>
              <dd>{p.humanityNow} → {p.humanityLossMin === p.humanityLossMax ? p.humanityNow - p.humanityLossMax : `${p.humanityNow - p.humanityLossMax}–${p.humanityNow - p.humanityLossMin}`}</dd>
              <dt>{t('EMP (pior caso)', 'EMP (worst case)')}</dt><dd>{p.empNow} → {p.empAfterWorst}</dd>
              <dt>{t('Teto da terapia', 'Therapy cap')}</dt><dd>{p.therapyCapAfter}</dd>
            </dl>
          )}
          {p?.humanityLossRolled && <span className={base.source}>{t('Fora da criação a Humanity Loss é rolada na instalação (CP:R p.110).', 'Outside creation, Humanity Loss is rolled on install (CP:R p.110).')}</span>}
          {p?.warnings.map((w) => <span key={w} className={`${base.small} ${base.amber}`}>{w}</span>)}
          {replacedName && <StateTag tone="warn">{t('Substitui ', 'Replaces ')}{replacedName}</StateTag>}
          <Button variant="primary" className={styles.validateButton} icon={<ShieldCheck size={18} />}
            disabled={busy || !p || !!p.error || (needsAck && !ack)} onClick={install}>
            {replacedName ? t('Validar e substituir', 'Validate and replace') : t('Validar e instalar', 'Validate and install')}
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
    <SheetPanel title={t('Detalhes do implante', 'Implant details')}>
      <div className={styles.details}>
        <div className={base.column} style={{ gap: 8 }}>
          <span className={styles.detailTitle}>{tv(line.name)}</span>
          <span className={base.small}>{CYBER_TYPE_LABEL[tv(line.type) ?? ''] ?? tv(line.type)}{line.side ? t(` · ${line.side === 'Right' ? 'lado direito' : 'lado esquerdo'}`, ` · ${line.side === 'Right' ? 'right side' : 'left side'}`) : ''}{line.meatLimb ? t(' · membro de carne', ' · meat limb') : ''}</span>
          <ItemDescription itemId={line.itemId} />
        </div>
        <dl className={base.kv}>
          <dt>{t('Humanity Loss aplicada', 'Humanity Loss applied')}</dt><dd>{line.humanityLossApplied}{piece?.humanityLossRolled ? t(` (rolado ${line.humanityLossDice})`, ` (rolled ${line.humanityLossDice})`) : t(' (pré-definida)', ' (preset)')}</dd>
          {line.slotsProvided != null && <><dt>Option Slots</dt><dd>{line.slotsUsed}/{line.slotsProvided}</dd></>}
          {parent && <><dt>{t('Instalado em', 'Installed in')}</dt><dd>{tv(parent.name)}</dd></>}
          {children.length > 0 && <><dt>{t('Opções', 'Options')}</dt><dd>{children.map((c) => tv(c.name)).join(', ')}</dd></>}
          <dt>{t('Pago', 'Paid')}</dt><dd>{formatEb(piece?.pricePaid ?? 0)}</dd>
          {line.currentSp != null && <><dt>{t('SP atual', 'Current SP')}</dt><dd>{line.currentSp}</dd></>}
        </dl>
        <div className={base.column} style={{ gap: 8 }}>
          {line.requirements && <span className={`${base.small} ${base.amber}`}>{t('Requisito: ', 'Requirement: ')}{tv(line.requirements)}</span>}
          {creating ? (
            piece?.origin === 'Purchase'
              ? <Button icon={<Undo2 size={15} />} disabled={busy} onClick={async () => { if (await run('purchase/undo', { instanceId: line.instanceId }, t('Compra desfeita.', 'Purchase undone.'))) onDone(); }}>{t('Desfazer compra', 'Undo purchase')}</Button>
              : <span className={`${base.small} ${base.muted}`}>{t('Peça do pacote inicial: muda depois de concluir a criação.', 'Starting package piece: change it after completing creation.')}</span>
          ) : (
            <>
              <Button icon={<Repeat size={15} />} active={replacing === line.instanceId}
                onClick={() => onReplace(replacing === line.instanceId ? null : line.instanceId)}>
                {replacing === line.instanceId ? t('Escolha o substituto na clínica', 'Pick the replacement in the clinic') : t('Substituir', 'Replace')}
              </Button>
              <Button variant="danger" icon={<Trash2 size={15} />} disabled={busy} onClick={() => setConfirm(true)}>{t('Remover', 'Remove')}</Button>
            </>
          )}
        </div>
      </div>

      <Modal open={confirm} onClose={() => setConfirm(false)} title={t(`Remover ${tv(line.name)}`, `Remove ${tv(line.name)}`)}>
        <div className={base.panel} style={{ clipPath: 'none', border: 'none' }}>
          {children.length > 0 && <p className={base.small}>{t('Sai junto: ', 'Comes out too: ')}{children.map((c) => tv(c.name)).join(', ')}.</p>}
          <p className={base.small}>{t('A Humanidade perdida não volta com a remoção: só o teto da terapia sobe (CP:R p.230).', 'Lost Humanity doesn\'t come back on removal: only the therapy cap goes up (CP:R p.230).')}</p>
          <Checkbox checked={keep} onChange={setKeep} label={t('Guardar as peças no inventário', 'Keep the pieces in the inventory')} />
          <div className={base.row}>
            <Button variant="danger" data-autofocus disabled={busy} onClick={async () => {
              if (await run('cyberware/remove', { instanceId: line.instanceId, keep }, t(`${line.name} removido.`, `${line.name} removed.`))) { setConfirm(false); onDone(); }
            }}>{t('Remover', 'Remove')}</Button>
            <Button variant="ghost" onClick={() => setConfirm(false)}>{t('Cancelar', 'Cancel')}</Button>
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
  const entry = therapies.find((x) => x.id === kind);
  const cost = payment === 'clinic' ? entry?.cost ?? 0 : payment === 'medtech' ? entry?.materials ?? 0 : 0;
  const room = h.therapyCap - h.current;
  const locked = !detail.sheet.creation.finalized;

  return (
    <SheetPanel title={t('Terapia', 'Therapy')} icon={<HeartPulse size={16} />} actions={<span className={base.source}>{rules.cyberware.therapySource}</span>}>
      <p className={base.small}>
        {t('Uma semana de terapia recupera Humanidade até o teto de ', 'A week of therapy recovers Humanity up to the cap of ')}{h.therapyCap}
        {room > 0 ? t(` (faltam ${room}).`, ` (${room} to go).`) : t(': já está no teto. Remover cyberware libera mais.', ': already at the cap. Removing cyberware frees up more.')}
      </p>
      <div className={base.row}>
        <Select value={kind} onChange={(e) => setKind(e.target.value)} aria-label={t('Tipo de terapia', 'Therapy type')}>
          {therapies.map((x) => <option key={x.id} value={x.id}>{tv(x.label)} ({tv(x.recovery)}, DV{x.dv})</option>)}
        </Select>
        <Select value={payment} onChange={(e) => setPayment(e.target.value)} aria-label={t('Pagamento', 'Payment')}>
          <option value="clinic">{t('Clínica (', 'Clinic (')}{formatEb(entry?.cost ?? 0)})</option>
          <option value="medtech">{t('Medtech: só materiais (', 'Medtech: materials only (')}{formatEb(entry?.materials ?? 0)})</option>
          <option value="free">{t('Sem custo (Mestre)', 'Free (GM)')}</option>
        </Select>
        <input className={`${base.input} ${base.inputSmall}`} type="number" min={1} value={roll} placeholder={tv(entry?.recovery)}
          aria-label={t('Resultado dos dados (vazio para rolar)', 'Dice result (empty to roll)')} onChange={(e) => setRoll(e.target.value)} />
        <Button disabled={busy || locked || room <= 0} title={locked ? t('Conclua a criação antes', 'Finish creation first') : t(`Custo: ${formatEb(cost)}`, `Cost: ${formatEb(cost)}`)}
          onClick={async () => { if (await run('therapy', { kind, payment, roll: roll ? Number(roll) : null })) setRoll(''); }}>
          {roll ? t('Registrar', 'Record') : t('Rolar', 'Roll')} {t('terapia', 'therapy')}
        </Button>
      </div>
      <span className={base.source}>{t('Medtechs não fazem terapia em si mesmos (CP:R p.229).', 'Medtechs can\'t do therapy on themselves (CP:R p.229).')}</span>
    </SheetPanel>
  );
}
