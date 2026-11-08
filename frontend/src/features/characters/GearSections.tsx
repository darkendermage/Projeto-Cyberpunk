import { useEffect, useRef, useState, type ReactNode } from 'react';
import {
  Anchor, AudioLines, Binoculars, Box, Cpu, Crosshair, Ear, EllipsisVertical, Flashlight, Glasses, Hand,
  Laptop, Package, Pill, Plus, Radio, RadioTower, Search, Shield, Shirt, Smartphone, Syringe, Video, Wrench,
} from 'lucide-react';
import type { CharacterDetail, InventoryLine } from '../../api/characterTypes';
import { ItemDetailModal } from '../catalog/ItemDetailModal';
import { formatEb } from '../../lib/format';
import { t, tv } from '../../i18n';
import { CATEGORY_LABEL, QUALITY_LABEL } from './characterLabels';
import type { Run } from './CombatPanels';
import { bodySlots, filterStored, gearTag, paginate, splitGear, type BodySlot, type GearFilter } from './gearLayout';
import { groupInventory } from './inventoryGroups';
import { Pager } from './Pager';
import styles from './Characters.module.css';

const RECEIVED_LABEL: Partial<Record<InventoryLine['origin'], string>> = { Trade: 'troca', Gift: 'presente', Loot: 'saque' };

/** An icon for an item: its category, and a few common gear items by name. */
function itemIcon(i: InventoryLine): ReactNode {
  const s = `${i.slug} ${i.name}`.toLowerCase();
  const size = 18;
  if (i.fashion) return <Shirt size={size} />;
  switch (i.category) {
    case 'Weapon': return <Crosshair size={size} />;
    case 'Ammunition': return <Box size={size} />;
    case 'WeaponAttachment': return <Wrench size={size} />;
    case 'Armor': return <Shield size={size} />;
    case 'Cyberware': return <Cpu size={size} />;
    case 'Drug': return <Pill size={size} />;
  }
  if (i.sp != null || i.shieldHp != null) return <Shield size={size} />;
  const by: [RegExp, ReactNode][] = [
    [/agent|phone/, <Smartphone size={size} />], [/flashlight|lanterna/, <Flashlight size={size} />], [/binocular/, <Binoculars size={size} />],
    [/grapple|gancho/, <Anchor size={size} />], [/scrambler/, <RadioTower size={size} />], [/radio|scanner/, <Radio size={size} />],
    [/camera|video/, <Video size={size} />], [/recorder|audio/, <AudioLines size={size} />], [/computer|cyberdeck|laptop/, <Laptop size={size} />],
    [/glasses|lens|goggle/, <Glasses size={size} />], [/ears|ear /, <Ear size={size} />], [/glove/, <Hand size={size} />],
    [/med|first aid|airhypo|speedheal/, <Syringe size={size} />], [/tool|kit/, <Wrench size={size} />],
  ];
  return by.find(([re]) => re.test(s))?.[1] ?? <Package size={size} />;
}

/** "1 unidade · 100 eb", "35 munições", or the received tag. */
function subtitle(i: InventoryLine): string {
  const qty = i.rounds ?? i.quantity;
  const unit = i.category === 'Ammunition' ? t(`${qty} munições`, `${qty} rounds`) : qty === 1 ? t('1 unidade', '1 unit') : t(`${qty} unidades`, `${qty} units`);
  const cat = i.fashion ? t('Vestuário', 'Clothing') : CATEGORY_LABEL[i.category] ?? i.category;
  const price = RECEIVED_LABEL[i.origin] ?? (i.pricePaid ? `${formatEb(i.pricePaid)} eb` : null);
  const quality = i.quality && i.quality !== 'Standard' ? QUALITY_LABEL[i.quality] : null;
  return [quality ?? cat, unit, price].filter(Boolean).join(' · ');
}

export interface GearActions {
  run: Run;
  busy: boolean;
  finalized: boolean;
  onOpen: (item: InventoryLine) => void;
  onRemove: (item: InventoryLine) => void;
}

/** The ⋮ menu of an item: where it goes, the weapon's quality, refund and sale. */
function ItemMenu({ item, actions }: { item: InventoryLine; actions: GearActions }) {
  const { run, busy, finalized, onRemove } = actions;
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const close = (e: MouseEvent) => { if (!box.current?.contains(e.target as Node)) setOpen(false); };
    const key = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false); };
    document.addEventListener('mousedown', close);
    document.addEventListener('keydown', key);
    return () => { document.removeEventListener('mousedown', close); document.removeEventListener('keydown', key); };
  }, [open]);
  const armor = item.sp != null;
  const current = item.state === 'Equipped' && item.worn ? `Equipped:${item.worn}` : item.state;
  const places: [string, string][] = [
    ...(armor ? [['Equipped:Body', t('Vestir no corpo', 'Wear on the body')], ['Equipped:Head', t('Vestir na cabeça', 'Wear on the head')]] as [string, string][]
      : [['Equipped', item.fashion ? t('Vestir', 'Wear') : t('Equipar', 'Equip')]] as [string, string][]),
    ['Carried', t('Levar com o personagem', 'Carry')],
    ['Stored', t('Guardar', 'Store')],
  ];
  const refundable = item.origin === 'Purchase' && (!finalized || item.paidFrom === 'Balance');
  const act = (fn: () => void) => { setOpen(false); fn(); };

  return (
    <div className={styles.gearMenu} ref={box}>
      <button type="button" className={styles.gearMenuButton} aria-haspopup="menu" aria-expanded={open} disabled={busy}
        aria-label={t(`Ações de ${tv(item.name)}`, `${tv(item.name)} actions`)} onClick={() => setOpen((o) => !o)}>
        <EllipsisVertical size={16} />
      </button>
      {open && (
        <div className={styles.gearMenuList} role="menu">
          {places.filter(([v]) => v !== current).map(([v, label]) => (
            <button key={v} type="button" role="menuitem" onClick={() => act(() => {
              const [state, worn] = v.split(':');
              void run('equip', { instanceId: item.instanceId, state, worn: worn ?? null });
            })}>{label}</button>
          ))}
          {item.quality && (
            <>
              <span className={styles.gearMenuLabel}>{t('Qualidade', 'Quality')}</span>
              {(['Poor', 'Standard', 'Excellent'] as const).filter((q) => q !== item.quality).map((q) => (
                <button key={q} type="button" role="menuitem" title={t('Excellent: +1 nos ataques. Poor: trava no 1 natural (CP:R p.344).', 'Excellent: +1 to attacks. Poor: jams on a natural 1 (CP:R p.344).')}
                  onClick={() => act(() => void run('weapon-quality', { instanceId: item.instanceId, quality: q }))}>{QUALITY_LABEL[q]}</button>
              ))}
            </>
          )}
          {(refundable || finalized) && <span className={styles.gearMenuLabel}>{t('Sair do inventário', 'Leave the inventory')}</span>}
          {refundable && (
            <button type="button" role="menuitem" onClick={() => act(() => {
              if (finalized && !window.confirm(t(`Desfazer a compra de ${tv(item.name)} e devolver ${formatEb(item.pricePaid ?? 0)} eb?`, `Undo buying ${tv(item.name)} and get ${formatEb(item.pricePaid ?? 0)} eb back?`))) return;
              void run('purchase/undo', { instanceId: item.instanceId }, t('Compra reembolsada.', 'Purchase refunded.'));
            })}>{t('Reembolsar a compra', 'Refund the purchase')}</button>
          )}
          {finalized && <button type="button" role="menuitem" onClick={() => act(() => onRemove(item))}>{t('Vender ou descartar…', 'Sell or discard…')}</button>}
        </div>
      )}
    </div>
  );
}

/** One item: icon, name (opens its catalog page), what it is and how many, the tag and the ⋮ menu. */
function GearRow({ item, actions }: { item: InventoryLine; actions: GearActions }) {
  const tag = gearTag(item);
  return (
    <li className={styles.gearRow}>
      <span className={styles.gearIcon} aria-hidden>{itemIcon(item)}</span>
      <button type="button" className={styles.gearName} onClick={() => actions.onOpen(item)} title={t('Ver a página do item', 'View the item page')}>
        <strong>{tv(item.name)}{item.caliber && <span className={styles.muted}> ({item.caliber})</span>}</strong>
        <span className={styles.muted}>{subtitle(item)}</span>
      </button>
      <span className={`${styles.gearTag} ${styles[`gearTag_${tag.tone}`]}`}>{tag.label}</span>
      <ItemMenu item={item} actions={actions} />
    </li>
  );
}

/** "SALDO 4.710 eb · EQUIPADOS 06 · INVENTÁRIO 15". */
export function GearStats({ detail }: { detail: CharacterDetail }) {
  const { onPerson, stored } = splitGear(detail.report.inventory);
  const tile = (label: string, value: ReactNode, sub: string, tone?: string) => (
    <div className={styles.gearStat}>
      <span className={styles.idLabel}>{label}</span>
      <strong className={tone}>{value}</strong>
      <span className={styles.muted}>{sub}</span>
    </div>
  );
  return (
    <div className={styles.gearStats}>
      {tile(t('Saldo', 'Balance'), formatEb(detail.sheet.wallet.balance), t('eb disponíveis', 'eb available'))}
      {tile(t('Equipados', 'Equipped'), String(onPerson.length).padStart(2, '0'), t('itens com o personagem', 'items on the character'), styles.green)}
      {tile(t('Inventário', 'Inventory'), String(stored.length).padStart(2, '0'), t('itens guardados', 'items stored'))}
    </div>
  );
}

function SectionHead({ index, title, count }: { index: string; title: string; count?: number }) {
  return (
    <header className={styles.gearHead}>
      <h3 className={styles.gearTitle}><span>{index} /</span> {title}</h3>
      {count != null && <span className={styles.gearCount}>{t(`${count} itens`, `${count} items`)}</span>}
    </header>
  );
}

function BodyStrip({ slots }: { slots: BodySlot[] }) {
  const icon = { eyes: <Glasses size={16} />, ears: <Ear size={16} />, hands: <Hand size={16} />, body: <Shirt size={16} /> };
  return (
    <div className={styles.bodyStrip}>
      {slots.map((s) => (
        <span key={s.key} className={`${styles.bodySlot} ${s.filled ? styles.bodySlotOn : ''}`} title={s.filled ? s.names.join(', ') : t('Vazio', 'Empty')}>
          {icon[s.key]}
          <span>{s.label}</span>
          <span aria-label={s.filled ? t('ocupado', 'filled') : t('vazio', 'empty')}>{s.filled ? '✓' : '+'}</span>
        </span>
      ))}
    </div>
  );
}

/** 01 / Equipados: what the character has on them, by kind, and the worn places. */
export function EquippedSection({ detail, actions }: { detail: CharacterDetail; actions: GearActions }) {
  const { onPerson } = splitGear(detail.report.inventory);
  const g = groupInventory(onPerson);
  const groups: [string, InventoryLine[]][] = [
    [t('Armas', 'Weapons'), g.weapons], [t('Armaduras', 'Armor'), g.armor], [t('Roupas', 'Clothing'), g.clothing], [t('Itens diversos', 'Misc items'), g.misc],
  ];
  return (
    <section className={styles.gearPanel} aria-label={t('Equipados', 'Equipped')}>
      <SectionHead index="01" title={t('Equipados', 'Equipped')} count={onPerson.length} />
      {onPerson.length === 0 && <p className={`${styles.small} ${styles.muted}`}>{t('Nada com o personagem. Use o ⋮ de um item guardado para equipar ou levar.', 'Nothing on the character. Use a stored item\'s ⋮ to equip or carry it.')}</p>}
      {groups.filter(([, items]) => items.length > 0).map(([label, items]) => (
        <div key={label} className={styles.gearGroup}>
          <div className={styles.gearGroupHead}><span>{label}</span><span className={styles.muted}>{String(items.length).padStart(2, '0')}</span></div>
          <ul className={styles.gearList}>{items.map((i) => <GearRow key={i.instanceId} item={i} actions={actions} />)}</ul>
        </div>
      ))}
      <BodyStrip slots={bodySlots(detail.report.wearables, detail.report.inventory)} />
    </section>
  );
}

const FILTERS: [GearFilter, string][] = [['all', t('Todos', 'All')], ['weapons', t('Armas', 'Weapons')], ['clothing', t('Roupas', 'Clothing')], ['other', t('Outros', 'Other')]];

/** 02 / Inventário: the stored items, searched and filtered, ten per page. */
export function InventorySection({ detail, actions, onAdd }: { detail: CharacterDetail; actions: GearActions; onAdd?: () => void }) {
  const { stored } = splitGear(detail.report.inventory);
  const [filter, setFilter] = useState<GearFilter>('all');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(0);
  const shown = filterStored(stored, filter, query);
  const p = paginate(shown, page);
  return (
    <section className={styles.gearPanel} aria-label={t('Inventário', 'Inventory')}>
      <SectionHead index="02" title={t('Inventário', 'Inventory')} count={stored.length} />
      <div className={styles.rowBetween}>
        <strong>{t('Itens guardados', 'Stored items')}</strong>
        {onAdd && <button type="button" className={styles.gearAdd} onClick={onAdd} aria-label={t('Comprar ou receber itens', 'Buy or receive items')} title={t('Comprar itens', 'Buy items')}><Plus size={15} /></button>}
      </div>
      <label className={styles.gearSearch}>
        <Search size={14} aria-hidden />
        <input value={query} placeholder={t('Buscar no inventário…', 'Search the inventory…')} aria-label={t('Buscar no inventário', 'Search the inventory')}
          onChange={(e) => { setQuery(e.target.value); setPage(0); }} />
      </label>
      <div className={styles.gearChips} role="group" aria-label={t('Filtrar', 'Filter')}>
        {FILTERS.map(([f, label]) => (
          <button key={f} type="button" className={filter === f ? styles.gearChipOn : styles.gearChip} aria-pressed={filter === f}
            onClick={() => { setFilter(f); setPage(0); }}>{label}</button>
        ))}
      </div>
      {shown.length === 0
        ? <p className={`${styles.small} ${styles.muted}`}>{stored.length === 0 ? t('Nada guardado.', 'Nothing stored.') : t('Nenhum item com esse filtro.', 'No items match.')}</p>
        : <ul className={styles.gearList}>{p.rows.map((i) => <GearRow key={i.instanceId} item={i} actions={actions} />)}</ul>}
      <Pager page={p.page} pages={p.pages} rows={p.rows.length} total={shown.length} noun={t('itens', 'items')} onPage={setPage} />
    </section>
  );
}

/** The catalog page of an item, opened from the Gear tab (the same one as the items screen). */
export function ItemPage({ item, onClose }: { item: InventoryLine | null; onClose: () => void }) {
  return <ItemDetailModal itemId={item?.itemId ?? null} onClose={onClose} />;
}
