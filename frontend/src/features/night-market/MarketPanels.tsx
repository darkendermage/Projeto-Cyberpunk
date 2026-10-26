import { useState } from 'react';
import { Gift, Moon, Package, Undo2, UserPlus } from 'lucide-react';
import { problemMessage } from '../../api/characters';
import { type DoorPrizeAnswer, type MysteryBoxAnswer, useMarketCommand } from '../../api/nightMarket';
import type { DoorPrizeResult, MysteryBoxResult, NightMarketDetail, NightMarketRules, OpenSlot, PrizeResult } from '../../api/nightMarketTypes';
import { Badge, Button, Collapsible, NumberInput } from '../../components/ui/ui';
import { formatEb, PRICE_CATEGORY_LABEL } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import { FEATURE_LABEL, ORIGIN_LABEL, wheelRotation, wheelSlices } from './nightMarket';
import styles from './NightMarket.module.css';
import { locale, t, tv } from '../../i18n';

const dateTime = new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short' });

export function MarketInfo({ market }: { market: NightMarketDetail }) {
  const p = market.preset;
  return (
    <Collapsible title={t('O mercado', 'The market')} defaultOpen>
      <dl className={styles.facts}>
        <dt>{t('Organizador', 'Organizer')}</dt>
        <dd>
          {tv(market.organizer.name)}{market.organizer.role ? ` · ${market.organizer.role}` : ''}
          {market.organizer.operatorRank != null && <> · Operator {market.organizer.operatorRank}</>}
          {market.organizerReach && <> · Reach {PRICE_CATEGORY_LABEL[market.organizerReach]}</>}
        </dd>
        {p.frequency && <><dt>{t('Quando', 'When')}</dt><dd>{p.frequency}</dd></>}
        {p.location && <><dt>{t('Onde', 'Where')}</dt><dd>{tv(p.location)}</dd></>}
        {p.sells && <><dt>{t('Vende', 'Sells')}</dt><dd>{tv(p.sells)}</dd></>}
        {p.invite && <><dt>{t('Convite', 'Invitation')}</dt><dd>{p.invite}</dd></>}
        {p.rules && <><dt>{t('Regras', 'Rules')}</dt><dd>{p.rules}</dd></>}
        {market.goods.length > 0 && (
          <>
            <dt>{t('Mercadorias', 'Goods')}</dt>
            <dd>
              {market.goods.map((g) => (
                <div key={g.id}>
                  <b>{tv(g.name)}</b>{g.roll != null ? ` (d6 ${g.roll})` : ''}: {g.lines} {t('linha(s)', 'line(s)')}
                  <div className={styles.source}>d100 {g.rolls.map((r) => `${r.roll} ${tv(r.label)}`).join(' · ')}</div>
                </div>
              ))}
            </dd>
          </>
        )}
        <dt>{t('Livros', 'Books')}</dt><dd>{market.books.join(', ')}</dd>
        <dt>Seed</dt><dd className={styles.source}>{market.seed}</dd>
        {p.features.length > 0 && <><dt>{t('Extras', 'Extras')}</dt><dd className={styles.tags}>{p.features.map((f) => <Badge key={f} tone="amber">{FEATURE_LABEL[f]}</Badge>)}</dd></>}
        <dt>{t('Fonte', 'Source')}</dt><dd className={styles.source}>{p.source}</dd>
      </dl>
    </Collapsible>
  );
}

export function SlotsPanel({ market, onFill }: { market: NightMarketDetail; onFill: (slot: OpenSlot) => void }) {
  if (market.openSlots.length === 0) return null;
  return (
    <Collapsible title={t('Escolhas do Mestre', 'GM\'s choices')} active={market.openSlots.length} defaultOpen>
      <p className={styles.muted}>{t('Linhas "à escolha do Mestre" e itens do Midnight Market ainda sem item.', '"GM\'s choice" lines and Midnight Market items with no item yet.')}</p>
      {market.openSlots.map((slot) => (
        <div key={slot.id} className={styles.slot}>
          <span><b>{tv(slot.label)}</b> <span className={styles.source}>booth {slot.stall} · {ORIGIN_LABEL[slot.origin]}</span></span>
          <Button onClick={() => onFill(slot)}>{t('Escolher item', 'Pick item')}</Button>
        </div>
      ))}
    </Collapsible>
  );
}

function PrizeLine({ prize }: { prize: PrizeResult }) {
  return (
    <span>
      <strong>{tv(prize.label)}</strong>
      {prize.quantity > 1 && !/x\d|\d+ /i.test(prize.label) ? ` ×${prize.quantity}` : ''}
      {prize.loaded ? t(' (carregada)', ' (loaded)') : ''}
    </span>
  );
}

function DoorPrize({ market, rules }: { market: NightMarketDetail; rules: NightMarketRules }) {
  const toast = useToast();
  const command = useMarketCommand(market.id);
  const dp = rules.rules.doorPrize;
  const wheel = [dp.example.value, ...dp.slices.value];
  const slices = wheelSlices(wheel);
  const [rotation, setRotation] = useState(0);
  const [result, setResult] = useState<DoorPrizeResult | null>(null);
  const [winner, setWinner] = useState('');
  const colors = ['#3a070e', '#13161b', '#7a0f1c', '#1a1e25'];

  const spin = () => command.mutate({ path: 'door-prize', body: { winner: winner || null } }, {
    onSuccess: (answer) => {
      const r = (answer as DoorPrizeAnswer).result;
      setResult(null);
      setRotation((current) => current - (current % 360) + wheelRotation(wheel, r.roll));
      window.setTimeout(() => setResult(r), document.documentElement.dataset.motion === 'off' ? 0 : 3200);
    },
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });

  return (
    <Collapsible title="Molly's Door Prize" defaultOpen>
      <div className={styles.wheelWrap}>
        <svg className={styles.wheel} viewBox="-100 -100 200 200" style={{ transform: `rotate(${rotation}deg)` }} aria-hidden>
          {slices.map((s, i) => {
            const a0 = ((s.start - 90) * Math.PI) / 180;
            const a1 = ((s.end - 90) * Math.PI) / 180;
            const large = s.end - s.start > 180 ? 1 : 0;
            const mid = (((s.start + s.end) / 2 - 90) * Math.PI) / 180;
            return (
              <g key={i}>
                <path d={`M0 0 L${96 * Math.cos(a0)} ${96 * Math.sin(a0)} A96 96 0 ${large} 1 ${96 * Math.cos(a1)} ${96 * Math.sin(a1)} Z`}
                  fill={colors[i % colors.length]} stroke="#ff2a3d" strokeWidth="0.8" />
                <text x={58 * Math.cos(mid)} y={58 * Math.sin(mid)} fill="#e9edf2" fontSize="7" textAnchor="middle" dominantBaseline="middle"
                  transform={`rotate(${(s.start + s.end) / 2} ${58 * Math.cos(mid)} ${58 * Math.sin(mid)})`}>
                  {s.label.length > 18 ? `${tv(s.label).slice(0, 17)}…` : tv(s.label)}
                </text>
              </g>
            );
          })}
          <circle r="10" fill="#050607" stroke="#ff2a3d" />
        </svg>
        <span className={styles.pointer} aria-hidden />
      </div>
      <div className={styles.row}>
        <input className={styles.text} value={winner} onChange={(e) => setWinner(e.target.value)} placeholder={t('Quem ganhou', 'Who won')} aria-label={t('Ganhador', 'Winner')} />
        <Button icon={<Gift size={15} />} onClick={spin} disabled={command.isPending}>{t('Girar a roleta', 'Spin the wheel')}</Button>
      </div>
      {result && (
        <div className={styles.prize} role="status">
          {t('Roleta ', 'Wheel ')}{result.roll}/{result.of}: <PrizeLine prize={result.prize} />{t('. Mais: ', '. More: ')}{result.bonus.map((b) => tv(b.label)).join(' e ')}.
        </div>
      )}
      <p className={styles.source}>{dp.arrival.reference}. {dp.slices.reference}{t('. Bônus: ', '. Bonus: ')}{dp.bonus.reference}.</p>
    </Collapsible>
  );
}

export function MysteryBoxResultView({ result }: { result: MysteryBoxResult }) {
  return (
    <div className={styles.prize} role="status">
      <div className={styles.muted}>1d10 {result.boxRoll}: {result.box}</div>
      1d6 {result.contentRoll}: <PrizeLine prize={result.prize} />
      {result.prize.extraItemName && <> + {result.prize.extraItemName}</>}
      <div className={styles.source}>{result.source}</div>
    </div>
  );
}

function MysteryBox({ market, rules }: { market: NightMarketDetail; rules: NightMarketRules }) {
  const toast = useToast();
  const command = useMarketCommand(market.id);
  const [recipient, setRecipient] = useState('');
  const [result, setResult] = useState<MysteryBoxResult | null>(null);
  const open = () => command.mutate({ path: 'mystery-box', body: { recipient: recipient || null } }, {
    onSuccess: (answer) => setResult((answer as MysteryBoxAnswer).result),
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });
  return (
    <Collapsible title={t('Mystery Boxes do Mr. Amaze', 'Mr. Amaze\'s Mystery Boxes')} defaultOpen>
      <p className={styles.muted}>{tv(rules.rules.mysteryBoxes.redemption.value)}</p>
      <div className={styles.row}>
        <input className={styles.text} value={recipient} onChange={(e) => setRecipient(e.target.value)} placeholder={t('Para quem', 'For whom')} aria-label={t('Destinatário', 'Recipient')} />
        <Button icon={<Package size={15} />} onClick={open} disabled={command.isPending}>{t('Abrir uma caixa', 'Open a box')}</Button>
      </div>
      {result && <MysteryBoxResultView result={result} />}
      <p className={styles.source}>{rules.rules.mysteryBoxes.redemption.reference}</p>
    </Collapsible>
  );
}

export function EventsPanel({ market, rules }: { market: NightMarketDetail; rules: NightMarketRules }) {
  const toast = useToast();
  const command = useMarketCommand(market.id);
  const [arrivals, setArrivals] = useState<number | null>(1);
  const features = market.preset.features;
  const op = rules.rules.operator;
  const hasMidnight = market.openSlots.some((s) => s.origin === 'Midnight') || market.listings.some((l) => l.origin === 'Midnight');
  const canMidnight = !hasMidnight && (market.organizer.operatorRank ?? 0) >= op.midnightMarketRank.value;
  const run = (path: string, body: Record<string, unknown> = {}) => command.mutate({ path, body }, {
    onSuccess: (answer) => ('market' in answer ? answer.market : answer).notices.forEach((n) => toast.notify(n, 'info')),
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });

  return (
    <>
      {features.includes('DoorPrize') && <DoorPrize market={market} rules={rules} />}
      {features.includes('MysteryBoxes') && <MysteryBox market={market} rules={rules} />}
      <Collapsible title={t('Movimento', 'Traffic')} defaultOpen>
        <div className={styles.row}>
          <span>{t('Visitantes: ', 'Visitors: ')}<b>{market.visitors}</b></span>
          <NumberInput className={styles.number} min={1} max={10000} value={arrivals} onChange={setArrivals} aria-label={t('Chegaram', 'Arrived')} />
          <Button icon={<UserPlus size={15} />} onClick={() => run('visitors', { count: arrivals ?? 1 })}>{t('Chegaram', 'Arrived')}</Button>
        </div>
        {features.includes('DoorPrize') && <p className={styles.source}>{t('Prêmio a cada ', 'Prize every ')}{tv(rules.rules.doorPrize.arrival.value)} {t('visitantes (', 'visitors (')}{rules.rules.doorPrize.arrival.reference}).</p>}
        {canMidnight && (
          <Button icon={<Moon size={15} />} onClick={() => run('midnight')}>
            {t('Abrir um Midnight Market (', 'Open a Midnight Market (')}{tv(rules.rules.midnightMarket.items.value)} {t('itens)', 'items)')}
          </Button>
        )}
        {!canMidnight && !hasMidnight && (
          <p className={styles.source}>{t('Midnight Market: só com organizador de Operator ', 'Midnight Market: only with an organizer of Operator ')}{tv(op.midnightMarketRank.value)}+ ({op.midnightMarketRank.reference}).</p>
        )}
      </Collapsible>
    </>
  );
}

export function LedgerPanel({ market }: { market: NightMarketDetail }) {
  const toast = useToast();
  const command = useMarketCommand(market.id);
  const sales = [...market.sales].reverse();
  const revenue = market.sales.filter((s) => s.kind === 'sale' && !s.undone).reduce((sum, s) => sum + s.paidNow, 0);
  const due = market.sales.filter((s) => s.kind === 'sale' && !s.undone).reduce((sum, s) => sum + s.dueLater, 0);
  return (
    <Collapsible title={t('Vendas e prêmios', 'Sales and prizes')} active={market.sales.filter((s) => !s.undone).length}>
      <p className={styles.muted}>
        {t('Recebido: ', 'Received: ')}<b className={styles.saleTotal}>{formatEb(revenue)}eb</b>{due > 0 && <> {t('· a receber em 1 mês: ', '· due in 1 month: ')}<b className={styles.saleTotal}>{formatEb(due)}eb</b></>}{t('. As compras ficam no mercado; nenhuma ficha é alterada.', '. Purchases stay in the market; no sheet is changed.')}
      </p>
      <div className={styles.ledger}>
        {sales.length === 0 && <span className={styles.muted}>{t('Nada vendido ainda.', 'Nothing sold yet.')}</span>}
        {sales.map((s) => (
          <div key={s.id} className={`${styles.sale} ${s.undone ? styles.saleUndone : ''}`}>
            <span>
              <b>{s.delivered}× {tv(s.name)}</b> · {s.buyer ?? t('Cliente', 'Customer')}
              {s.kind !== 'sale' && <> <Badge tone="amber">{s.kind === 'prize' ? 'Door Prize' : 'Mystery Box'}</Badge></>}
              {s.haggle && <div className={styles.source}>{s.haggle}</div>}
            </span>
            <span className={styles.saleTotal}>
              {s.kind === 'sale' ? `${formatEb(s.total)}eb` : '—'}
              {s.kind === 'sale' && !s.undone && (
                <Button variant="ghost" icon={<Undo2 size={13} />} aria-label={t('Desfazer venda', 'Undo sale')} title={t('Desfazer venda', 'Undo sale')}
                  onClick={() => command.mutate({ path: `sales/${s.id}/undo` }, { onError: (e) => toast.notify(problemMessage(e), 'error') })} />
              )}
            </span>
          </div>
        ))}
      </div>
    </Collapsible>
  );
}

export function LogPanel({ market }: { market: NightMarketDetail }) {
  return (
    <Collapsible title={t('Histórico', 'History')}>
      <div className={styles.events}>
        {[...market.events].reverse().map((e, i) => (
          <div key={i} className={styles.event}>
            <span className={styles.source}>{dateTime.format(new Date(e.at))}</span> <b>{tv(e.title)}</b>
            {e.detail && <div className={styles.muted}>{tv(e.detail)}</div>}
          </div>
        ))}
      </div>
    </Collapsible>
  );
}

