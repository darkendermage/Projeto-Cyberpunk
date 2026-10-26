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
import { t, tv } from '../../i18n';

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
          {t('Monte a feira da noite pelas tabelas do Core (p.338–339), visite os seis mercados do Black Chrome ou as bancas dos Interface RED, compre pelo CBK com Haggle e acompanhe Door Prizes e Mystery Boxes. Itens Damaged e Destroyed seguem o Interface RED 5.', 'Build tonight\'s market with the Core tables (p.338–339), visit the six Black Chrome markets or the Interface RED stalls, buy through the CBK with Haggle and track Door Prizes and Mystery Boxes. Damaged and Destroyed items follow Interface RED 5.')}
        </p>
      </header>
      <Segmented ariaLabel={t('Seção', 'Section')} value={tab} onChange={(entry) => setParams(entry === 'markets' ? {} : { tab: entry })} options={[
        { value: 'markets', label: t('Mercados', 'Markets') },
        { value: 'index', label: 'Night Market Index' },
        { value: 'tools', label: t('Ferramentas do Fixer', 'Fixer tools') },
        { value: 'sources', label: t('Fontes e parâmetros', 'Sources and parameters') },
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
        <h2>{t('Mercados salvos', 'Saved markets')}</h2>
        {markets.isLoading ? <Spinner /> : (markets.data ?? []).length === 0 ? (
          <EmptyState title={t('Nenhum mercado aberto', 'No markets open')} icon={<Store size={30} />}>{t('Gere o primeiro abaixo.', 'Generate the first one below.')}</EmptyState>
        ) : (
          <div className={styles.library}>
            {markets.data!.map((m) => (
              <Link key={m.id} to={`/night-market/${m.id}`} className={styles.marketCard}>
                <h3>{tv(m.name)}</h3>
                <span className={styles.muted}>{m.presetName} · {m.organizer}{m.operatorRank != null ? ` (Operator ${m.operatorRank})` : ''}</span>
                <span className={styles.row}>
                  <Badge tone={m.status === 'Open' ? 'cyan' : 'default'}>{m.status === 'Open' ? t('Aberto', 'Open') : t('Encerrado', 'Closed')}</Badge>
                  <span className={styles.source}>{m.listings} {t('listagens · ', 'listings · ')}{m.sales} {t('vendas · ', 'sales · ')}{formatEb(m.revenue)}eb</span>
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
  { title: t('Interface RED e DLCs', 'Interface RED and DLCs'), ids: (p) => !p.id.startsWith('bc-') && p.id !== 'core' && p.id !== 'custom' },
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
      toast.notify(t(`${market.name} montado (seed ${market.seed}).`, `${market.name} set up (seed ${market.seed}).`));
      navigate(`/night-market/${market.id}`);
    },
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });

  const showGoods = choice === 'pick' || (choice === 'preset' && preset.goods === 'Suggested');
  const goodsForLines = choice === 'pick' ? goods : choice === 'preset' && preset.goods === 'Suggested' ? preset.suggestedGoods : [];

  return (
    <section className={page.section}>
      <h2>{t('Novo mercado', 'New market')}</h2>
      {GROUPS.map((g) => (
        <div key={g.title} className={styles.presetGroup}>
          <p className={styles.label}>{tv(g.title)}</p>
          <div className={styles.presets}>
            {rules.presets.filter(g.ids).map((p) => (
              <button key={p.id} type="button" className={clsx(styles.preset, p.id === presetId && styles.presetActive)} onClick={() => pick(p)}>
                <span className={styles.presetLetter}>{p.letter}</span>
                <span className={styles.presetName}>{tv(p.name)}</span>
                <span className={styles.presetMeta}>
                  {KIND_LABEL[p.kind]} · {tv(p.organizer.name)}{p.organizer.operatorRank != null ? ` (Op ${p.organizer.operatorRank})` : ''}
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
            <Field label={t('Nome (opcional)', 'Name (optional)')}><input className={styles.text} value={name} onChange={(e) => setName(e.target.value)} placeholder={t(`${tv(preset.name)} — hoje`, `${tv(preset.name)} — tonight`)} /></Field>
            <Field label={t('Organizador', 'Organizer')}><input className={styles.text} value={organizer} onChange={(e) => setOrganizer(e.target.value)} /></Field>
            <div className={styles.row}>
              <Field label="Operator Rank" hint={preset.kind === 'NightMarket' ? `Night Market: ${tv(op.nightMarketRank.value)}+ (${op.nightMarketRank.reference})` : undefined}>
                <NumberInput className={styles.number} min={0} max={10} allowEmpty value={rank} onChange={setRank} />
              </Field>
              <Field label="COOL + Trading">
                <NumberInput className={styles.number} min={0} max={40} allowEmpty value={trading} onChange={setTrading} />
              </Field>
            </div>
          </div>

          <Field label={t('Tipos de mercadoria', 'Types of goods')} hint={t(`${rules.rules.generator.goodsRolls.reference}: 1d6 duas vezes, sem repetir; ${tv(rules.rules.generator.linesPerType.value)} linhas por tipo.`, `${rules.rules.generator.goodsRolls.reference}: 1d6 twice, no repeats; ${tv(rules.rules.generator.linesPerType.value)} lines per type.`)}>
            <Segmented ariaLabel={t('Mercadorias', 'Goods')} value={choice} onChange={setChoice} options={[
              { value: 'preset', label: preset.goods === 'Roll' ? t('Rolar (Core)', 'Roll (Core)') : preset.goods === 'Suggested' ? t('Sugeridas pelo livro', 'Suggested by the book') : preset.goods === 'None' ? t('Só o estoque', 'Stock only') : t('Escolher', 'Choose') },
              ...(preset.goods !== 'Roll' ? [{ value: 'roll' as const, label: t('Rolar 1d6 ×2', 'Roll 1d6 ×2') }] : []),
              { value: 'pick', label: t('Escolher', 'Choose') },
            ]} />
          </Field>
          {choice === 'pick' && (
            <div className={styles.row}>
              {goodsTypes.map((g) => (
                <Checkbox key={g.id} checked={goods.includes(g.id)} label={`${g.roll}. ${tv(g.name)}`}
                  onChange={(v) => setGoods(v ? [...goods, g.id] : goods.filter((x) => x !== g.id))} />
              ))}
            </div>
          )}
          {showGoods && goodsForLines.length > 0 && (
            <div className={styles.row}>
              {goodsForLines.map((id) => {
                const g = goodsTypes.find((x) => x.id === id)!;
                return (
                  <Field key={id} label={t(`Linhas: ${tv(g.name)}`, `Lines: ${tv(g.name)}`)} hint={t('Vazio: 1d10.', 'Empty: 1d10.')}>
                    <NumberInput className={styles.number} min={1} max={10} allowEmpty value={lines[id] ?? null}
                      onChange={(v) => setLines({ ...lines, [id]: v })} />
                  </Field>
                );
              })}
            </div>
          )}

          <Field label={t(`Livros do acervo (${chosenBooks.length})`, `Books in the collection (${chosenBooks.length})`)} hint={presetId === 'core' ? t('O mercado do Core usa só o Core Rulebook; ligue outros livros para variar as linhas "de X eb".', 'The Core market uses only the Core Rulebook; turn on other books to vary the "X eb" lines.') : t('Vale para as linhas que descrevem um tipo de item.', 'Applies to lines that describe a type of item.')}>
            <div className={styles.row}>
              {rules.books.map((b) => (
                <Checkbox key={b.name} checked={chosenBooks.includes(b.name)} count={b.items} label={tv(b.name)}
                  onChange={(v) => setBooks(v ? [...chosenBooks, b.name] : chosenBooks.filter((x) => x !== b.name))} />
              ))}
            </div>
          </Field>

          <div className={styles.grid3}>
            <Field label={t('Seed (opcional)', 'Seed (optional)')} hint={t('A mesma seed monta o mesmo mercado.', 'The same seed builds the same market.')}>
              <NumberInput className={styles.number} min={1} max={2147483646} allowEmpty value={seed} onChange={setSeed} />
            </Field>
            <Field label={t('Itens Damaged (%)', 'Damaged items (%)')} hint={t(`Segunda mão: chance por listagem (regra complementar). Preço: ${rules.rules.condition.damagedSteps.reference}.`, `Second hand: chance per listing (complementary rule). Price: ${rules.rules.condition.damagedSteps.reference}.`)}>
              <NumberInput className={styles.number} min={0} max={100} value={damaged} onChange={setDamaged} />
            </Field>
            {!preset.id.startsWith('bc-') && (
              <Field label={t('Itens do Black Chrome', 'Black Chrome items')} hint={rules.rules.blackChrome.booths.reference}>
                <Select value={bcLetter} onChange={(e) => setBcLetter(e.target.value)}>
                  <option value="">{t('Nenhum', 'None')}</option>
                  {rules.presets.filter((p) => p.id.startsWith('bc-')).map((p) => <option key={p.letter} value={p.letter}>{p.letter} · {tv(p.name)}</option>)}
                </Select>
              </Field>
            )}
          </div>
          <div className={styles.row}>
            {(damaged ?? 0) > 0 && <Switch checked={scam} onChange={setScam} label={t('Golpe: vender os Damaged como novos', 'Scam: sell the Damaged ones as new')} />}
            <Switch checked={related} onChange={setRelated} label={t('Itens de outros livros por tipo (regra complementar)', 'Items from other books by type (complementary rule)')} />
            {preset.kind !== 'MidnightMarket' && (
              <Switch checked={midnight} onChange={setMidnight} label={`Midnight Market (Operator ${tv(op.midnightMarketRank.value)}+)`} />
            )}
          </div>
          <div className={styles.row}>
            <Button variant="primary" icon={<Dices size={15} />} onClick={() => create.mutate()}
              disabled={create.isPending || (choice === 'pick' && goods.length === 0 && preset.goods === 'Pick')}>
              {t('Gerar mercado', 'Generate market')}
            </Button>
          </div>
        </div>

        <aside className={styles.dialog}>
          <dl className={styles.facts}>
            <dt>{t('Tipo', 'Type')}</dt><dd>{KIND_LABEL[preset.kind]} {t('· letra ', '· letter ')}{preset.letter}</dd>
            <dt>{t('Quem', 'Who')}</dt><dd>{tv(preset.organizer.name)} · {preset.organizer.role}</dd>
            {preset.summary && <><dt>{t('Resumo', 'Summary')}</dt><dd>{tv(preset.summary)}</dd></>}
            {preset.frequency && <><dt>{t('Quando', 'When')}</dt><dd>{preset.frequency}</dd></>}
            {preset.location && <><dt>{t('Onde', 'Where')}</dt><dd>{tv(preset.location)}</dd></>}
            {preset.sells && <><dt>{t('Vende', 'Sells')}</dt><dd>{tv(preset.sells)}</dd></>}
            {preset.invite && <><dt>{t('Convite', 'Invitation')}</dt><dd>{preset.invite}</dd></>}
            {preset.admission && <><dt>{t('Entrada', 'Admission')}</dt><dd>{formatEb(preset.admission.price)}eb · {tv(preset.admission.label)}</dd></>}
            {preset.features.length > 0 && <><dt>{t('Extras', 'Extras')}</dt><dd className={styles.tags}>{preset.features.map((f) => <Badge key={f} tone="amber">{FEATURE_LABEL[f]}</Badge>)}</dd></>}
            <dt>{t('Fonte', 'Source')}</dt><dd className={styles.source}>{preset.source}</dd>
          </dl>
        </aside>
      </div>
    </section>
  );
}
