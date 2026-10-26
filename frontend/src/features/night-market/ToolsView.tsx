import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { HandCoins, Package } from 'lucide-react';
import { problemMessage } from '../../api/characters';
import { useGlobalSearch } from '../../api/hooks';
import { marketApi } from '../../api/nightMarket';
import type {
  ConditionDisclosure, HaggleResult, ItemCondition, MysteryBoxResult, NightMarketRules, SellQuote,
} from '../../api/nightMarketTypes';
import { Button, Checkbox, Collapsible, Field, NumberInput, Segmented } from '../../components/ui/ui';
import { formatEb, PRICE_CATEGORY_LABEL } from '../../lib/format';
import { useDebouncedValue } from '../../lib/hooks';
import { useToast } from '../../state/ToastContext';
import { HagglePanel } from './MarketDialogs';
import { MysteryBoxResultView } from './MarketPanels';
import { CONDITION_LABEL, type HaggleState, initialHaggle, toHaggleRequest } from './nightMarket';
import styles from './NightMarket.module.css';
import { t, tv } from '../../i18n';

/** Tools that don't need a market: selling to a Fixer (IR5 p.5), a Haggle check and the Table of Mystery (IR4 p.27). */
export function ToolsView({ rules }: { rules: NightMarketRules }) {
  return (
    <div className={styles.grid2}>
      <SellToFixer rules={rules} />
      <div className={styles.dialog}>
        <MysteryBoxRoller rules={rules} />
        <HaggleCalculator rules={rules} />
      </div>
    </div>
  );
}

const CONDITIONS: ItemCondition[] = ['Normal', 'Damaged', 'Destroyed', 'BeyondRepair'];

function SellToFixer({ rules }: { rules: NightMarketRules }) {
  const toast = useToast();
  const [q, setQ] = useState('');
  const debounced = useDebouncedValue(q, 250);
  const search = useGlobalSearch(debounced);
  const [itemId, setItemId] = useState<number | null>(null);
  const [itemName, setItemName] = useState('');
  const [price, setPrice] = useState<number | null>(null);
  const [condition, setCondition] = useState<ItemCondition>('Damaged');
  const [disclosure, setDisclosure] = useState<ConditionDisclosure>('Disclosed');
  const [fixerRank, setFixerRank] = useState<number | null>(4);
  const [haggling, setHaggling] = useState(false);
  const [haggle, setHaggle] = useState<HaggleState>(() => initialHaggle());
  const [result, setResult] = useState<HaggleResult | null>(null);
  const [deal, setDeal] = useState<string | null>(null);
  const [quote, setQuote] = useState<SellQuote | null>(null);
  const c = rules.rules.condition;

  const ask = useMutation({
    mutationFn: () => marketApi.sellQuote({
      itemId, price: itemId ? null : price, condition, disclosure, fixerRank,
      haggle: haggling && result ? { ...toHaggleRequest(haggle), buyerRolls: result.buyerRolls, sellerRolls: result.sellerRolls } : null,
      dealId: haggling && result ? deal : null,
    }),
    onSuccess: setQuote,
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });

  return (
    <Collapsible title={t('Vender ao Fixer', 'Sell to a Fixer')} defaultOpen>
      <div className={styles.dialog}>
        <p className={styles.muted}>
          {t('Damaged vale ', 'Damaged is worth ')}{tv(c.damagedSteps.value)} {t('categoria abaixo; Destroyed, ', 'category lower; Destroyed, ')}{tv(c.destroyedSteps.value)}{t('. Fixers de Operator ', '. Fixers with Operator ')}{tv(c.detectRank.value)}{t('+ percebem o dano se ele não foi disfarçado (', '+ notice the damage if it wasn\'t disguised (')}{c.damagedSteps.reference}).
        </p>
        <Field label={t('Item do catálogo', 'Catalog item')} hint={itemId ? t(`Escolhido: ${itemName}`, `Chosen: ${itemName}`) : t('Ou informe só o preço de mercado abaixo.', 'Or just enter the market price below.')}>
          <input className={styles.search} value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('Buscar item', 'Search item')} />
        </Field>
        {debounced && (
          <div className={styles.picker}>
            {(search.data ?? []).flatMap((g) => g.items).filter((i) => i.price != null).map((item) => (
              <button key={item.id} type="button" className={`${styles.pickRow} ${itemId === item.id ? styles.pickActive : ''}`}
                onClick={() => { setItemId(item.id); setItemName(item.name); setQ(''); }}>
                <span>{tv(item.name)}</span><span className={styles.source}>{formatEb(item.price)}eb</span>
              </button>
            ))}
          </div>
        )}
        {!itemId && (
          <Field label={t('Preço de mercado (eb)', 'Market price (eb)')}>
            <NumberInput className={styles.number} min={0} max={10_000_000} allowEmpty value={price} onChange={setPrice} />
          </Field>
        )}
        {itemId && <Button variant="ghost" onClick={() => { setItemId(null); setItemName(''); }}>{t('Trocar por preço livre', 'Switch to a free price')}</Button>}
        <Field label={t('Condição', 'Condition')}>
          <Segmented ariaLabel={t('Condição', 'Condition')} value={condition} onChange={setCondition} options={CONDITIONS.map((x) => ({ value: x, label: CONDITION_LABEL[x] }))} />
        </Field>
        {condition !== 'Normal' && (
          <Field label={t('O vendedor...', 'The seller...')}>
            <Segmented ariaLabel={t('Revelação', 'Disclosure')} value={disclosure} onChange={setDisclosure} options={[
              { value: 'Disclosed', label: t('avisa do dano', 'discloses the damage') },
              { value: 'Silent', label: t('fica quieto', 'keeps quiet') },
              { value: 'Disguised', label: t('disfarça (Forgery)', 'disguises it (Forgery)') },
            ]} />
          </Field>
        )}
        <Field label={t('Operator Rank do Fixer que compra', 'Operator Rank of the buying Fixer')}>
          <NumberInput className={styles.number} min={0} max={10} allowEmpty value={fixerRank} onChange={setFixerRank} />
        </Field>
        <Checkbox checked={haggling} onChange={(v) => { setHaggling(v); setResult(null); setDeal(null); }} label={t('O vendedor é Fixer e pechincha (10% ou 20% a mais)', 'The seller is a Fixer and haggles (10% or 20% more)')} />
        {haggling && (
          <HagglePanel rules={rules} state={haggle} onChange={setHaggle} result={result} onResult={setResult} deal={deal} onDeal={setDeal}
            buyerLabel={t('Quem vende (Fixer)', 'Who sells (Fixer)')} sellerLabel={t('Fixer que compra', 'Buying Fixer')} />
        )}
        <Button variant="primary" icon={<HandCoins size={15} />} onClick={() => ask.mutate()} disabled={ask.isPending || (!itemId && price === null)}>
          {t('Quanto ele paga?', 'How much does he pay?')}
        </Button>
        {quote && (
          <div className={styles.prize} role="status">
            {quote.name && <b>{tv(quote.name)}: </b>}
            {t('mercado ', 'market ')}{formatEb(quote.marketPrice)}{t('eb (', 'eb (')}{PRICE_CATEGORY_LABEL[quote.marketCategory]}{t(') → oferta ', ') → offer ')}<strong>{formatEb(quote.offer)}eb</strong> ({PRICE_CATEGORY_LABEL[quote.offerCategory]})
            <ul>{quote.notes.map((n, i) => <li key={i} className={styles.muted}>{n}</li>)}</ul>
          </div>
        )}
      </div>
    </Collapsible>
  );
}

function MysteryBoxRoller({ rules }: { rules: NightMarketRules }) {
  const toast = useToast();
  const [box, setBox] = useState<number | null>(null);
  const [content, setContent] = useState<number | null>(null);
  const [result, setResult] = useState<MysteryBoxResult | null>(null);
  const roll = useMutation({
    mutationFn: () => marketApi.mysteryBox({ boxRoll: box, contentRoll: content }),
    onSuccess: setResult,
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });
  return (
    <Collapsible title="Mystery Box (Achievement)" defaultOpen>
      <div className={styles.dialog}>
        <p className={styles.muted}>{tv(rules.rules.mysteryBoxes.redemption.value)} <span className={styles.source}>({rules.rules.mysteryBoxes.redemption.reference})</span></p>
        <div className={styles.row}>
          <Field label={t('1d10 (caixa)', '1d10 (box)')} hint={t('Vazio: rolar.', 'Empty: roll.')}><NumberInput className={styles.number} min={1} max={10} allowEmpty value={box} onChange={setBox} /></Field>
          <Field label={t('1d6 (conteúdo)', '1d6 (contents)')} hint={t('Vazio: rolar.', 'Empty: roll.')}><NumberInput className={styles.number} min={1} max={6} allowEmpty value={content} onChange={setContent} /></Field>
          <Button icon={<Package size={15} />} onClick={() => roll.mutate()} disabled={roll.isPending}>{t('Abrir', 'Open')}</Button>
        </div>
        {result && <MysteryBoxResultView result={result} />}
      </div>
    </Collapsible>
  );
}

function HaggleCalculator({ rules }: { rules: NightMarketRules }) {
  const [state, setState] = useState<HaggleState>(() => initialHaggle());
  const [result, setResult] = useState<HaggleResult | null>(null);
  const [deal, setDeal] = useState<string | null>(null);
  return (
    <Collapsible title={t('Teste de Haggle', 'Haggle check')}>
      <HagglePanel rules={rules} state={state} onChange={setState} result={result} onResult={setResult} deal={deal} onDeal={setDeal} />
    </Collapsible>
  );
}
