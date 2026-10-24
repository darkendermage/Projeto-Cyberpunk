import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { ArrowLeft, Lock, LockOpen, Pencil, Plus, RefreshCw, Trash2 } from 'lucide-react';
import clsx from 'clsx';
import { problemMessage } from '../../api/characters';
import { useItems } from '../../api/hooks';
import { marketApi, useMarket, useMarketCommand, useStoreMarket } from '../../api/nightMarket';
import type { Listing, NightMarketDetail, NightMarketRules, OpenSlot } from '../../api/nightMarketTypes';
import type { Item } from '../../api/types';
import { ItemArt } from '../../components/art/ItemArt';
import { CategoryIcon } from '../../components/art/CategoryIcon';
import { Badge, Button, EmptyState, ErrorState, IconButton, Spinner, Switch } from '../../components/ui/ui';
import { formatEb, PRICE_CATEGORY_LABEL } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import page from '../../styles/Page.module.css';
import { CATEGORY_ICONS } from '../catalog/categoryMeta';
import { ItemDetailModal } from '../catalog/ItemDetailModal';
import { AddListingDialog, BuyDialog, EditListingDialog, FillSlotDialog } from './MarketDialogs';
import { EventsPanel, LedgerPanel, LogPanel, MarketInfo, SlotsPanel } from './MarketPanels';
import {
  CBK_CATEGORIES, cbkCategoryOf, CONDITION_LABEL, filterListings, KIND_LABEL, ORIGIN_LABEL, qualityLabel, shownCondition, sortStalls,
} from './nightMarket';
import styles from './NightMarket.module.css';

const PAGE = 24;

export function MarketView({ id, rules }: { id: number; rules: NightMarketRules }) {
  const market = useMarket(id);
  if (market.isLoading) return <div className={page.page}><Spinner /></div>;
  if (market.error || !market.data) return <div className={page.page}><ErrorState error={market.error} onRetry={() => market.refetch()} /></div>;
  return <Market market={market.data} rules={rules} />;
}

function Market({ market, rules }: { market: NightMarketDetail; rules: NightMarketRules }) {
  const toast = useToast();
  const navigate = useNavigate();
  const command = useMarketCommand(market.id);
  const store = useStoreMarket();
  const [stall, setStall] = useState<string | null>(null);
  const [category, setCategory] = useState<string | null>(null);
  const [q, setQ] = useState('');
  const [gm, setGm] = useState(true);
  const [onlyForSale, setOnlyForSale] = useState(false);
  const [pageIndex, setPageIndex] = useState(0);
  const [buying, setBuying] = useState<Listing | null>(null);
  const [editing, setEditing] = useState<Listing | null>(null);
  const [filling, setFilling] = useState<OpenSlot | null>(null);
  const [adding, setAdding] = useState(false);
  const [detail, setDetail] = useState<number | null>(null);

  const filtered = useMemo(() => filterListings(market.listings, { stall, category, q, gm, onlyForSale }),
    [market.listings, stall, category, q, gm, onlyForSale]);
  const pages = Math.max(1, Math.ceil(filtered.length / PAGE));
  const current = Math.min(pageIndex, pages - 1);
  const visible = filtered.slice(current * PAGE, (current + 1) * PAGE);
  const ids = visible.map((l) => l.itemId).filter((x): x is number => x != null);
  const items = useItems([...new Set(ids)]);
  const byId = new Map((items.data ?? []).map((i) => [i.id, i]));
  const open = market.status === 'Open';
  const counts = new Map(CBK_CATEGORIES.map((c) => [c.key, market.listings.filter((l) => cbkCategoryOf(l) === c.key).length]));
  const reset = () => setPageIndex(0);
  const fail = (e: unknown) => toast.notify(problemMessage(e), 'error');

  const setStatus = (status: 'Open' | 'Closed') =>
    marketApi.update(market.id, { version: market.version, name: market.name, notes: market.notes, status }).then(store, fail);
  const rename = () => {
    const name = window.prompt('Nome do mercado', market.name)?.trim();
    if (name) marketApi.update(market.id, { version: market.version, name, notes: market.notes, status: market.status }).then(store, fail);
  };
  const remove = () => {
    if (!window.confirm(`Apagar "${market.name}"? As vendas registradas também somem.`)) return;
    marketApi.remove(market.id).then(() => navigate('/night-market'), fail);
  };
  const regenerate = () => {
    if (!window.confirm('Rolar o mercado de novo com uma seed nova? As listagens atuais saem; o livro de vendas fica.')) return;
    command.mutate({ path: 'regenerate' }, { onSuccess: () => toast.notify('Mercado rolado de novo.'), onError: fail });
  };

  return (
    <div className={page.page}>
      <header className={page.header}>
        <Link to="/night-market" className={page.link}><ArrowLeft size={14} /> Night Markets</Link>
        <h1>{market.name}</h1>
        <p className={page.lead}>
          {market.preset.name} · {KIND_LABEL[market.preset.kind]} · letra {market.preset.letter}
          {market.preset.summary ? ` · ${market.preset.summary}` : ''}
        </p>
      </header>

      <div className={styles.toolbar}>
        <div className={styles.row}>
          <Switch checked={gm} onChange={setGm} label="Visão do Mestre" />
          <Switch checked={onlyForSale} onChange={(v) => { setOnlyForSale(v); reset(); }} label="Só o que está à venda" />
        </div>
        <div className={styles.row}>
          {gm && <Button icon={<Plus size={15} />} onClick={() => setAdding(true)}>Listagem</Button>}
          {gm && <Button icon={<RefreshCw size={15} />} onClick={regenerate} disabled={command.isPending}>Rolar de novo</Button>}
          <Button icon={open ? <Lock size={15} /> : <LockOpen size={15} />} onClick={() => setStatus(open ? 'Closed' : 'Open')}>
            {open ? 'Encerrar' : 'Reabrir'}
          </Button>
          <IconButton label="Renomear" onClick={rename}><Pencil size={15} /></IconButton>
          <IconButton label="Apagar mercado" onClick={remove}><Trash2 size={15} /></IconButton>
        </div>
      </div>

      {market.notices.map((n, i) => <div key={i} className={styles.notice}>{n}</div>)}

      <div className={styles.layout}>
        <section className={styles.cbk} aria-label="CBK">
          <div className={styles.cbkBar}>
            <span className={styles.cbkLogo}>CBK<small>night market companion</small></span>
            <span className={clsx(styles.live, !open && styles.closed)}>
              {open ? `Conectado ao banco de dados de ${market.organizer.name}` : 'Mercado encerrado'}
            </span>
            <input className={styles.search} value={q} onChange={(e) => { setQ(e.target.value); reset(); }}
              placeholder="Buscar por nome, fabricante ou linha" aria-label="Buscar" />
          </div>
          <nav className={styles.tabs} aria-label="Categorias">
            <button type="button" className={clsx(styles.tab, category === null && styles.tabActive)} onClick={() => { setCategory(null); reset(); }}>
              tudo<span className={styles.tabCount}>{market.listings.length}</span>
            </button>
            {CBK_CATEGORIES.filter((c) => (counts.get(c.key) ?? 0) > 0).map((c) => (
              <button key={c.key} type="button" className={clsx(styles.tab, category === c.key && styles.tabActive)}
                onClick={() => { setCategory(c.key); reset(); }}>
                {c.label}<span className={styles.tabCount}>{counts.get(c.key)}</span>
              </button>
            ))}
          </nav>
          <div className={styles.map} aria-label="Booths">
            <button type="button" className={clsx(styles.booth, stall === null && styles.boothActive)} onClick={() => { setStall(null); reset(); }}>
              Todos os booths
            </button>
            {sortStalls(market.stalls).map((s) => (
              <button key={s.code} type="button" className={clsx(styles.booth, stall === s.code && styles.boothActive)}
                onClick={() => { setStall(stall === s.code ? null : s.code); reset(); }} title={`${s.listings} listagem(ns)`}>
                <b>@{s.code}</b> {s.label}
              </button>
            ))}
          </div>

          {visible.length === 0 ? (
            <EmptyState title="Nada nesta busca">O banco de dados do CBK não tem listagens com esses filtros.</EmptyState>
          ) : (
            <div className={styles.listings}>
              {visible.map((l) => (
                <ListingCard key={l.id} listing={l} item={l.itemId != null ? byId.get(l.itemId) : undefined} gm={gm} open={open}
                  onBuy={() => setBuying(l)} onEdit={() => setEditing(l)} onDetail={l.itemId != null ? () => setDetail(l.itemId!) : undefined} />
              ))}
            </div>
          )}
          {pages > 1 && (
            <div className={clsx(styles.pager, styles.cbkBar)}>
              <Button disabled={current === 0} onClick={() => setPageIndex(current - 1)}>Anterior</Button>
              <span className={styles.source}>{current + 1} / {pages} · {filtered.length} listagens</span>
              <Button disabled={current >= pages - 1} onClick={() => setPageIndex(current + 1)}>Próxima</Button>
            </div>
          )}
        </section>

        <aside className={styles.aside}>
          <MarketInfo market={market} />
          {gm && <SlotsPanel market={market} onFill={setFilling} />}
          <EventsPanel market={market} rules={rules} />
          <LedgerPanel market={market} />
          <LogPanel market={market} />
        </aside>
      </div>

      {buying && <BuyDialog market={market} listing={buying} rules={rules} onClose={() => setBuying(null)} />}
      {editing && <EditListingDialog market={market} listing={editing} rules={rules} onClose={() => setEditing(null)} />}
      {filling && <FillSlotDialog market={market} slot={filling} onClose={() => setFilling(null)} />}
      {adding && <AddListingDialog market={market} onClose={() => setAdding(false)} />}
      <ItemDetailModal itemId={detail} onClose={() => setDetail(null)} />
    </div>
  );
}

/** A CBK listing (BC p.8): picture, name and maker, asking price, sales pitch, Buy Now!!! and where to pick it up. */
function ListingCard({ listing, item, gm, open, onBuy, onEdit, onDetail }: {
  listing: Listing; item?: Item; gm: boolean; open: boolean; onBuy: () => void; onEdit: () => void; onDetail?: () => void;
}) {
  const condition = shownCondition(listing, gm);
  const quality = qualityLabel(listing.quality);
  const soldOut = listing.stock === 0;
  const discounted = condition !== 'Normal' && listing.price < listing.basePrice;
  return (
    <article className={styles.listing}>
      {item ? (
        <ItemArt item={item} size="card" className={styles.listingArt} />
      ) : (
        <div className={clsx(styles.listingArt, styles.row)} style={{ justifyContent: 'center' }} aria-hidden>
          <CategoryIcon icon={CATEGORY_ICONS[listing.categoryKey ?? 'gear'] ?? 'gear'} size={42} />
        </div>
      )}
      <div className={styles.listingBody}>
        {onDetail ? (
          <button type="button" className={styles.listingName} onClick={onDetail}>{listing.name}</button>
        ) : (
          <span className={styles.listingName}>{listing.name}</span>
        )}
        <span className={styles.maker}>
          {[quality, listing.manufacturer].filter(Boolean).join(' · ') || ORIGIN_LABEL[listing.origin]}
        </span>
        <span className={styles.price}>
          {formatEb(listing.price)}eb
          {discounted && <s>{formatEb(listing.basePrice)}eb</s>}
          <small>{PRICE_CATEGORY_LABEL[listing.priceCategory]}</small>
        </span>
        <div className={styles.tags}>
          {condition !== 'Normal' && <Badge tone="red">{CONDITION_LABEL[condition]}</Badge>}
          {gm && listing.conditionHidden && <Badge tone="amber" title="Vendido como novo">golpe</Badge>}
          {listing.stock != null && <Badge>{listing.stock} em estoque</Badge>}
          {gm && <Badge tone="cyan" title={listing.group ?? undefined}>{ORIGIN_LABEL[listing.origin]}</Badge>}
        </div>
        {listing.description && <p className={styles.pitch}>{listing.description}</p>}
        {listing.note && <p className={styles.muted}>{listing.note}</p>}
        {gm && listing.warnings.map((w, i) => <p key={i} className={styles.warn}>{w}</p>)}
        <div className={styles.listingFoot}>
          <button type="button" className={styles.buyNow} onClick={onBuy} disabled={!open || !listing.forSale || soldOut}>
            {!listing.forSale ? (listing.origin === 'Preset' ? 'Só para uso no local' : 'Fora de venda') : soldOut ? 'Esgotado' : 'Buy Now!!!'}
          </button>
          <span className={styles.pickup}>Pick-up or Browse at Booth <b>{listing.stall}</b></span>
          {listing.source && <span className={styles.source}>{listing.source}</span>}
          {gm && (
            <div className={styles.gmTools}>
              <Button variant="ghost" icon={<Pencil size={13} />} onClick={onEdit}>Editar</Button>
            </div>
          )}
        </div>
      </div>
    </article>
  );
}
