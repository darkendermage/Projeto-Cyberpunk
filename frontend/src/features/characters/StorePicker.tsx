import { Fragment, useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ChevronDown, ChevronRight, Gift, Search, ShoppingCart, Sparkles } from 'lucide-react';
import { characterApi } from '../../api/characters';
import type { BudgetPool, ItemOrigin, StoreItem } from '../../api/characterTypes';
import type { ArmorSlot } from '../../api/types';
import { Button, Checkbox, NumberInput, Select, Spinner } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { useDebouncedValue } from '../../lib/hooks';
import { Pagination } from '../catalog/Pagination';
import { CATEGORY_LABEL } from './characterLabels';
import styles from './Characters.module.css';
import { t, tv } from '../../i18n';

export interface PurchaseInput {
  itemId: number;
  quantity: number;
  caliber: string | null;
  pool: BudgetPool | null;
  install?: boolean;
  equip?: boolean;
  worn?: ArmorSlot | null;
  /** Received without paying (trade between players, gift, loot); only after creation. */
  received?: ItemOrigin | null;
  note?: string | null;
}

type Received = 'Trade' | 'Gift' | 'Loot';
const RECEIVED_LABEL: Record<Received, string> = { Gift: t('Presente', 'Gift'), Trade: t('Troca', 'Trade'), Loot: t('Saque', 'Loot') };

/**
 * Catalog search with a confirmation step per item: price, quantity, budget now and after, and the explicit choice
 * to install or equip. With `receiving` (after creation only) items come in without paying: trade, gift or loot.
 * The server still decides whether it is allowed (price, budget, installation).
 */
export function StorePicker({ onBuy, busy, creationBudgets, available, creating, receiving = false }: {
  onBuy: (input: PurchaseInput, item: StoreItem) => Promise<unknown> | void;
  busy: boolean;
  /** Complete Package creation: pay from the general and fashion budgets. */
  creationBudgets: boolean;
  /** Money available, by budget, to project the balance before confirming. */
  available: { balance: number; gear?: number; fashion?: number };
  creating: boolean;
  /** Trade, gift or loot: no money moves. */
  receiving?: boolean;
}) {
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('');
  const [pageSize, setPageSize] = useState(24);
  const q = useDebouncedValue(query, 250);
  // A new search or category starts again at page 1.
  const filter = `${q}|${category}|${pageSize}`;
  const [paging, setPaging] = useState({ filter, page: 1 });
  const page = paging.filter === filter ? paging.page : 1;
  const results = useQuery({
    queryKey: ['character-store', q, category, page, pageSize],
    queryFn: ({ signal }) => characterApi.store(q, category || null, page, pageSize, signal),
    placeholderData: keepPreviousData,
  });
  const data = results.data;
  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <div className={styles.column}>
      <div className={styles.skillToolbar}>
        <label className={styles.search}>
          <Search size={15} aria-hidden />
          <span className="visually-hidden">{t('Buscar no catálogo', 'Search the catalog')}</span>
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t('Buscar no catálogo…', 'Search the catalog…')} />
        </label>
        <Select value={category} onChange={(e) => setCategory(e.target.value)} aria-label={t('Categoria', 'Category')}>
          <option value="">{t('Todas as categorias', 'All categories')}</option>
          {Object.entries(CATEGORY_LABEL).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </Select>
      </div>
      {results.isLoading && <Spinner />}
      {data && data.items.length === 0 && <p className={styles.muted}>{t('Nada encontrado.', 'Nothing found.')}</p>}
      {data && data.items.length > 0 && (
        <>
          <p className={styles.small}>
            {data.total} {data.total === 1 ? 'item' : 'itens'} {t('· página ', '· page ')}{data.page} {t('de ', 'of ')}{totalPages}{t('. Clique no nome para ver a descrição.', '. Click the name to see the description.')}
          </p>
          <table className={styles.table}>
            <thead><tr><th>Item</th><th>{t('Categoria', 'Category')}</th><th className={styles.num}>{t('Preço', 'Price')}</th><th /></tr></thead>
            <tbody>
              {data.items.map((item) => (
                <StoreRow key={item.id} item={item} onBuy={onBuy} busy={busy} creationBudgets={creationBudgets} available={available}
                  creating={creating} receiving={receiving} />
              ))}
            </tbody>
          </table>
          {data.total > pageSize || pageSize !== 24 ? (
            <Pagination page={data.page} totalPages={totalPages} pageSize={pageSize}
              onPage={(p) => setPaging({ filter, page: p })} onPageSize={setPageSize} />
          ) : null}
        </>
      )}
    </div>
  );
}

function StoreRow({ item, onBuy, busy, creationBudgets, available, creating, receiving }: {
  item: StoreItem; onBuy: (input: PurchaseInput, item: StoreItem) => Promise<unknown> | void; busy: boolean;
  creationBudgets: boolean; available: { balance: number; gear?: number; fashion?: number }; creating: boolean; receiving: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [info, setInfo] = useState(false);
  const [quantity, setQuantity] = useState(1);
  const [caliber, setCaliber] = useState(item.calibers.length === 1 ? item.calibers[0] : '');
  const [install, setInstall] = useState(creating);
  const [equip, setEquip] = useState(false);
  const [worn, setWorn] = useState<ArmorSlot>('Body');
  const [how, setHow] = useState<Received>('Gift');
  const [note, setNote] = useState('');
  const free = receiving && !creating;
  const needsCaliber = item.category === 'Ammunition' && item.calibers.length > 1;
  const pool: BudgetPool | null = creationBudgets ? (item.fashion ? 'CreationFashion' : 'CreationGear') : null;
  const budgetNow = pool === 'CreationFashion' ? available.fashion ?? 0 : pool === 'CreationGear' ? available.gear ?? 0 : available.balance;
  const qty = item.cyberware ? 1 : quantity;
  const total = free ? 0 : (item.price ?? 0) * qty;
  const equippable = item.category === 'Weapon' || item.category === 'Armor';
  const isBodyArmor = item.category === 'Armor' && /SP \d+/.test(item.summary ?? '');

  return (
    <Fragment>
      <tr>
        <td>
          <div>
            <button type="button" className={styles.storeName} aria-expanded={info || open} onClick={() => setInfo(!info)}
              title={item.description ? t('Ver a descrição', 'See the description') : t('Sem descrição no catálogo', 'No description in the catalog')}>
              {info || open ? <ChevronDown size={13} aria-hidden /> : <ChevronRight size={13} aria-hidden />}{tv(item.name)}
            </button>
            {item.fashion && <span className={styles.tag} title="Fashion/Fashionware"><Sparkles size={10} /> {t('moda', 'fashion')}</span>}
          </div>
          <div className={styles.source}>{tv(item.summary)}{item.source ? ` · ${item.source}${item.page ? ` p.${item.page}` : ''}` : ''}</div>
        </td>
        <td className={styles.small}>{CATEGORY_LABEL[item.category] ?? item.category}</td>
        <td className={styles.total}>{formatEb(item.price)}</td>
        <td>
          <Button icon={free ? <Gift size={14} /> : <ShoppingCart size={14} />} active={open} onClick={() => setOpen(!open)}>
            {open ? t('Fechar', 'Close') : free ? t('Receber', 'Receive') : t('Comprar', 'Buy')}
          </Button>
        </td>
      </tr>
      {(info || open) && (
        <tr>
          <td colSpan={4}>
            <p className={styles.storeDescription}>{tv(item.description) ?? t('O catálogo não tem descrição para este item.', 'The catalog has no description for this item.')}</p>
            {open && <div className={styles.row} style={{ padding: '6px 0' }}>
              {free && (
                <Select value={how} onChange={(e) => setHow(e.target.value as Received)} aria-label={t('Como o item chega', 'How the item arrives')}>
                  {(Object.keys(RECEIVED_LABEL) as Received[]).map((k) => <option key={k} value={k}>{RECEIVED_LABEL[k]}</option>)}
                </Select>
              )}
              {!item.cyberware && (
                <label className={styles.small}>
                  {t('Qtd', 'Qty')}{item.perPack ? t(` (pacotes de ${item.perPack})`, ` (packs of ${item.perPack})`) : ''}{' '}
                  <NumberInput className={`${styles.input} ${styles.inputSmall}`} min={1} max={99} value={quantity}
                    onChange={(v) => setQuantity(v ?? 1)} />
                </label>
              )}
              {needsCaliber && (
                <Select value={caliber} onChange={(e) => setCaliber(e.target.value)} aria-label={t('Calibre', 'Caliber')}>
                  <option value="">{t('Calibre…', 'Caliber…')}</option>
                  {item.calibers.map((c) => <option key={c} value={c}>{c}</option>)}
                </Select>
              )}
              {item.cyberware && <Checkbox checked={install} onChange={setInstall} label={t('Instalar agora', 'Install now')} />}
              {equippable && <Checkbox checked={equip} onChange={setEquip} label={t('Equipar', 'Equip')} />}
              {equippable && equip && isBodyArmor && (
                <Select value={worn} onChange={(e) => setWorn(e.target.value as ArmorSlot)} aria-label={t('Onde vestir', 'Where to wear')}>
                  <option value="Body">{t('Corpo', 'Body')}</option>
                  <option value="Head">{t('Cabeça', 'Head')}</option>
                </Select>
              )}
              {free && (
                <input className={`${styles.input} ${styles.grow}`} style={{ minWidth: 160 }} value={note} onChange={(e) => setNote(e.target.value)}
                  placeholder={how === 'Loot' ? t('De onde veio…', 'Where it came from…') : t('De quem veio…', 'Who it came from…')} aria-label={t('Observação', 'Note')} />
              )}
              <span className={styles.grow} />
              {free
                ? <span className={styles.small}><Gift size={12} aria-hidden /> {t('sem custo', 'free')}{item.cyberware && install ? t(' (peças-base e Humanity Loss continuam valendo)', ' (foundational pieces and Humanity Loss still apply)') : ''}</span>
                : (
                  <span className={styles.small}>
                    {formatEb(total)} · {pool === 'CreationFashion' ? 'moda' : pool === 'CreationGear' ? t('orçamento geral', 'general budget') : t('saldo', 'balance')} {formatEb(budgetNow)} →{' '}
                    <strong className={total > budgetNow ? styles.red : styles.cyan}>{formatEb(budgetNow - total)}</strong>
                  </span>
                )}
              <Button variant="primary" disabled={busy || (needsCaliber && !caliber) || total > budgetNow}
                title={total > budgetNow ? t('Saldo insuficiente', 'Not enough money') : undefined}
                onClick={async () => {
                  await onBuy({
                    itemId: item.id, quantity: qty, caliber: caliber || null, pool,
                    install: item.cyberware ? install : undefined,
                    equip: equippable ? equip : undefined,
                    worn: equippable && equip && isBodyArmor ? worn : null,
                    received: free ? how : null,
                    note: free && note.trim() ? note.trim() : null,
                  }, item);
                  setOpen(false);
                }}>{free ? t('Receber', 'Receive') : t('Confirmar', 'Confirm')}</Button>
            </div>}
            {open && item.cyberware && install && (
              <p className={styles.source}>{t('O plano completo (peças-base, lado, faixa de Humanity Loss) aparece na aba Cyberware.', 'The full plan (foundational pieces, side, Humanity Loss range) shows on the Cyberware tab.')}</p>
            )}
          </td>
        </tr>
      )}
    </Fragment>
  );
}
