import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Dices, Search, ShoppingCart } from 'lucide-react';
import { problemMessage } from '../../api/characters';
import { useGlobalSearch, useItemDetail } from '../../api/hooks';
import { marketApi, useMarketCommand, useMarketIndex } from '../../api/nightMarket';
import type { HaggleResult, ItemCondition, Listing, NightMarketDetail, NightMarketRules, OpenSlot } from '../../api/nightMarketTypes';
import { Button, Checkbox, Field, Modal, NumberInput, Segmented, Select } from '../../components/ui/ui';
import { formatEb, PRICE_CATEGORY_LABEL } from '../../lib/format';
import { useDebouncedValue } from '../../lib/hooks';
import { useToast } from '../../state/ToastContext';
import { CONDITION_LABEL, type HaggleState, initialHaggle, qualityLabel, toHaggleRequest } from './nightMarket';
import styles from './NightMarket.module.css';
import { t, tv } from '../../i18n';

// ------------------------------------------------------------------ Haggle

/**
 * The Haggle opposed check of CP:R p.159: COOL + Trading + Operator Rank + 1d10 on each side. The result lists the
 * deals of the buyer's Rank or lower; `onDeal` receives the chosen one with the dice that were rolled.
 */
export function HagglePanel({ rules, state, onChange, result, onResult, deal, onDeal, buyerLabel = t('Quem pechincha', 'Who haggles'), sellerLabel = t('Outro lado', 'Other side') }: {
  rules: NightMarketRules;
  state: HaggleState;
  onChange: (s: HaggleState) => void;
  result: HaggleResult | null;
  onResult: (r: HaggleResult | null) => void;
  deal: string | null;
  onDeal: (id: string | null) => void;
  buyerLabel?: string;
  sellerLabel?: string;
}) {
  const toast = useToast();
  const roll = useMutation({
    mutationFn: () => marketApi.haggle(toHaggleRequest(state)),
    onSuccess: (r) => { onResult(r); onDeal(null); },
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });
  const op = rules.rules.operator;
  const side = (key: 'buyer' | 'seller', title: string, dice: 'buyerDice' | 'sellerDice') => (
    <div className={styles.side}>
      <span className={styles.sideTitle}>{title}</span>
      <Field label="COOL + Trading (base)">
        <NumberInput className={styles.number} min={0} max={40} value={state[key].trading}
          onChange={(v) => { onChange({ ...state, [key]: { ...state[key], trading: v ?? 0 } }); onResult(null); }} />
      </Field>
      <Field label="Operator Rank" hint={key === 'buyer' ? t('Só um Fixer (Rank 1+) faz acordos.', 'Only a Fixer (Rank 1+) makes deals.') : t('0 se não for Fixer.', '0 if not a Fixer.')}>
        <NumberInput className={styles.number} min={0} max={10} value={state[key].rank}
          onChange={(v) => { onChange({ ...state, [key]: { ...state[key], rank: v ?? 0 } }); onResult(null); }} />
      </Field>
      <Field label={t('d10 (opcional)', 'd10 (optional)')} hint={t('Dados físicos: o d10 e, se der 10 ou 1, o extra. Vazio: o servidor rola.', 'Physical dice: the d10 and, on a 10 or 1, the extra one. Empty: the server rolls.')}>
        <input className={styles.text} value={state[dice]} placeholder={t('ex.: 7 ou 10 4', 'e.g. 7 or 10 4')}
          onChange={(e) => { onChange({ ...state, [dice]: e.target.value }); onResult(null); }} />
      </Field>
    </div>
  );
  return (
    <div className={styles.dialog}>
      <p className={styles.muted}>{tv(op.haggleFormula.value)} <span className={styles.source}>({op.haggleFormula.reference})</span></p>
      <div className={styles.haggle}>
        {side('buyer', buyerLabel, 'buyerDice')}
        {side('seller', sellerLabel, 'sellerDice')}
      </div>
      <div className={styles.row}>
        <Button icon={<Dices size={15} />} onClick={() => roll.mutate()} disabled={roll.isPending}>{t('Rolar Haggle', 'Roll Haggle')}</Button>
        <span className={styles.source}>{t('Empate fica com o Defensor (', 'Ties go to the Defender (')}{op.haggleTie.reference}). {op.criticals.reference}</span>
      </div>
      {result && (
        <>
          <div className={`${styles.result} ${result.success ? styles.resultWin : ''}`}>
            <b>{result.buyerTotal}</b> (d10 {result.buyerRolls.join(' + ')}{t(') contra ', ') versus ')}<b>{result.sellerTotal}</b> (d10 {result.sellerRolls.join(' + ')}). {tv(result.summary)}
          </div>
          {result.deals.length > 0 && (
            <div className={styles.deals} role="radiogroup" aria-label={t('Acordo', 'Deal')}>
              <label className={styles.deal}>
                <input type="radio" name="deal" checked={deal === null} onChange={() => onDeal(null)} />
                <span>{t('Sem acordo (preço de mercado)', 'No deal (market price)')}</span>
              </label>
              {result.deals.map((d) => (
                <label key={d.id} className={`${styles.deal} ${d.applies ? '' : styles.dealOff}`} title={tv(d.text)}>
                  <input type="radio" name="deal" disabled={!d.applies} checked={deal === d.id} onChange={() => onDeal(d.id)} />
                  <span><b>Rank {d.minRank}+ · {tv(d.label)}</b>{d.reason ? <><br /><span className={styles.muted}>{tv(d.reason)}</span></> : null}</span>
                </label>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}

// ------------------------------------------------------------------ Buy Now!!!

export function BuyDialog({ market, listing, rules, onClose }: {
  market: NightMarketDetail; listing: Listing; rules: NightMarketRules; onClose: () => void;
}) {
  const toast = useToast();
  const command = useMarketCommand(market.id);
  const [quantity, setQuantity] = useState(1);
  const [buyer, setBuyer] = useState('');
  const [haggling, setHaggling] = useState(false);
  const [haggle, setHaggle] = useState<HaggleState>(() => initialHaggle(market));
  const [result, setResult] = useState<HaggleResult | null>(null);
  const [deal, setDeal] = useState<string | null>(null);
  const quality = qualityLabel(listing.quality);
  const max = listing.stock ?? 99;

  const buy = () => command.mutate({
    path: 'purchase',
    body: {
      listingId: listing.id, quantity, buyer: buyer || null,
      haggle: haggling && result ? { ...toHaggleRequest(haggle), buyerRolls: result.buyerRolls, sellerRolls: result.sellerRolls } : null,
      dealId: haggling && result ? deal : null,
    },
  }, {
    onSuccess: (answer) => {
      const detail = 'market' in answer ? answer.market : answer;
      toast.notify(t(`Comprado: ${quantity}× ${listing.name}. Retire no booth ${listing.stall}.`, `Bought: ${quantity}× ${listing.name}. Pick it up at booth ${listing.stall}.`));
      detail.notices.forEach((n) => toast.notify(n, 'info'));
      onClose();
    },
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });

  return (
    <Modal open onClose={onClose} title={<>Buy Now!!! · {tv(listing.name)}{quality ? ` (${quality})` : ''}</>} wide>
      <div className={styles.dialog}>
        <div className={styles.row}>
          <span className={styles.price}>{formatEb(listing.price)}eb <small>{PRICE_CATEGORY_LABEL[listing.priceCategory]}</small></span>
          <span className={styles.pickup}>Pick-up or Browse at Booth <b>{listing.stall}</b></span>
        </div>
        <div className={styles.grid2}>
          <Field label={t('Quantidade', 'Quantity')} hint={listing.stock != null ? t(`${listing.stock} em estoque`, `${listing.stock} in stock`) : t('Estoque não controlado (CP:R p.338)', 'Stock not tracked (CP:R p.338)')}>
            <NumberInput className={styles.number} min={1} max={max} value={quantity} onChange={(v) => setQuantity(v ?? 1)} />
          </Field>
          <Field label={t('Comprador', 'Buyer')}>
            <input className={styles.text} value={buyer} onChange={(e) => setBuyer(e.target.value)} placeholder={t('Nome ou handle', 'Name or handle')} data-autofocus />
          </Field>
        </div>
        <Checkbox checked={haggling} onChange={(v) => { setHaggling(v); setResult(null); setDeal(null); }}
          label={t('Pechinchar (Haggle de um Fixer)', 'Haggle (a Fixer\'s Haggle)')} />
        {haggling && (
          <HagglePanel rules={rules} state={haggle} onChange={setHaggle} result={result} onResult={setResult} deal={deal} onDeal={setDeal}
            buyerLabel={t('Comprador (Fixer)', 'Buyer (Fixer)')} sellerLabel={t(`Vendedor: ${market.organizer.name}`, `Seller: ${market.organizer.name}`)} />
        )}
        <p className={styles.source}>{tv(rules.rules.blackChrome.buyNow.value)} ({rules.rules.blackChrome.buyNow.reference})</p>
        <div className={styles.row}>
          <Button variant="primary" icon={<ShoppingCart size={15} />} onClick={buy}
            disabled={command.isPending || (haggling && !result)}>
            {t('Comprar ', 'Buy ')}{quantity > 1 ? `${quantity}× ` : ''}{t('por ', 'for ')}{formatEb(listing.price * quantity)}eb{deal ? t(' (com acordo)', ' (with a deal)') : ''}
          </Button>
          <Button variant="ghost" onClick={onClose}>{t('Cancelar', 'Cancel')}</Button>
        </div>
      </div>
    </Modal>
  );
}

// ------------------------------------------------------------------ GM: edit a listing

const CONDITIONS: ItemCondition[] = ['Normal', 'Damaged', 'Destroyed', 'BeyondRepair'];

export function EditListingDialog({ market, listing, rules, onClose }: {
  market: NightMarketDetail; listing: Listing; rules: NightMarketRules; onClose: () => void;
}) {
  const toast = useToast();
  const command = useMarketCommand(market.id);
  const [condition, setCondition] = useState<ItemCondition>(listing.condition);
  const [hidden, setHidden] = useState(listing.conditionHidden);
  const [stock, setStock] = useState<number | null>(listing.stock ?? null);
  const [forSale, setForSale] = useState(listing.forSale);
  const [stall, setStall] = useState(listing.stall);
  const [note, setNote] = useState(listing.note ?? '');
  const [price, setPrice] = useState<number | null>(listing.basePrice);
  const c = rules.rules.condition;
  const fail = (e: unknown) => toast.notify(problemMessage(e), 'error');

  const submit = async () => {
    try {
      if (price !== null && price !== listing.basePrice)
        await command.mutateAsync({ path: `listings/${listing.id}/price`, body: { price } });
      await command.mutateAsync({
        path: `listings/${listing.id}`, method: 'PUT',
        body: { stall, condition, conditionHidden: condition !== 'Normal' && hidden, stock, forSale, note: note || null },
      });
      onClose();
    } catch (e) {
      fail(e);
    }
  };

  const remove = () => command.mutate({ path: `listings/${listing.id}/remove` }, { onSuccess: onClose, onError: fail });

  return (
    <Modal open onClose={onClose} title={<>{t('Listagem · ', 'Listing · ')}{tv(listing.name)}</>} wide>
      <div className={styles.dialog}>
        <Field label={t('Condição', 'Condition')} hint={t(`Damaged: ${tv(c.damagedSteps.value)} categoria abaixo; Destroyed: ${tv(c.destroyedSteps.value)} (${c.damagedSteps.reference}).`, `Damaged: ${tv(c.damagedSteps.value)} category lower; Destroyed: ${tv(c.destroyedSteps.value)} (${c.damagedSteps.reference}).`)}>
          <Segmented ariaLabel={t('Condição', 'Condition')} value={condition} onChange={setCondition}
            options={CONDITIONS.map((x) => ({ value: x, label: CONDITION_LABEL[x] }))} />
        </Field>
        {condition !== 'Normal' && (
          <Checkbox checked={hidden} onChange={setHidden}
            label={t(`Golpe: vender como novo, a preço cheio (${c.sellingIsGmCall.reference}). Fixers de Operator ${tv(c.detectRank.value)}+ percebem.`, `Scam: sell as new, at full price (${c.sellingIsGmCall.reference}). Fixers with Operator ${tv(c.detectRank.value)}+ notice.`)} />
        )}
        <div className={styles.grid3}>
          <Field label="Booth" hint={t(`Letra do mercado (${market.preset.letter}) + número.`, `Market letter (${market.preset.letter}) + number.`)}>
            <Select value={stall} onChange={(e) => setStall(e.target.value)}>
              {market.stalls.map((s) => <option key={s.code} value={s.code}>{s.code} · {tv(s.label)}</option>)}
            </Select>
          </Field>
          <Field label={t('Estoque', 'Stock')} hint={t('Vazio: não controlado.', 'Empty: not tracked.')}>
            <NumberInput className={styles.number} min={0} max={9999} allowEmpty value={stock} onChange={setStock} />
          </Field>
          <Field label={t('Preço base (eb)', 'Base price (eb)')} hint={t('Preço combinado na mesa (CP:R p.385).', 'Price agreed at the table (CP:R p.385).')}>
            <NumberInput className={styles.number} min={0} max={10_000_000} value={price} onChange={setPrice} />
          </Field>
        </div>
        <Checkbox checked={forSale} onChange={setForSale} label={t('À venda', 'For sale')} />
        <Field label={t('Nota', 'Note')}>
          <input className={styles.text} value={note} onChange={(e) => setNote(e.target.value)} />
        </Field>
        <div className={styles.row}>
          <Button variant="primary" onClick={submit} disabled={command.isPending}>{t('Salvar', 'Save')}</Button>
          <Button variant="danger" onClick={remove} disabled={command.isPending}>{t('Remover do mercado', 'Remove from the market')}</Button>
          <Button variant="ghost" onClick={onClose}>{t('Cancelar', 'Cancel')}</Button>
        </div>
      </div>
    </Modal>
  );
}

// ------------------------------------------------------------------ GM: add a listing

export function AddListingDialog({ market, onClose }: { market: NightMarketDetail; onClose: () => void }) {
  const toast = useToast();
  const command = useMarketCommand(market.id);
  const [source, setSource] = useState<'catalog' | 'index' | 'free'>('catalog');
  const [q, setQ] = useState('');
  const debounced = useDebouncedValue(q, 250);
  const catalog = useGlobalSearch(source === 'catalog' ? debounced : '');
  const index = useMarketIndex({ q: debounced, catalogued: false, page: 1, pageSize: 30 });
  const [stall, setStall] = useState(market.stalls[0]?.code ?? `${market.preset.letter}1`);
  const [name, setName] = useState('');
  const [price, setPrice] = useState<number | null>(null);

  const add = (body: Record<string, unknown>) => command.mutate({ path: 'listings', body: { stall, ...body } }, {
    onSuccess: () => { toast.notify(t('Listagem adicionada.', 'Listing added.')); onClose(); },
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });

  return (
    <Modal open onClose={onClose} title={t('Adicionar listagem', 'Add listing')} wide>
      <div className={styles.dialog}>
        <div className={styles.row}>
          <Segmented ariaLabel={t('Origem', 'Source')} value={source} onChange={setSource} options={[
            { value: 'catalog', label: t('Catálogo', 'Catalog') },
            { value: 'index', label: t('Só no Night Market Index', 'Only in the Night Market Index') },
            { value: 'free', label: t('Nome e preço', 'Name and price') },
          ]} />
          <Field label="Booth">
            <input className={styles.number} value={stall} onChange={(e) => setStall(e.target.value.toUpperCase())} />
          </Field>
        </div>
        {source !== 'free' ? (
          <>
            <div className={styles.row}>
              <Search size={15} aria-hidden />
              <input className={styles.search} value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('Buscar pelo nome', 'Search by name')} data-autofocus />
            </div>
            <div className={styles.picker}>
              {source === 'catalog'
                ? (catalog.data ?? []).flatMap((g) => g.items).map((item) => (
                  <button key={item.id} type="button" className={styles.pickRow} onClick={() => add({ itemId: item.id })}>
                    <span>{tv(item.name)}</span>
                    <span className={styles.source}>{item.price != null ? `${formatEb(item.price)}eb` : '—'} · {tv(item.source?.name)}</span>
                  </button>
                ))
                : (index.data?.rows ?? []).map((row) => (
                  <button key={row.row} type="button" className={styles.pickRow} onClick={() => add({ indexRow: row.row })}
                    disabled={row.priceEb == null}>
                    <span>{tv(row.name)} <span className={styles.muted}>· {tv(row.group)}</span></span>
                    <span className={styles.source}>{row.cost} · {row.sources.map((s) => `${s.book} ${s.page ?? ''}`).join(', ')}</span>
                  </button>
                ))}
            </div>
          </>
        ) : (
          <div className={styles.grid2}>
            <Field label={t('Nome', 'Name')}><input className={styles.text} value={name} onChange={(e) => setName(e.target.value)} /></Field>
            <Field label={t('Preço (eb)', 'Price (eb)')}>
              <NumberInput className={styles.number} min={0} max={10_000_000} allowEmpty value={price} onChange={setPrice} />
            </Field>
            <Button variant="primary" disabled={!name.trim() || price === null} onClick={() => add({ name, price })}>{t('Adicionar', 'Add')}</Button>
          </div>
        )}
      </div>
    </Modal>
  );
}

// ------------------------------------------------------------------ GM: fill a "GM's choice" slot

export function FillSlotDialog({ market, slot, onClose }: { market: NightMarketDetail; slot: OpenSlot; onClose: () => void }) {
  const toast = useToast();
  const command = useMarketCommand(market.id);
  const [q, setQ] = useState('');
  const debounced = useDebouncedValue(q, 250);
  const search = useGlobalSearch(debounced);
  const [picked, setPicked] = useState<number | null>(null);
  const detail = useItemDetail(picked);

  const fill = (body: Record<string, unknown>) => command.mutate({ path: `slots/${slot.id}/fill`, body }, {
    onSuccess: (answer) => {
      const d = 'market' in answer ? answer.market : answer;
      d.notices.forEach((n) => toast.notify(n, 'info'));
      onClose();
    },
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });

  return (
    <Modal open onClose={onClose} title={<>{tv(slot.label)} {t('· booth ', '· booth ')}{slot.stall}</>} wide>
      <div className={styles.dialog}>
        <div className={styles.row}>
          <Button icon={<Dices size={15} />} onClick={() => fill({ draw: true })} disabled={command.isPending}>{t('Sortear do acervo', 'Draw from the collection')}</Button>
          <span className={styles.muted}>{t('ou escolha um item:', 'or pick an item:')}</span>
        </div>
        <input className={styles.search} value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('Buscar no catálogo', 'Search the catalog')} data-autofocus />
        <div className={styles.picker}>
          {(search.data ?? []).flatMap((g) => g.items).map((item) => (
            <button key={item.id} type="button" className={`${styles.pickRow} ${picked === item.id ? styles.pickActive : ''}`}
              onClick={() => setPicked(item.id)}>
              <span>{tv(item.name)}</span>
              <span className={styles.source}>{item.price != null ? `${formatEb(item.price)}eb` : '—'} · {tv(item.source?.name)}</span>
            </button>
          ))}
        </div>
        {detail.data && <p className={styles.pitch}>{tv(detail.data.item.description)}</p>}
        <Button variant="primary" disabled={picked === null || command.isPending} onClick={() => fill({ itemId: picked })}>
          {t('Pôr à venda', 'Put on sale')}
        </Button>
      </div>
    </Modal>
  );
}
