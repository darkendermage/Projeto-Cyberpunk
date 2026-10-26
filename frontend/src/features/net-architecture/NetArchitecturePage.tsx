import { useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { Dices, Network } from 'lucide-react';
import { problemMessage } from '../../api/characters';
import { netApi, useArchitectures, useNetRules, useStoreArchitecture } from '../../api/netArchitecture';
import type { GenerateArchitectureRequest, NetArchitectureRules, NetDifficulty } from '../../api/netArchitectureTypes';
import { Badge, Button, EmptyState, ErrorState, Field, NumberInput, Segmented, Select, Spinner, Switch } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import page from '../../styles/Page.module.css';
import { ArchitectureView } from './ArchitectureView';
import { DIFFICULTY_TONE, formatEbShort } from './netArchitecture';
import { SourcesView } from './SourcesView';
import styles from './NetArchitecture.module.css';
import { t, tv } from '../../i18n';

type Tab = 'library' | 'sources';

export function NetArchitecturePage() {
  const rules = useNetRules();
  const { id } = useParams();
  if (rules.isLoading) return <div className={page.page}><Spinner /></div>;
  if (rules.error || !rules.data) return <div className={page.page}><ErrorState error={rules.error} onRetry={() => rules.refetch()} /></div>;
  return id ? <ArchitectureView id={Number(id)} rules={rules.data} /> : <Home rules={rules.data} />;
}

function Home({ rules }: { rules: NetArchitectureRules }) {
  const [params, setParams] = useSearchParams();
  const tab = (params.get('tab') as Tab | null) ?? 'library';
  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1>{t('Arquiteturas de NET', 'NET Architectures')}</h1>
        <p className={page.lead}>
          {t('Gere a Arquitetura que o Netrunner vai invadir pelos passos do Core (p.210–211): dificuldade, andares e ramos, Lobby e corpo, e o que cada Control Node e File controla no mundo real. Demons, Defesas e o preço de compra seguem p.212–218, e os Watchers do ', 'Generate the Architecture the Netrunner will break into, following the Core steps (p.210–211): difficulty, floors and branches, Lobby and body, and what each Control Node and File controls in the real world. Demons, Defenses and the purchase price follow p.212–218, and the Watchers from ')}<em>Going Quiet</em> {t('ficam prontos para o stealth.', 'are ready for stealth.')}
        </p>
      </header>
      <Segmented ariaLabel={t('Seção', 'Section')} value={tab} onChange={(entry) => setParams(entry === 'library' ? {} : { tab: entry })} options={[
        { value: 'library', label: t('Arquiteturas', 'Architectures') },
        { value: 'sources', label: t('Tabelas e fontes', 'Tables and sources') },
      ]} />
      {tab === 'library' ? <Library rules={rules} /> : <SourcesView rules={rules} />}
    </div>
  );
}

function Library({ rules }: { rules: NetArchitectureRules }) {
  const list = useArchitectures();
  const difficulties = rules.rules.difficulties.value;
  return (
    <>
      <CreateArchitecture rules={rules} />
      <section className={page.section}>
        <h2>{t('Arquiteturas salvas', 'Saved Architectures')}</h2>
        {list.isLoading ? <Spinner /> : (list.data ?? []).length === 0 ? (
          <EmptyState title={t('Nenhuma Arquitetura ainda', 'No Architectures yet')} icon={<Network size={30} />}>{t('Gere a primeira acima.', 'Generate the first one above.')}</EmptyState>
        ) : (
          <div className={styles.library}>
            {list.data!.map((a) => (
              <Link key={a.id} to={`/net-architecture/${a.id}`} className={styles.card}>
                <h3>{tv(a.name)}</h3>
                <span className={styles.muted}>{tv(a.location.label)} · {a.floors} {t('andares', 'floors')}{a.branches > 0 ? t(` · ${a.branches} ramo(s)`, ` · ${a.branches} branch(es)`) : ''}</span>
                <span className={styles.row}>
                  <Badge tone={DIFFICULTY_TONE[a.difficulty]}>{tv(difficulties.find((d) => d.id === a.difficulty)?.label) ?? a.difficulty}</Badge>
                  <span className={styles.source}>{formatEbShort(a.total)} · {a.portable ? t('portátil', 'portable') : t('não portátil', 'not portable')}</span>
                </span>
              </Link>
            ))}
          </div>
        )}
      </section>
    </>
  );
}

function CreateArchitecture({ rules }: { rules: NetArchitectureRules }) {
  const toast = useToast();
  const navigate = useNavigate();
  const store = useStoreArchitecture();
  const r = rules.rules;
  const [difficulty, setDifficulty] = useState<NetDifficulty | ''>('');
  const [floors, setFloors] = useState<number | null>(null);
  const [branches, setBranches] = useState<number | null>(null);
  const [locationId, setLocationId] = useState('generic');
  const [seed, setSeed] = useState<number | null>(null);
  const [respect, setRespect] = useState(r.generator.respectNodeLimit.value);
  const [netrunner, setNetrunner] = useState<'roll' | 'yes' | 'no'>('roll');
  const [name, setName] = useState('');

  const chosen = r.difficulties.value.find((d) => d.id === difficulty);
  const location = r.locations.value.find((l) => l.id === locationId);

  const create = useMutation({
    mutationFn: () => {
      const body: GenerateArchitectureRequest = {
        name: name.trim() || null,
        difficulty: difficulty || null,
        floors,
        branches,
        locationId,
        seed,
        respectNodeLimit: respect,
        enemyNetrunner: netrunner === 'roll' ? null : netrunner === 'yes',
      };
      return netApi.create(body);
    },
    onSuccess: (detail) => {
      store(detail);
      toast.notify(t(`Arquitetura gerada: ${detail.name}.`, `Architecture generated: ${detail.name}.`));
      navigate(`/net-architecture/${detail.id}`);
    },
    onError: (error) => toast.notify(problemMessage(error), 'error'),
  });

  return (
    <section className={styles.panel}>
      <h2 className={styles.panelTitle}>{t('Nova Arquitetura', 'New Architecture')}</h2>
      <div className={styles.formGrid}>
        <Field label={t('Dificuldade (Passo 0)', 'Difficulty (Step 0)')} htmlFor="na-difficulty"
          hint={chosen ? t(`DV${chosen.dv} · Netrunner com chance: Interface ${chosen.fightingChance}+ · ${tv(chosen.description)}`, `DV${chosen.dv} · Netrunner with a fighting chance: Interface ${chosen.fightingChance}+ · ${tv(chosen.description)}`) : t(`Sorteada · ${r.difficulties.source}`, `Random · ${r.difficulties.source}`)}>
          <Select id="na-difficulty" value={difficulty} onChange={(e) => setDifficulty(e.target.value as NetDifficulty | '')}>
            <option value="">{t('Sortear', 'Random')}</option>
            {r.difficulties.value.map((d) => <option key={d.id} value={d.id}>{tv(d.label)} (DV{d.dv})</option>)}
          </Select>
        </Field>
        <Field label={t('Andares (Passo 1)', 'Floors (Step 1)')} htmlFor="na-floors" hint={floors == null ? t('Vazio: rola 3d6 (3 a 18)', 'Empty: rolls 3d6 (3 to 18)') : t('De 3 a 18', 'From 3 to 18')}>
          <NumberInput id="na-floors" min={3} max={18} allowEmpty value={floors} onChange={setFloors} placeholder="3d6" />
        </Field>
        <Field label={t('Ramos', 'Branches')} htmlFor="na-branches" hint={branches == null ? t('Vazio: 1d10 por ramo, 7+ cria um ramo', 'Empty: 1d10 per branch, 7+ creates a branch') : t('Ramos que não couberem são descartados', 'Branches that don\'t fit are dropped')}>
          <NumberInput id="na-branches" min={0} max={8} allowEmpty value={branches} onChange={setBranches} placeholder="1d10" />
        </Field>
        <Field label={t('Tipo de local (Passo 3)', 'Location type (Step 3)')} htmlFor="na-location" hint={tv(location?.description)}>
          <Select id="na-location" value={locationId} onChange={(e) => setLocationId(e.target.value)}>
            {r.locations.value.map((l) => <option key={l.id} value={l.id}>{tv(l.label)}</option>)}
          </Select>
        </Field>
        <Field label={t('Netrunner inimigo (Watcher)', 'Enemy Netrunner (Watcher)')} htmlFor="na-netrunner" hint="Going Quiet p.4–5">
          <Select id="na-netrunner" value={netrunner} onChange={(e) => setNetrunner(e.target.value as 'roll' | 'yes' | 'no')}>
            <option value="roll">{t('Sortear pela dificuldade', 'Random by difficulty')}</option>
            <option value="yes">{t('Sim, plugado no fundo', 'Yes, jacked in at the bottom')}</option>
            <option value="no">{t('Não', 'No')}</option>
          </Select>
        </Field>
        <Field label={t('Seed (opcional)', 'Seed (optional)')} htmlFor="na-seed" hint={t('A mesma seed gera a mesma Arquitetura.', 'The same seed generates the same Architecture.')}>
          <NumberInput id="na-seed" min={1} max={2147483647} allowEmpty value={seed} onChange={setSeed} placeholder={t('Aleatória', 'Random')} />
        </Field>
        <Field label={t('Nome (opcional)', 'Name (optional)')} htmlFor="na-name">
          <input id="na-name" value={name} maxLength={80} onChange={(e) => setName(e.target.value)} placeholder={t('Ex.: Servidor da Biotechnica', 'E.g. Biotechnica server')} />
        </Field>
      </div>
      <div className={styles.row}>
        <Switch checked={respect} onChange={setRespect} label={t(`Respeitar o limite de Control Nodes da compra (${r.pricing.floors.source})`, `Respect the purchase's Control Node limit (${r.pricing.floors.source})`)} />
      </div>
      <div className={styles.row}>
        <Button variant="primary" icon={create.isPending ? <Spinner /> : <Dices size={15} />} disabled={create.isPending} onClick={() => create.mutate()}>
          {t('Gerar Arquitetura', 'Generate Architecture')}
        </Button>
      </div>
    </section>
  );
}
