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
        <h1>Arquiteturas de NET</h1>
        <p className={page.lead}>
          Gere a Arquitetura que o Netrunner vai invadir pelos passos do Core (p.210–211): dificuldade, andares e ramos, Lobby e corpo,
          e o que cada Control Node e File controla no mundo real. Demons, Defesas e o preço de compra seguem p.212–218, e os Watchers
          do <em>Going Quiet</em> ficam prontos para o stealth.
        </p>
      </header>
      <Segmented ariaLabel="Seção" value={tab} onChange={(t) => setParams(t === 'library' ? {} : { tab: t })} options={[
        { value: 'library', label: 'Arquiteturas' },
        { value: 'sources', label: 'Tabelas e fontes' },
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
        <h2>Arquiteturas salvas</h2>
        {list.isLoading ? <Spinner /> : (list.data ?? []).length === 0 ? (
          <EmptyState title="Nenhuma Arquitetura ainda" icon={<Network size={30} />}>Gere a primeira acima.</EmptyState>
        ) : (
          <div className={styles.library}>
            {list.data!.map((a) => (
              <Link key={a.id} to={`/net-architecture/${a.id}`} className={styles.card}>
                <h3>{a.name}</h3>
                <span className={styles.muted}>{a.location.label} · {a.floors} andares{a.branches > 0 ? ` · ${a.branches} ramo(s)` : ''}</span>
                <span className={styles.row}>
                  <Badge tone={DIFFICULTY_TONE[a.difficulty]}>{difficulties.find((d) => d.id === a.difficulty)?.label ?? a.difficulty}</Badge>
                  <span className={styles.source}>{formatEbShort(a.total)} · {a.portable ? 'portátil' : 'não portátil'}</span>
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
      toast.notify(`Arquitetura gerada: ${detail.name}.`);
      navigate(`/net-architecture/${detail.id}`);
    },
    onError: (error) => toast.notify(problemMessage(error), 'error'),
  });

  return (
    <section className={styles.panel}>
      <h2 className={styles.panelTitle}>Nova Arquitetura</h2>
      <div className={styles.formGrid}>
        <Field label="Dificuldade (Passo 0)" htmlFor="na-difficulty"
          hint={chosen ? `DV${chosen.dv} · Netrunner com chance: Interface ${chosen.fightingChance}+ · ${chosen.description}` : `Sorteada · ${r.difficulties.source}`}>
          <Select id="na-difficulty" value={difficulty} onChange={(e) => setDifficulty(e.target.value as NetDifficulty | '')}>
            <option value="">Sortear</option>
            {r.difficulties.value.map((d) => <option key={d.id} value={d.id}>{d.label} (DV{d.dv})</option>)}
          </Select>
        </Field>
        <Field label="Andares (Passo 1)" htmlFor="na-floors" hint={floors == null ? 'Vazio: rola 3d6 (3 a 18)' : 'De 3 a 18'}>
          <NumberInput id="na-floors" min={3} max={18} allowEmpty value={floors} onChange={setFloors} placeholder="3d6" />
        </Field>
        <Field label="Ramos" htmlFor="na-branches" hint={branches == null ? 'Vazio: 1d10 por ramo, 7+ cria um ramo' : 'Ramos que não couberem são descartados'}>
          <NumberInput id="na-branches" min={0} max={8} allowEmpty value={branches} onChange={setBranches} placeholder="1d10" />
        </Field>
        <Field label="Tipo de local (Passo 3)" htmlFor="na-location" hint={location?.description}>
          <Select id="na-location" value={locationId} onChange={(e) => setLocationId(e.target.value)}>
            {r.locations.value.map((l) => <option key={l.id} value={l.id}>{l.label}</option>)}
          </Select>
        </Field>
        <Field label="Netrunner inimigo (Watcher)" htmlFor="na-netrunner" hint="Going Quiet p.4–5">
          <Select id="na-netrunner" value={netrunner} onChange={(e) => setNetrunner(e.target.value as 'roll' | 'yes' | 'no')}>
            <option value="roll">Sortear pela dificuldade</option>
            <option value="yes">Sim, plugado no fundo</option>
            <option value="no">Não</option>
          </Select>
        </Field>
        <Field label="Seed (opcional)" htmlFor="na-seed" hint="A mesma seed gera a mesma Arquitetura.">
          <NumberInput id="na-seed" min={1} max={2147483647} allowEmpty value={seed} onChange={setSeed} placeholder="Aleatória" />
        </Field>
        <Field label="Nome (opcional)" htmlFor="na-name">
          <input id="na-name" value={name} maxLength={80} onChange={(e) => setName(e.target.value)} placeholder="Ex.: Servidor da Biotechnica" />
        </Field>
      </div>
      <div className={styles.row}>
        <Switch checked={respect} onChange={setRespect} label={`Respeitar o limite de Control Nodes da compra (${r.pricing.floors.source})`} />
      </div>
      <div className={styles.row}>
        <Button variant="primary" icon={create.isPending ? <Spinner /> : <Dices size={15} />} disabled={create.isPending} onClick={() => create.mutate()}>
          Gerar Arquitetura
        </Button>
      </div>
    </section>
  );
}
