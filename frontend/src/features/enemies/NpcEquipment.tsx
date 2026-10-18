import { useMemo, useState, type Dispatch } from 'react';
import clsx from 'clsx';
import { Ban, CheckCircle2, Plus, Trash2, X } from 'lucide-react';
import { api } from '../../api/client';
import { useCatalogSearch, useItems } from '../../api/hooks';
import type { FilterCriterionDto, Item, NpcSheet, NpcValidationReport, WeaponQuality } from '../../api/types';
import { Button, IconButton, Segmented, Select, Spinner } from '../../components/ui/ui';
import { useDebouncedValue } from '../../lib/hooks';
import { formatEb } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import { LockButton } from './NpcEditors';
import { cyberRuleState, cyberwareTree, type DraftAction } from './npcDraft';
import styles from './EnemyGenerator.module.css';

// ---------------------------------------------------------------- catalog picker

interface PickerSource {
  key: string;
  label: string;
  category: string;
  filters?: FilterCriterionDto[];
}

const GEAR_SOURCES: PickerSource[] = [
  { key: 'weapons', label: 'Armas', category: 'weapons' },
  { key: 'armor', label: 'Armaduras', category: 'armor', filters: [{ field: 'armorType', op: 'in', values: ['BodyArmor', 'Shield', 'Clothing'] }] },
  { key: 'ammunition', label: 'Munições', category: 'ammunition' },
  { key: 'gear', label: 'Itens avulsos', category: 'gear' },
  { key: 'netrunning', label: 'Netrunning', category: 'netrunning' },
  { key: 'drugs', label: 'Drogas', category: 'drugs' },
  { key: 'weapon-attachments', label: 'Upgrades de armas', category: 'weapon-attachments' },
];

const CYBER_SOURCES: PickerSource[] = [
  { key: 'cyberware', label: 'Cyberware (inclui Fashionware)', category: 'cyberware' },
  { key: 'frames', label: 'Linear Frames', category: 'armor', filters: [{ field: 'armorType', op: 'in', values: ['LinearFrame'] }] },
];

export function ItemPicker({ sources, onPick, onClose }: { sources: PickerSource[]; onPick: (item: Item) => void; onClose: () => void }) {
  const [sourceKey, setSourceKey] = useState(sources[0].key);
  const [text, setText] = useState('');
  const q = useDebouncedValue(text, 300);
  const source = sources.find((s) => s.key === sourceKey) ?? sources[0];
  const search = useCatalogSearch(source.category, { q, filters: source.filters ?? [], page: 1, pageSize: 30 }, true);

  return (
    <div className={styles.picker}>
      <div className={styles.pickerBar}>
        <Select value={sourceKey} onChange={(e) => setSourceKey(e.target.value)} aria-label="Categoria do catálogo">
          {sources.map((s) => <option key={s.key} value={s.key}>{s.label}</option>)}
        </Select>
        <input type="search" value={text} onChange={(e) => setText(e.target.value)} placeholder="Buscar no catálogo" aria-label="Buscar no catálogo" data-autofocus />
        <IconButton label="Fechar busca" onClick={onClose}><X size={16} /></IconButton>
      </div>
      <div className={styles.results}>
        {search.isLoading && <Spinner />}
        {search.data?.items.length === 0 && <p className={styles.muted}>Nada encontrado.</p>}
        {search.data?.items.map((item) => (
          <button key={item.id} type="button" className={styles.result} onClick={() => onPick(item)}>
            <span>
              <span className={styles.itemName}>{item.name}</span>
              <span className={styles.itemSub}>{item.subtype?.label}{item.source ? ` · ${item.source.name}${item.page ? ` p.${item.page}` : ''}` : ''}</span>
            </span>
            <span className={styles.num}>{item.price === null || item.price === undefined ? '—' : `${formatEb(item.price)}eb`}</span>
            <Plus size={14} />
          </button>
        ))}
      </div>
    </div>
  );
}

// ---------------------------------------------------------------- gear

const QUALITIES: { value: WeaponQuality; label: string }[] = [
  { value: 'Poor', label: 'Poor Quality' },
  { value: 'Standard', label: 'Standard' },
  { value: 'Excellent', label: 'Excellent' },
];

export function GearEditor({
  sheet, report, dispatch, semi,
}: { sheet: NpcSheet; report?: NpcValidationReport; dispatch: Dispatch<DraftAction>; semi: boolean }) {
  const [picking, setPicking] = useState(false);
  const lines = useMemo(() => new Map((report?.gearLines ?? []).map((l) => [l.index, l])), [report]);
  const errorFields = new Set(report?.issues.filter((i) => i.severity === 'Error' && !i.overridden).map((i) => i.field));

  return (
    <>
      {sheet.gear.length === 0 ? <p className={styles.muted}>Sem equipamento.</p> : (
        <div className={styles.tableWrap}>
          <table className={styles.table}>
            <thead>
              <tr><th>Item</th><th>Qtd.</th><th>Qualidade</th><th>Vestido</th><th className={styles.num}>Total</th><th aria-label="Ações" /></tr>
            </thead>
            <tbody>
              {sheet.gear.map((g, i) => {
                const line = lines.get(i);
                const isWeapon = line?.category === 'Weapon';
                const isArmor = line?.category === 'Armor';
                return (
                  <tr key={i} className={clsx(errorFields.has(`gear[${i}]`) && styles.rowError)}>
                    <td>
                      <span className={styles.itemName}>{line?.name ?? `Item ${g.itemId}`}</span>
                      {line?.summary && <span className={styles.itemSub}>{line.summary}</span>}
                    </td>
                    <td>
                      <input type="number" min={1} max={999} value={g.quantity} aria-label="Quantidade"
                        onChange={(e) => dispatch({ type: 'updateGear', index: i, patch: { quantity: Math.max(1, Number(e.target.value)) } })} />
                    </td>
                    <td>
                      {isWeapon ? (
                        <Select value={g.quality ?? line?.quality ?? 'Standard'} aria-label="Qualidade"
                          onChange={(e) => dispatch({ type: 'updateGear', index: i, patch: { quality: e.target.value as WeaponQuality } })}>
                          {QUALITIES.map((q) => <option key={q.value} value={q.value}>{q.label}</option>)}
                        </Select>
                      ) : '—'}
                    </td>
                    <td>
                      {isArmor ? (
                        <Select value={g.worn ?? ''} aria-label="Local vestido"
                          onChange={(e) => dispatch({ type: 'updateGear', index: i, patch: { worn: (e.target.value || null) as NpcSheet['gear'][number]['worn'] } })}>
                          <option value="">Carregada</option>
                          <option value="Body">Corpo</option>
                          <option value="Head">Cabeça</option>
                        </Select>
                      ) : '—'}
                    </td>
                    <td className={styles.num}>{line ? `${formatEb(line.total)}eb` : '…'}</td>
                    <td>
                      <div className={styles.inline}>
                        {semi && <LockButton locked={!!g.locked} label={line?.name ?? 'item'}
                          onToggle={() => dispatch({ type: 'updateGear', index: i, patch: { locked: !g.locked } })} />}
                        <IconButton label="Remover" onClick={() => dispatch({ type: 'removeGear', index: i })}><Trash2 size={14} /></IconButton>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      {picking
        ? <ItemPicker sources={GEAR_SOURCES} onClose={() => setPicking(false)} onPick={(item) => dispatch({ type: 'addGear', itemId: item.id })} />
        : <div><Button icon={<Plus size={14} />} onClick={() => setPicking(true)}>Adicionar do catálogo</Button></div>}
    </>
  );
}

// ---------------------------------------------------------------- cyberware

export function CyberwareEditor({
  sheet, report, dispatch, semi,
}: { sheet: NpcSheet; report?: NpcValidationReport; dispatch: Dispatch<DraftAction>; semi: boolean }) {
  const toast = useToast();
  const [picking, setPicking] = useState(false);
  const [busy, setBusy] = useState(false);
  const lines = useMemo(() => new Map((report?.cyberLines ?? []).map((l) => [l.instanceId, l])), [report]);
  const foundations = (report?.cyberLines ?? []).filter((l) => l.foundational);

  // Installation goes through the server-side planner: it adds missing foundational pieces, slots and pairs.
  const install = async (item: Item) => {
    setBusy(true);
    try {
      const next = await api.installCyberware(sheet, item.id);
      dispatch({ type: 'setCyberware', entries: next.cyberware });
    } catch (error) {
      toast.error(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      {sheet.cyberware.length === 0 ? <p className={styles.muted}>Sem implantes.</p> : (
        <div className={styles.tableWrap}>
          <table className={styles.table}>
            <thead>
              <tr><th>Implante</th><th>Instalado em</th><th className={styles.num}>Slots</th><th className={styles.num}>Preço</th><th aria-label="Ações" /></tr>
            </thead>
            <tbody>
              {cyberwareTree(sheet.cyberware).map(({ entry, depth }) => {
                const line = lines.get(entry.instanceId);
                return (
                  <tr key={entry.instanceId}>
                    <td>
                      <span className={styles.indent}>{depth > 0 ? `${'  '.repeat(depth - 1)}└ ` : ''}</span>
                      <span className={styles.itemName}>{line?.name ?? `Item ${entry.itemId}`}</span>
                      {line?.freeTextRequirement && <span className={styles.itemSub}>Requisito: {line.freeTextRequirement}</span>}
                    </td>
                    <td>
                      {line?.foundational && !line.requiresItemId ? '—' : (
                        <Select value={entry.parentInstanceId ?? ''} aria-label="Peça base"
                          onChange={(e) => dispatch({ type: 'updateCyber', instanceId: entry.instanceId, patch: { parentInstanceId: e.target.value || null } })}>
                          <option value="">{line?.pool ? 'Corpo (sem peça base)' : 'Nenhuma'}</option>
                          {foundations.filter((f) => f.instanceId !== entry.instanceId).map((f) => (
                            <option key={f.instanceId} value={f.instanceId}>{f.name} ({f.instanceId})</option>
                          ))}
                        </Select>
                      )}
                    </td>
                    <td className={styles.num}>
                      {line?.foundational && line.slotsProvided !== null
                        ? `${line.slotsFree ?? 0} livres de ${line.slotsProvided}`
                        : line ? `${line.slotsUsed}${line.slotsAssumed ? '*' : ''}` : '…'}
                    </td>
                    <td className={styles.num}>{line?.price === null || line?.price === undefined ? '—' : `${formatEb(line.price)}eb`}</td>
                    <td>
                      <div className={styles.inline}>
                        {semi && <LockButton locked={!!entry.locked} label={line?.name ?? 'implante'}
                          onToggle={() => dispatch({ type: 'updateCyber', instanceId: entry.instanceId, patch: { locked: !entry.locked } })} />}
                        <IconButton label="Remover (e o que estiver instalado nele)" onClick={() => dispatch({ type: 'removeCyber', instanceId: entry.instanceId })}>
                          <Trash2 size={14} />
                        </IconButton>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      <p className={styles.muted}>* slots não informados no catálogo contam como 1 (CP:R p.111).</p>
      {picking
        ? <ItemPicker sources={CYBER_SOURCES} onClose={() => setPicking(false)} onPick={install} />
        : <div><Button icon={busy ? <Spinner /> : <Plus size={14} />} disabled={busy} onClick={() => setPicking(true)}>Instalar do catálogo</Button></div>}
    </>
  );
}

// ---------------------------------------------------------------- required / forbidden

export function CyberwareRulesEditor({ sheet, dispatch }: { sheet: NpcSheet; dispatch: Dispatch<DraftAction> }) {
  const [picking, setPicking] = useState(false);
  const [pickState, setPickState] = useState<'Required' | 'Forbidden'>('Required');
  const ids = sheet.cyberwareRules.map((r) => r.itemId);
  const items = useItems(ids);
  const names = new Map((items.data ?? []).map((i) => [i.id, i.name]));

  return (
    <>
      <p className={styles.muted}>
        Todo implante é <strong>permitido</strong> por padrão. <strong>Obrigatórios</strong> entram sempre (com a peça base de que dependem);
        <strong> proibidos</strong> nunca são escolhidos. A viabilidade é conferida antes de gerar.
      </p>
      <div className={styles.ruleChips}>
        {sheet.cyberwareRules.map((r) => (
          <span key={r.itemId} className={clsx(styles.ruleChip, r.state === 'Required' ? styles.ruleRequired : styles.ruleForbidden)}>
            {r.state === 'Required' ? <CheckCircle2 size={14} color="var(--green)" /> : <Ban size={14} color="var(--red)" />}
            {names.get(r.itemId) ?? `Item ${r.itemId}`}
            <Select value={cyberRuleState(sheet, r.itemId)} aria-label="Estado"
              onChange={(e) => dispatch({ type: 'cyberRule', itemId: r.itemId, state: e.target.value as 'Allowed' | 'Required' | 'Forbidden' })}>
              <option value="Allowed">Permitido</option>
              <option value="Required">Obrigatório</option>
              <option value="Forbidden">Proibido</option>
            </Select>
          </span>
        ))}
      </div>
      {picking ? (
        <>
          <Segmented size="sm" ariaLabel="Marcar como" value={pickState} onChange={setPickState}
            options={[{ value: 'Required', label: 'Marcar como obrigatório' }, { value: 'Forbidden', label: 'Marcar como proibido' }]} />
          <ItemPicker sources={CYBER_SOURCES.slice(0, 1)} onClose={() => setPicking(false)}
            onPick={(item) => dispatch({ type: 'cyberRule', itemId: item.id, state: pickState })} />
        </>
      ) : (
        <div className={styles.ruleChips}><Button icon={<Plus size={14} />} onClick={() => setPicking(true)}>Definir obrigatório ou proibido</Button></div>
      )}
    </>
  );
}
