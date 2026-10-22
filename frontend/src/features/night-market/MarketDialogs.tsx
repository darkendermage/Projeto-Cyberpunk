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

// ------------------------------------------------------------------ Haggle

/**
 * The Haggle opposed check of CP:R p.159: COOL + Trading + Operator Rank + 1d10 on each side. The result lists the
 * deals of the buyer's Rank or lower; `onDeal` receives the chosen one with the dice that were rolled.
 */
export function HagglePanel({ rules, state, onChange, result, onResult, deal, onDeal, buyerLabel = 'Quem pechincha', sellerLabel = 'Outro lado' }: {
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
      <Field label="Operator Rank" hint={key === 'buyer' ? 'Só um Fixer (Rank 1+) faz acordos.' : '0 se não for Fixer.'}>
        <NumberInput className={styles.number} min={0} max={10} value={state[key].rank}
          onChange={(v) => { onChange({ ...state, [key]: { ...state[key], rank: v ?? 0 } }); onResult(null); }} />
      </Field>
      <Field label="d10 (opcional)" hint="Dados físicos: o d10 e, se der 10 ou 1, o extra. Vazio: o servidor rola.">
        <input className={styles.text} value={state[dice]} placeholder="ex.: 7 ou 10 4"
          onChange={(e) => { onChange({ ...state, [dice]: e.target.value }); onResult(null); }} />
      </Field>
    </div>
  );
  return (
    <div className={styles.dialog}>
      <p className={styles.muted}>{op.haggleFormula.value} <span className={styles.source}>({op.haggleFormula.reference})</span></p>
      <div className={styles.haggle}>
        {side('buyer', buyerLabel, 'buyerDice')}
        {side('seller', sellerLabel, 'sellerDice')}
      </div>
      <div className={styles.row}>
        <Button icon={<Dices size={15} />} onClick={() => roll.mutate()} disabled={roll.isPending}>Rolar Haggle</Button>
        <span className={styles.source}>Empate fica com o Defensor ({op.haggleTie.reference}). {op.criticals.reference}</span>
      </div>
      {result && (
        <>
          <div className={`${styles.result} ${result.success ? styles.resultWin : ''}`}>
            <b>{result.buyerTotal}</b> (d10 {result.buyerRolls.join(' + ')}) contra <b>{result.sellerTotal}</b> (d10 {result.sellerRolls.join(' + ')}). {result.summary}
          </div>
          {result.deals.length > 0 && (
            <div className={styles.deals} role="radiogroup" aria-label="Acordo">
              <label className={styles.deal}>
                <input type="radio" name="deal" checked={deal === null} onChange={() => onDeal(null)} />
                <span>Sem acordo (preço de mercado)</span>
              </label>
              {result.deals.map((d) => (
                <label key={d.id} className={`${styles.deal} ${d.applies ? '' : styles.dealOff}`} title={d.text}>
                  <input type="radio" name="deal" disabled={!d.applies} checked={deal === d.id} onChange={() => onDeal(d.id)} />
                  <span><b>Rank {d.minRank}+ · {d.label}</b>{d.reason ? <><br /><span className={styles.muted}>{d.reason}</span></> : null}</span>
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
      toast.notify(`Comprado: ${quantity}× ${listing.name}. Retire no booth ${listing.stall}.`);
      detail.notices.forEach((n) => toast.notify(n, 'info'));
      onClose();
    },
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });

  return (
    <Modal open onClose={onClose} title={<>Buy Now!!! · {listing.name}{quality ? ` (${quality})` : ''}</>} wide>
      <div className={styles.dialog}>
        <div className={styles.row}>
          <span className={styles.price}>{formatEb(listing.price)}eb <small>{PRICE_CATEGORY_LABEL[listing.priceCategory]}</small></span>
          <span className={styles.pickup}>Pick-up or Browse at Booth <b>{listing.stall}</b></span>
        </div>
        <div className={styles.grid2}>
          <Field label="Quantidade" hint={listing.stock != null ? `${listing.stock} em estoque` : 'Estoque não controlado (CP:R p.338)'}>
            <NumberInput className={styles.number} min={1} max={max} value={quantity} onChange={(v) => setQuantity(v ?? 1)} />
          </Field>
          <Field label="Comprador">
            <input className={styles.text} value={buyer} onChange={(e) => setBuyer(e.target.value)} placeholder="Nome ou handle" data-autofocus />
          </Field>
        </div>
        <Checkbox checked={haggling} onChange={(v) => { setHaggling(v); setResult(null); setDeal(null); }}
          label="Pechinchar (Haggle de um Fixer)" />
        {haggling && (
          <HagglePanel rules={rules} state={haggle} onChange={setHaggle} result={result} onResult={setResult} deal={deal} onDeal={setDeal}
            buyerLabel="Comprador (Fixer)" sellerLabel={`Vendedor: ${market.organizer.name}`} />
        )}
        <p className={styles.source}>{rules.rules.blackChrome.buyNow.value} ({rules.rules.blackChrome.buyNow.reference})</p>
        <div className={styles.row}>
          <Button variant="primary" icon={<ShoppingCart size={15} />} onClick={buy}
            disabled={command.isPending || (haggling && !result)}>
            Comprar {quantity > 1 ? `${quantity}× ` : ''}por {formatEb(listing.price * quantity)}eb{deal ? ' (com acordo)' : ''}
          </Button>
          <Button variant="ghost" onClick={onClose}>Cancelar</Button>
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
    <Modal open onClose={onClose} title={<>Listagem · {listing.name}</>} wide>
      <div className={styles.dialog}>
        <Field label="Condição" hint={`Damaged: ${c.damagedSteps.value} categoria abaixo; Destroyed: ${c.destroyedSteps.value} (${c.damagedSteps.reference}).`}>
          <Segmented ariaLabel="Condição" value={condition} onChange={setCondition}
            options={CONDITIONS.map((x) => ({ value: x, label: CONDITION_LABEL[x] }))} />
        </Field>
        {condition !== 'Normal' && (
          <Checkbox checked={hidden} onChange={setHidden}
            label={`Golpe: vender como novo, a preço cheio (${c.sellingIsGmCall.reference}). Fixers de Operator ${c.detectRank.value}+ percebem.`} />
        )}
        <div className={styles.grid3}>
          <Field label="Booth" hint={`Letra do mercado (${market.preset.letter}) + número.`}>
            <Select value={stall} onChange={(e) => setStall(e.target.value)}>
              {market.stalls.map((s) => <option key={s.code} value={s.code}>{s.code} · {s.label}</option>)}
            </Select>
          </Field>
          <Field label="Estoque" hint="Vazio: não controlado.">
            <NumberInput className={styles.number} min={0} max={9999} allowEmpty value={stock} onChange={setStock} />
          </Field>
          <Field label="Preço base (eb)" hint="Preço combinado na mesa (CP:R p.385).">
            <NumberInput className={styles.number} min={0} max={10_000_000} value={price} onChange={setPrice} />
          </Field>
        </div>
        <Checkbox checked={forSale} onChange={setForSale} label="À venda" />
        <Field label="Nota">
          <input className={styles.text} value={note} onChange={(e) => setNote(e.target.value)} />
        </Field>
        <div className={styles.row}>
          <Button variant="primary" onClick={submit} disabled={command.isPending}>Salvar</Button>
          <Button variant="danger" onClick={remove} disabled={command.isPending}>Remover do mercado</Button>
          <Button variant="ghost" onClick={onClose}>Cancelar</Button>
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
    onSuccess: () => { toast.notify('Listagem adicionada.'); onClose(); },
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });

  return (
    <Modal open onClose={onClose} title="Adicionar listagem" wide>
      <div className={styles.dialog}>
        <div className={styles.row}>
          <Segmented ariaLabel="Origem" value={source} onChange={setSource} options={[
            { value: 'catalog', label: 'Catálogo' },
            { value: 'index', label: 'Só no Night Market Index' },
            { value: 'free', label: 'Nome e preço' },
          ]} />
          <Field label="Booth">
            <input className={styles.number} value={stall} onChange={(e) => setStall(e.target.value.toUpperCase())} />
          </Field>
        </div>
        {source !== 'free' ? (
          <>
            <div className={styles.row}>
              <Search size={15} aria-hidden />
              <input className={styles.search} value={q} onChange={(e) => setQ(e.target.value)} placeholder="Buscar pelo nome" data-autofocus />
            </div>
            <div className={styles.picker}>
              {source === 'catalog'
                ? (catalog.data ?? []).flatMap((g) => g.items).map((item) => (
                  <button key={item.id} type="button" className={styles.pickRow} onClick={() => add({ itemId: item.id })}>
                    <span>{item.name}</span>
                    <span className={styles.source}>{item.price != null ? `${formatEb(item.price)}eb` : '—'} · {item.source?.name}</span>
                  </button>
                ))
                : (index.data?.rows ?? []).map((row) => (
                  <button key={row.row} type="button" className={styles.pickRow} onClick={() => add({ indexRow: row.row })}
                    disabled={row.priceEb == null}>
                    <span>{row.name} <span className={styles.muted}>· {row.group}</span></span>
                    <span className={styles.source}>{row.cost} · {row.sources.map((s) => `${s.book} ${s.page ?? ''}`).join(', ')}</span>
                  </button>
                ))}
            </div>
          </>
        ) : (
          <div className={styles.grid2}>
            <Field label="Nome"><input className={styles.text} value={name} onChange={(e) => setName(e.target.value)} /></Field>
            <Field label="Preço (eb)">
              <NumberInput className={styles.number} min={0} max={10_000_000} allowEmpty value={price} onChange={setPrice} />
            </Field>
            <Button variant="primary" disabled={!name.trim() || price === null} onClick={() => add({ name, price })}>Adicionar</Button>
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
    <Modal open onClose={onClose} title={<>{slot.label} · booth {slot.stall}</>} wide>
      <div className={styles.dialog}>
        <div className={styles.row}>
          <Button icon={<Dices size={15} />} onClick={() => fill({ draw: true })} disabled={command.isPending}>Sortear do acervo</Button>
          <span className={styles.muted}>ou escolha um item:</span>
        </div>
        <input className={styles.search} value={q} onChange={(e) => setQ(e.target.value)} placeholder="Buscar no catálogo" data-autofocus />
        <div className={styles.picker}>
          {(search.data ?? []).flatMap((g) => g.items).map((item) => (
            <button key={item.id} type="button" className={`${styles.pickRow} ${picked === item.id ? styles.pickActive : ''}`}
              onClick={() => setPicked(item.id)}>
              <span>{item.name}</span>
              <span className={styles.source}>{item.price != null ? `${formatEb(item.price)}eb` : '—'} · {item.source?.name}</span>
            </button>
          ))}
        </div>
        {detail.data && <p className={styles.pitch}>{detail.data.item.description}</p>}
        <Button variant="primary" disabled={picked === null || command.isPending} onClick={() => fill({ itemId: picked })}>
          Pôr à venda
        </Button>
      </div>
    </Modal>
  );
}
