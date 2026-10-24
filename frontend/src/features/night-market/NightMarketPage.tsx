import { useMemo, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { Dices, Store } from 'lucide-react';
import clsx from 'clsx';
import { problemMessage } from '../../api/characters';
import { marketApi, useMarketRules, useMarkets, useStoreMarket } from '../../api/nightMarket';
import type { GenerateMarketRequest, MarketPreset, NightMarketRules } from '../../api/nightMarketTypes';
import { Badge, Button, Checkbox, EmptyState, ErrorState, Field, NumberInput, Segmented, Select, Spinner, Switch } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import page from '../../styles/Page.module.css';
import { IndexView } from './IndexView';
import { MarketView } from './MarketView';
import { FEATURE_LABEL, KIND_LABEL } from './nightMarket';
import { SourcesView } from './SourcesView';
import { ToolsView } from './ToolsView';
import styles from './NightMarket.module.css';

type Tab = 'markets' | 'index' | 'tools' | 'sources';

export function NightMarketPage() {
  const rules = useMarketRules();
  const { id } = useParams();
  if (rules.isLoading) return <div className={page.page}><Spinner /></div>;
  if (rules.error || !rules.data) return <div className={page.page}><ErrorState error={rules.error} onRetry={() => rules.refetch()} /></div>;
  return id ? <MarketView id={Number(id)} rules={rules.data} /> : <Home rules={rules.data} />;
}

function Home({ rules }: { rules: NightMarketRules }) {
  const [params, setParams] = useSearchParams();
  const tab = (params.get('tab') as Tab | null) ?? 'markets';
  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1>Night Market</h1>
        <p className={page.lead}>
          Monte a feira da noite pelas tabelas do Core (p.338–339), visite os seis mercados do Black Chrome ou as bancas dos Interface RED,
          compre pelo CBK com Haggle e acompanhe Door Prizes e Mystery Boxes. Itens Damaged e Destroyed seguem o Interface RED 5.
        </p>
      </header>
      <Segmented ariaLabel="Seção" value={tab} onChange={(t) => setParams(t === 'markets' ? {} : { tab: t })} options={[
        { value: 'markets', label: 'Mercados' },
        { value: 'index', label: 'Night Market Index' },
        { value: 'tools', label: 'Ferramentas do Fixer' },
        { value: 'sources', label: 'Fontes e parâmetros' },
      ]} />
      {tab === 'markets' && <Markets rules={rules} />}
      {tab === 'index' && <IndexView rules={rules} />}
      {tab === 'tools' && <ToolsView rules={rules} />}
      {tab === 'sources' && <SourcesView rules={rules} />}
    </div>
  );
}

function Markets({ rules }: { rules: NightMarketRules }) {
  const markets = useMarkets();
  return (
    <>
      <section className={page.section}>
        <h2>Mercados salvos</h2>
        {markets.isLoading ? <Spinner /> : (markets.data ?? []).length === 0 ? (
          <EmptyState title="Nenhum mercado aberto" icon={<Store size={30} />}>Gere o primeiro abaixo.</EmptyState>
        ) : (
          <div className={styles.library}>
            {markets.data!.map((m) => (
              <Link key={m.id} to={`/night-market/${m.id}`} className={styles.marketCard}>
                <h3>{m.name}</h3>
                <span className={styles.muted}>{m.presetName} · {m.organizer}{m.operatorRank != null ? ` (Operator ${m.operatorRank})` : ''}</span>
                <span className={styles.row}>
                  <Badge tone={m.status === 'Open' ? 'cyan' : 'default'}>{m.status === 'Open' ? 'Aberto' : 'Encerrado'}</Badge>
                  <span className={styles.source}>{m.listings} listagens · {m.sales} vendas · {formatEb(m.revenue)}eb</span>
                </span>
              </Link>
            ))}
          </div>
        )}
      </section>
      <CreateMarket rules={rules} />
    </>
  );
}

const GROUPS: { title: string; ids: (p: MarketPreset) => boolean }[] = [
  { title: 'Core Rulebook', ids: (p) => p.id === 'core' || p.id === 'custom' },
  { title: 'Black Chrome (BC p.131–156)', ids: (p) => p.id.startsWith('bc-') },
  { title: 'Interface RED e DLCs', ids: (p) => !p.id.startsWith('bc-') && p.id !== 'core' && p.id !== 'custom' },
];

type GoodsChoice = 'preset' | 'roll' | 'pick';

function CreateMarket({ rules }: { rules: NightMarketRules }) {
  const toast = useToast();
  const navigate = useNavigate();
  const store = useStoreMarket();
  const [presetId, setPresetId] = useState('core');
  const preset = rules.presets.find((p) => p.id === presetId)!;
  const [name, setName] = useState('');
  const [organizer, setOrganizer] = useState(preset.organizer.name);
  const [rank, setRank] = useState<number | null>(preset.organizer.operatorRank ?? null);
  const [trading, setTrading] = useState<number | null>(preset.organizer.trading ?? null);
  const [choice, setChoice] = useState<GoodsChoice>('preset');
  const [goods, setGoods] = useState<string[]>([]);
  const [lines, setLines] = useState<Record<string, number | null>>({});
  const [seed, setSeed] = useState<number | null>(null);
  const [books, setBooks] = useState<string[] | null>(null);
  const [midnight, setMidnight] = useState(false);
  const [damaged, setDamaged] = useState<number | null>(0);
  const [scam, setScam] = useState(false);
  const [related, setRelated] = useState(false);
  const [bcLetter, setBcLetter] = useState('');
  const goodsTypes = rules.rules.generator.goods.value;
  const op = rules.rules.operator;
  const defaultBooks = useMemo(() => (presetId === 'core' ? ['Core Book'] : rules.books.map((b) => b.name)), [presetId, rules.books]);
  const chosenBooks = books ?? defaultBooks;

  const pick = (p: MarketPreset) => {
    setPresetId(p.id);
    setOrganizer(p.organizer.name);
    setRank(p.organizer.operatorRank ?? null);
    setTrading(p.organizer.trading ?? null);
    setChoice(p.goods === 'Pick' ? 'pick' : 'preset');
    setGoods(p.goods === 'Pick' ? [] : p.suggestedGoods);
    setBooks(null);
    setMidnight(false);
    setDamaged(p.features.includes('SecondHand') ? 25 : 0);
  };

  const body = (): GenerateMarketRequest => {
    const pickedGoods = choice === 'pick' ? goods : choice === 'preset' && preset.goods === 'Suggested' ? preset.suggestedGoods : null;
    const typed = Object.fromEntries(Object.entries(lines).filter(([, v]) => v != null)) as Record<string, number>;
    return {
      name: name.trim() || undefined, presetId, seed, books: chosenBooks,
      goods: choice === 'roll' ? null : pickedGoods, rollGoods: choice === 'roll',
      lines: Object.keys(typed).length ? typed : null,
      organizer: { name: organizer, operatorRank: rank, trading },
      options: { damagedPercent: damaged ?? 0, scam, includeRelated: related, blackChromeLetter: bcLetter || null },
      midnight,
    };
  };

  const create = useMutation({
    mutationFn: () => marketApi.create(body()),
    onSuccess: (market) => {
      store(market);
      toast.notify(`${market.name} montado (seed ${market.seed}).`);
      navigate(`/night-market/${market.id}`);
    },
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });

  const showGoods = choice === 'pick' || (choice === 'preset' && preset.goods === 'Suggested');
  const goodsForLines = choice === 'pick' ? goods : choice === 'preset' && preset.goods === 'Suggested' ? preset.suggestedGoods : [];

  return (
    <section className={page.section}>
      <h2>Novo mercado</h2>
      {GROUPS.map((g) => (
        <div key={g.title} className={styles.presetGroup}>
          <p className={styles.label}>{g.title}</p>
          <div className={styles.presets}>
            {rules.presets.filter(g.ids).map((p) => (
              <button key={p.id} type="button" className={clsx(styles.preset, p.id === presetId && styles.presetActive)} onClick={() => pick(p)}>
                <span className={styles.presetLetter}>{p.letter}</span>
                <span className={styles.presetName}>{p.name}</span>
                <span className={styles.presetMeta}>
                  {KIND_LABEL[p.kind]} · {p.organizer.name}{p.organizer.operatorRank != null ? ` (Op ${p.organizer.operatorRank})` : ''}
                </span>
                <span className={styles.source}>{p.source}</span>
              </button>
            ))}
          </div>
        </div>
      ))}

      <div className={styles.details} style={{ marginTop: 16 }}>
        <div className={styles.dialog}>
          <div className={styles.grid3}>
            <Field label="Nome (opcional)"><input className={styles.text} value={name} onChange={(e) => setName(e.target.value)} placeholder={`${preset.name} — hoje`} /></Field>
            <Field label="Organizador"><input className={styles.text} value={organizer} onChange={(e) => setOrganizer(e.target.value)} /></Field>
            <div className={styles.row}>
              <Field label="Operator Rank" hint={preset.kind === 'NightMarket' ? `Night Market: ${op.nightMarketRank.value}+ (${op.nightMarketRank.reference})` : undefined}>
                <NumberInput className={styles.number} min={0} max={10} allowEmpty value={rank} onChange={setRank} />
              </Field>
              <Field label="COOL + Trading">
                <NumberInput className={styles.number} min={0} max={40} allowEmpty value={trading} onChange={setTrading} />
              </Field>
            </div>
          </div>

          <Field label="Tipos de mercadoria" hint={`${rules.rules.generator.goodsRolls.reference}: 1d6 duas vezes, sem repetir; ${rules.rules.generator.linesPerType.value} linhas por tipo.`}>
            <Segmented ariaLabel="Mercadorias" value={choice} onChange={setChoice} options={[
              { value: 'preset', label: preset.goods === 'Roll' ? 'Rolar (Core)' : preset.goods === 'Suggested' ? 'Sugeridas pelo livro' : preset.goods === 'None' ? 'Só o estoque' : 'Escolher' },
              ...(preset.goods !== 'Roll' ? [{ value: 'roll' as const, label: 'Rolar 1d6 ×2' }] : []),
              { value: 'pick', label: 'Escolher' },
            ]} />
          </Field>
          {choice === 'pick' && (
            <div className={styles.row}>
              {goodsTypes.map((g) => (
                <Checkbox key={g.id} checked={goods.includes(g.id)} label={`${g.roll}. ${g.name}`}
                  onChange={(v) => setGoods(v ? [...goods, g.id] : goods.filter((x) => x !== g.id))} />
              ))}
            </div>
          )}
          {showGoods && goodsForLines.length > 0 && (
            <div className={styles.row}>
              {goodsForLines.map((id) => {
                const g = goodsTypes.find((x) => x.id === id)!;
                return (
                  <Field key={id} label={`Linhas: ${g.name}`} hint="Vazio: 1d10.">
                    <NumberInput className={styles.number} min={1} max={10} allowEmpty value={lines[id] ?? null}
                      onChange={(v) => setLines({ ...lines, [id]: v })} />
                  </Field>
                );
              })}
            </div>
          )}

          <Field label={`Livros do acervo (${chosenBooks.length})`} hint={presetId === 'core' ? 'O mercado do Core usa só o Core Rulebook; ligue outros livros para variar as linhas "de X eb".' : 'Vale para as linhas que descrevem um tipo de item.'}>
            <div className={styles.row}>
              {rules.books.map((b) => (
                <Checkbox key={b.name} checked={chosenBooks.includes(b.name)} count={b.items} label={b.name}
                  onChange={(v) => setBooks(v ? [...chosenBooks, b.name] : chosenBooks.filter((x) => x !== b.name))} />
              ))}
            </div>
          </Field>

          <div className={styles.grid3}>
            <Field label="Seed (opcional)" hint="A mesma seed monta o mesmo mercado.">
              <NumberInput className={styles.number} min={1} max={2147483646} allowEmpty value={seed} onChange={setSeed} />
            </Field>
            <Field label="Itens Damaged (%)" hint={`Segunda mão: chance por listagem (regra complementar). Preço: ${rules.rules.condition.damagedSteps.reference}.`}>
              <NumberInput className={styles.number} min={0} max={100} value={damaged} onChange={setDamaged} />
            </Field>
            {!preset.id.startsWith('bc-') && (
              <Field label="Itens do Black Chrome" hint={rules.rules.blackChrome.booths.reference}>
                <Select value={bcLetter} onChange={(e) => setBcLetter(e.target.value)}>
                  <option value="">Nenhum</option>
                  {rules.presets.filter((p) => p.id.startsWith('bc-')).map((p) => <option key={p.letter} value={p.letter}>{p.letter} · {p.name}</option>)}
                </Select>
              </Field>
            )}
          </div>
          <div className={styles.row}>
            {(damaged ?? 0) > 0 && <Switch checked={scam} onChange={setScam} label="Golpe: vender os Damaged como novos" />}
            <Switch checked={related} onChange={setRelated} label="Itens de outros livros por tipo (regra complementar)" />
            {preset.kind !== 'MidnightMarket' && (
              <Switch checked={midnight} onChange={setMidnight} label={`Midnight Market (Operator ${op.midnightMarketRank.value}+)`} />
            )}
          </div>
          <div className={styles.row}>
            <Button variant="primary" icon={<Dices size={15} />} onClick={() => create.mutate()}
              disabled={create.isPending || (choice === 'pick' && goods.length === 0 && preset.goods === 'Pick')}>
              Gerar mercado
            </Button>
          </div>
        </div>

        <aside className={styles.dialog}>
          <dl className={styles.facts}>
            <dt>Tipo</dt><dd>{KIND_LABEL[preset.kind]} · letra {preset.letter}</dd>
            <dt>Quem</dt><dd>{preset.organizer.name} · {preset.organizer.role}</dd>
            {preset.summary && <><dt>Resumo</dt><dd>{preset.summary}</dd></>}
            {preset.frequency && <><dt>Quando</dt><dd>{preset.frequency}</dd></>}
            {preset.location && <><dt>Onde</dt><dd>{preset.location}</dd></>}
            {preset.sells && <><dt>Vende</dt><dd>{preset.sells}</dd></>}
            {preset.invite && <><dt>Convite</dt><dd>{preset.invite}</dd></>}
            {preset.admission && <><dt>Entrada</dt><dd>{formatEb(preset.admission.price)}eb · {preset.admission.label}</dd></>}
            {preset.features.length > 0 && <><dt>Extras</dt><dd className={styles.tags}>{preset.features.map((f) => <Badge key={f} tone="amber">{FEATURE_LABEL[f]}</Badge>)}</dd></>}
            <dt>Fonte</dt><dd className={styles.source}>{preset.source}</dd>
          </dl>
        </aside>
      </div>
    </section>
  );
}
