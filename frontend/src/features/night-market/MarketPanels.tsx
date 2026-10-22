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

const dateTime = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

export function MarketInfo({ market }: { market: NightMarketDetail }) {
  const p = market.preset;
  return (
    <Collapsible title="O mercado" defaultOpen>
      <dl className={styles.facts}>
        <dt>Organizador</dt>
        <dd>
          {market.organizer.name}{market.organizer.role ? ` · ${market.organizer.role}` : ''}
          {market.organizer.operatorRank != null && <> · Operator {market.organizer.operatorRank}</>}
          {market.organizerReach && <> · Reach {PRICE_CATEGORY_LABEL[market.organizerReach]}</>}
        </dd>
        {p.frequency && <><dt>Quando</dt><dd>{p.frequency}</dd></>}
        {p.location && <><dt>Onde</dt><dd>{p.location}</dd></>}
        {p.sells && <><dt>Vende</dt><dd>{p.sells}</dd></>}
        {p.invite && <><dt>Convite</dt><dd>{p.invite}</dd></>}
        {p.rules && <><dt>Regras</dt><dd>{p.rules}</dd></>}
        {market.goods.length > 0 && (
          <>
            <dt>Mercadorias</dt>
            <dd>
              {market.goods.map((g) => (
                <div key={g.id}>
                  <b>{g.name}</b>{g.roll != null ? ` (d6 ${g.roll})` : ''}: {g.lines} linha(s)
                  <div className={styles.source}>d100 {g.rolls.map((r) => `${r.roll} ${r.label}`).join(' · ')}</div>
                </div>
              ))}
            </dd>
          </>
        )}
        <dt>Livros</dt><dd>{market.books.join(', ')}</dd>
        <dt>Seed</dt><dd className={styles.source}>{market.seed}</dd>
        {p.features.length > 0 && <><dt>Extras</dt><dd className={styles.tags}>{p.features.map((f) => <Badge key={f} tone="amber">{FEATURE_LABEL[f]}</Badge>)}</dd></>}
        <dt>Fonte</dt><dd className={styles.source}>{p.source}</dd>
      </dl>
    </Collapsible>
  );
}

export function SlotsPanel({ market, onFill }: { market: NightMarketDetail; onFill: (slot: OpenSlot) => void }) {
  if (market.openSlots.length === 0) return null;
  return (
    <Collapsible title="Escolhas do Mestre" active={market.openSlots.length} defaultOpen>
      <p className={styles.muted}>Linhas "à escolha do Mestre" e itens do Midnight Market ainda sem item.</p>
      {market.openSlots.map((slot) => (
        <div key={slot.id} className={styles.slot}>
          <span><b>{slot.label}</b> <span className={styles.source}>booth {slot.stall} · {ORIGIN_LABEL[slot.origin]}</span></span>
          <Button onClick={() => onFill(slot)}>Escolher item</Button>
        </div>
      ))}
    </Collapsible>
  );
}

function PrizeLine({ prize }: { prize: PrizeResult }) {
  return (
    <span>
      <strong>{prize.label}</strong>
      {prize.quantity > 1 && !/x\d|\d+ /i.test(prize.label) ? ` ×${prize.quantity}` : ''}
      {prize.loaded ? ' (carregada)' : ''}
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
                  {s.label.length > 18 ? `${s.label.slice(0, 17)}…` : s.label}
                </text>
              </g>
            );
          })}
          <circle r="10" fill="#050607" stroke="#ff2a3d" />
        </svg>
        <span className={styles.pointer} aria-hidden />
      </div>
      <div className={styles.row}>
        <input className={styles.text} value={winner} onChange={(e) => setWinner(e.target.value)} placeholder="Quem ganhou" aria-label="Ganhador" />
        <Button icon={<Gift size={15} />} onClick={spin} disabled={command.isPending}>Girar a roleta</Button>
      </div>
      {result && (
        <div className={styles.prize} role="status">
          Roleta {result.roll}/{result.of}: <PrizeLine prize={result.prize} />. Mais: {result.bonus.map((b) => b.label).join(' e ')}.
        </div>
      )}
      <p className={styles.source}>{dp.arrival.reference}. {dp.slices.reference}. Bônus: {dp.bonus.reference}.</p>
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
    <Collapsible title="Mystery Boxes do Mr. Amaze" defaultOpen>
      <p className={styles.muted}>{rules.rules.mysteryBoxes.redemption.value}</p>
      <div className={styles.row}>
        <input className={styles.text} value={recipient} onChange={(e) => setRecipient(e.target.value)} placeholder="Para quem" aria-label="Destinatário" />
        <Button icon={<Package size={15} />} onClick={open} disabled={command.isPending}>Abrir uma caixa</Button>
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
      <Collapsible title="Movimento" defaultOpen>
        <div className={styles.row}>
          <span>Visitantes: <b>{market.visitors}</b></span>
          <NumberInput className={styles.number} min={1} max={10000} value={arrivals} onChange={setArrivals} aria-label="Chegaram" />
          <Button icon={<UserPlus size={15} />} onClick={() => run('visitors', { count: arrivals ?? 1 })}>Chegaram</Button>
        </div>
        {features.includes('DoorPrize') && <p className={styles.source}>Prêmio a cada {rules.rules.doorPrize.arrival.value} visitantes ({rules.rules.doorPrize.arrival.reference}).</p>}
        {canMidnight && (
          <Button icon={<Moon size={15} />} onClick={() => run('midnight')}>
            Abrir um Midnight Market ({rules.rules.midnightMarket.items.value} itens)
          </Button>
        )}
        {!canMidnight && !hasMidnight && (
          <p className={styles.source}>Midnight Market: só com organizador de Operator {op.midnightMarketRank.value}+ ({op.midnightMarketRank.reference}).</p>
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
    <Collapsible title="Vendas e prêmios" active={market.sales.filter((s) => !s.undone).length}>
      <p className={styles.muted}>
        Recebido: <b className={styles.saleTotal}>{formatEb(revenue)}eb</b>{due > 0 && <> · a receber em 1 mês: <b className={styles.saleTotal}>{formatEb(due)}eb</b></>}.
        As compras ficam no mercado; nenhuma ficha é alterada.
      </p>
      <div className={styles.ledger}>
        {sales.length === 0 && <span className={styles.muted}>Nada vendido ainda.</span>}
        {sales.map((s) => (
          <div key={s.id} className={`${styles.sale} ${s.undone ? styles.saleUndone : ''}`}>
            <span>
              <b>{s.delivered}× {s.name}</b> · {s.buyer ?? 'Cliente'}
              {s.kind !== 'sale' && <> <Badge tone="amber">{s.kind === 'prize' ? 'Door Prize' : 'Mystery Box'}</Badge></>}
              {s.haggle && <div className={styles.source}>{s.haggle}</div>}
            </span>
            <span className={styles.saleTotal}>
              {s.kind === 'sale' ? `${formatEb(s.total)}eb` : '—'}
              {s.kind === 'sale' && !s.undone && (
                <Button variant="ghost" icon={<Undo2 size={13} />} aria-label="Desfazer venda" title="Desfazer venda"
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
    <Collapsible title="Histórico">
      <div className={styles.events}>
        {[...market.events].reverse().map((e, i) => (
          <div key={i} className={styles.event}>
            <span className={styles.source}>{dateTime.format(new Date(e.at))}</span> <b>{e.title}</b>
            {e.detail && <div className={styles.muted}>{e.detail}</div>}
          </div>
        ))}
      </div>
    </Collapsible>
  );
}

