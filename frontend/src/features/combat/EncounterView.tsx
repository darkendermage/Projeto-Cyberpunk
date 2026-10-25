import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import {
  Activity, ArrowLeft, Bandage, Copy, Dices, Flag, HeartPulse, Shield, SkipForward, Skull, Swords, Trash2, UserPlus,
} from 'lucide-react';
import clsx from 'clsx';
import { problemMessage } from '../../api/characters';
import { encounterApi, useEncounter, useEncounterCommand } from '../../api/encounters';
import type { EncounterDetail, EncounterRules, FighterView, InjuryLocation } from '../../api/encounterTypes';
import { Badge, Button, Checkbox, Collapsible, ErrorState, IconButton, NumberInput, Select, Spinner, Switch } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import page from '../../styles/Page.module.css';
import { AddCombatantsDialog } from './AddCombatantsDialog';
import { AttackPanel, AttackResultView } from './AttackPanel';
import { CONDITIONS, hpPercent, WOUND_LABEL, WOUND_TONE } from './combat';
import styles from './Combat.module.css';

export type Run = (path: string, body?: Record<string, unknown>, done?: string, method?: 'POST' | 'PUT') => void;

export function EncounterView({ id, rules }: { id: number; rules: EncounterRules }) {
  const query = useEncounter(id);
  if (query.isLoading) return <div className={page.page}><Spinner /></div>;
  if (query.error || !query.data) return <div className={page.page}><ErrorState error={query.error} onRetry={() => query.refetch()} /></div>;
  return <Table detail={query.data} rules={rules} />;
}

function Table({ detail, rules }: { detail: EncounterDetail; rules: EncounterRules }) {
  const toast = useToast();
  const navigate = useNavigate();
  const command = useEncounterCommand(detail.id);
  const [adding, setAdding] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const busy = command.isPending;
  const active = detail.queue.find((f) => f.id === detail.activeId) ?? null;
  const selected = detail.queue.find((f) => f.id === (selectedId ?? detail.activeId)) ?? detail.queue[0] ?? null;

  const run: Run = (path, body = {}, done, method) =>
    command.mutate({ path, body, method }, {
      onSuccess: (d) => {
        if (done) toast.notify(done);
        d.notices.forEach((n) => toast.notify(n, 'info'));
      },
      onError: (error) => toast.notify(problemMessage(error), 'error'),
    });

  const remove = async () => {
    if (!window.confirm(`Excluir "${detail.name}"? As fichas dos PCs não mudam.`)) return;
    await encounterApi.remove(detail.id);
    navigate('/combat');
  };

  const copyLog = async () => {
    await navigator.clipboard.writeText(detail.log.map((l) => `[R${l.round}] ${l.text}`).join('\n'));
    toast.notify('Log copiado.');
  };

  return (
    <div className={page.page}>
      <Link to="/combat" className={styles.back}><ArrowLeft size={14} /> Combates</Link>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>{detail.name}</h1>
          <span className={styles.row}>
            <Badge tone={detail.status === 'Active' ? 'red' : 'default'}>{detail.status === 'Active' ? `Rodada ${detail.round}` : 'Encerrado'}</Badge>
            {active && <span className={styles.muted}>Turno de <strong>{active.name}</strong></span>}
          </span>
        </div>
        <div className={styles.actions}>
          <Button icon={<UserPlus size={15} />} onClick={() => setAdding(true)}>Combatentes</Button>
          <Button icon={<Dices size={15} />} disabled={busy || detail.queue.length === 0}
            onClick={() => run('initiative', {}, 'Iniciativa rolada (REF + 1d10).')}>Rolar iniciativa</Button>
          <Button variant="primary" icon={<SkipForward size={15} />} disabled={busy || detail.queue.every((f) => f.initiative == null)}
            onClick={() => run('next-turn')}>Próximo turno</Button>
          <Button variant="ghost" icon={<Flag size={15} />} disabled={busy} onClick={() => run('end')}>
            {detail.status === 'Active' ? 'Encerrar' : 'Reabrir'}
          </Button>
          <Button variant="ghost" icon={<Trash2 size={15} />} onClick={remove}>Excluir</Button>
        </div>
      </header>

      {detail.queue.length === 0 ? (
        <section className={styles.panel}>
          <p className={styles.muted}>Ninguém na mesa ainda. Adicione PCs, NPCs salvos, inimigos gerados ou genéricos.</p>
          <div><Button variant="primary" icon={<UserPlus size={15} />} onClick={() => setAdding(true)}>Adicionar combatentes</Button></div>
        </section>
      ) : (
        <div className={styles.layout}>
          <section className={styles.panel} aria-label="Fila de iniciativa">
            <h2 className={styles.panelTitle}>Fila de iniciativa</h2>
            <ol className={styles.queue}>
              {detail.queue.map((f) => (
                <QueueRow key={f.id} f={f} active={f.id === detail.activeId} selected={f.id === selected?.id}
                  cover={detail.covers.find((c) => c.id === f.coverId)?.label} onSelect={() => setSelectedId(f.id)} />
              ))}
            </ol>
            <span className={styles.source}>{rules.rules.initiative.value} · {rules.rules.initiativeTies.value} ({rules.rules.initiative.source})</span>
          </section>

          <div className={styles.main}>
            <AttackPanel key={selected?.id} detail={detail} rules={rules} attacker={selected} busy={busy} run={run} />
            {detail.lastAttack && <AttackResultView result={detail.lastAttack} />}
            {selected && <FighterPanel key={selected.id} f={selected} detail={detail} rules={rules} busy={busy} run={run} />}
          </div>
        </div>
      )}

      <div className={styles.columns}>
        <CoversPanel detail={detail} rules={rules} busy={busy} run={run} />
        <section className={styles.panel}>
          <div className={styles.rowBetween}>
            <h2 className={styles.panelTitle}>Log</h2>
            <IconButton label="Copiar log" onClick={copyLog}><Copy size={14} /></IconButton>
          </div>
          <ol className={styles.log}>
            {[...detail.log].reverse().map((l, i) => (
              <li key={`${l.at}-${i}`}><span className={styles.source}>R{l.round}</span> {l.text}</li>
            ))}
          </ol>
        </section>
      </div>

      <Collapsible title="Ações do turno (CP:R p.168–169)">
        <ul className={styles.actionsHelp}>
          {rules.rules.actions.value.map((a) => <li key={a.name}><strong>{a.label}</strong> <span className={styles.source}>{a.name}</span> — {a.text}</li>)}
        </ul>
        <p className={styles.muted}>{rules.rules.turn.value}</p>
      </Collapsible>

      <AddCombatantsDialog open={adding} onClose={() => setAdding(false)} rules={rules} busy={busy} run={run} />
    </div>
  );
}

function QueueRow({ f, active, selected, cover, onSelect }: { f: FighterView; active: boolean; selected: boolean; cover?: string; onSelect: () => void }) {
  return (
    <li>
      <button type="button" onClick={onSelect}
        className={clsx(styles.queueRow, active && styles.queueActive, selected && styles.queueSelected, f.side === 'Enemy' ? styles.enemy : styles.ally, f.dead && styles.dead)}>
        <span className={styles.init}>{f.initiative ?? '—'}</span>
        <span className={styles.queueMain}>
          <span className={styles.queueName}>{f.dead && <Skull size={13} />} {f.name}</span>
          <span className={styles.hpBar} aria-hidden><span style={{ width: `${hpPercent(f)}%` }} data-state={f.woundState} /></span>
          <span className={styles.queueMeta}>
            HP {f.hp}/{f.maxHp} · SP {f.spBody}/{f.spHead}
            {f.woundState !== 'Unhurt' && <> · <span className={styles[`w${f.woundState}`]}>{WOUND_LABEL[f.woundState]}</span></>}
            {cover && <> · <Shield size={11} /> {cover}</>}
            {f.conditions.length > 0 && <> · {f.conditions.map((c) => c.replace('OnFire:', '🔥 ')).join(', ')}</>}
          </span>
        </span>
        {f.mustDeathSave && !f.dead && <span className={styles.deathFlag} title="Death Save no início do turno">DS</span>}
      </button>
    </li>
  );
}

function FighterPanel({ f, detail, rules, busy, run }: { f: FighterView; detail: EncounterDetail; rules: EncounterRules; busy: boolean; run: Run }) {
  const [amount, setAmount] = useState<number | null>(null);
  const [location, setLocation] = useState<InjuryLocation>('Body');
  const [bypass, setBypass] = useState(false);
  const [halve, setHalve] = useState(false);
  const [critical, setCritical] = useState(false);
  const [roll, setRoll] = useState<number | null>(null);
  const [initiative, setInitiative] = useState<number | null>(f.initiative);
  const [environment, setEnvironment] = useState(rules.rules.environment.value[0].id);
  const base = `combatants/${f.id}`;
  const fire = f.conditions.find((c) => c.startsWith('OnFire:'));

  return (
    <section className={styles.panel}>
      <div className={styles.rowBetween}>
        <h2 className={styles.panelTitle}>{f.name}</h2>
        <span className={styles.row}>
          <Badge tone={f.side === 'Enemy' ? 'red' : 'cyan'}>{f.side === 'Enemy' ? 'Inimigo' : 'Aliado'}</Badge>
          <Badge tone={WOUND_TONE[f.woundState]}>{WOUND_LABEL[f.woundState]}</Badge>
          {f.kind === 'Pc' && f.characterId && <Link to={`/characters/${f.characterId}`} className={styles.link}>Abrir ficha</Link>}
        </span>
      </div>
      <dl className={styles.stats}>
        <div><dt>HP</dt><dd>{f.hp}/{f.maxHp}</dd></div>
        <div><dt>SP corpo</dt><dd>{f.spBody}/{f.maxSpBody}</dd></div>
        <div><dt>SP cabeça</dt><dd>{f.spHead}/{f.maxSpHead}</dd></div>
        <div><dt>REF</dt><dd>{f.ref}</dd></div>
        <div><dt>DEX</dt><dd>{f.dex}</dd></div>
        <div><dt>MOVE</dt><dd>{f.move}</dd></div>
        <div><dt>Evasion</dt><dd>{f.evasion}</dd></div>
        <div><dt>Death Save</dt><dd>&lt; {f.deathSaveTarget} (+{f.deathSavePenalty})</dd></div>
        <div><dt>Penalidade</dt><dd>{f.actionPenalty}</dd></div>
      </dl>
      {f.source && <span className={styles.source}>{f.source}</span>}
      {f.injuries.length > 0 && (
        <ul className={styles.injuries}>
          {f.injuries.map((i) => <li key={i.name}><strong>{i.name}</strong> ({i.location === 'Head' ? 'cabeça' : 'corpo'}) — {i.effect}</li>)}
        </ul>
      )}

      <div className={styles.block}>
        <span className={styles.label}>Dano manual</span>
        <div className={styles.row}>
          <NumberInput aria-label="Dano" min={0} max={999} allowEmpty value={amount} onChange={setAmount} className={styles.narrow} />
          <Select aria-label="Local" value={location} onChange={(e) => setLocation(e.target.value as InjuryLocation)}>
            <option value="Body">Corpo</option>
            <option value="Head">Cabeça (×2)</option>
          </Select>
          <Switch checked={halve} onChange={setHalve} label="Metade do SP" />
          <Switch checked={bypass} onChange={setBypass} label="Ignora armadura" />
          <Switch checked={critical} onChange={setCritical} label="Crítico" />
          <Button icon={<Swords size={14} />} disabled={busy || amount == null}
            onClick={() => run(`${base}/damage`, { amount, location, bypassArmor: bypass, halveArmor: halve, critical })}>Aplicar</Button>
          <Button icon={<Bandage size={14} />} disabled={busy || !amount} onClick={() => run(`${base}/heal`, { amount })}>Curar</Button>
        </div>
      </div>

      <div className={styles.block}>
        <span className={styles.label}>Ferimentos graves</span>
        <div className={styles.row}>
          <Button icon={<Activity size={14} />} disabled={busy || f.woundState === 'Unhurt' || f.dead}
            onClick={() => run(`${base}/stabilize`)}>Estabilizar{f.stabilizationDv ? ` (DV${f.stabilizationDv})` : ''}</Button>
          <NumberInput aria-label="d10 do Death Save" min={1} max={10} allowEmpty value={roll} onChange={setRoll} className={styles.narrow} placeholder="d10" />
          <Button icon={<HeartPulse size={14} />} disabled={busy || !f.mustDeathSave} onClick={() => run(`${base}/death-save`, { roll })}>
            {roll ? 'Registrar' : 'Rolar'} Death Save
          </Button>
        </div>
      </div>

      <div className={styles.block}>
        <span className={styles.label}>Condições, cobertura e ambiente</span>
        <div className={styles.row}>
          {CONDITIONS.map((c) => (
            <Checkbox key={c} checked={f.conditions.includes(c)} label={c} onChange={(on) => run(`${base}/condition`, { condition: c, on })} />
          ))}
        </div>
        <div className={styles.row}>
          <Select aria-label="Atrás de" value={f.coverId ?? ''} disabled={busy}
            onChange={(e) => run(`${base}/cover`, { coverId: e.target.value || null })}>
            <option value="">Sem cobertura</option>
            {detail.covers.filter((c) => c.hp > 0).map((c) => <option key={c.id} value={c.id}>{c.label} ({c.hp} HP)</option>)}
          </Select>
          <Select aria-label="Fogo" value={fire ?? ''} disabled={busy}
            onChange={(e) => e.target.value
              ? run(`${base}/condition`, { condition: e.target.value, on: true })
              : fire && run(`${base}/condition`, { condition: fire, on: false })}>
            <option value="">Sem fogo</option>
            {rules.rules.environment.value.filter((x) => x.id.startsWith('fire')).map((x) => <option key={x.id} value={`OnFire:${x.id}`}>{x.label}</option>)}
          </Select>
        </div>
        <div className={styles.row}>
          <Select aria-label="Dano de ambiente" value={environment} onChange={(e) => setEnvironment(e.target.value)}>
            {rules.rules.environment.value.map((x) => <option key={x.id} value={x.id}>{x.label} ({x.damage})</option>)}
          </Select>
          <Button disabled={busy} onClick={() => run(`${base}/environment`, { environmentId: environment })}>Aplicar ambiente</Button>
        </div>
        <span className={styles.source}>{rules.rules.environment.value.find((x) => x.id === environment)?.note} ({rules.rules.environment.source})</span>
      </div>

      <div className={styles.row}>
        <NumberInput aria-label="Iniciativa" min={-20} max={60} allowEmpty value={initiative} onChange={setInitiative} className={styles.narrow} />
        <Button disabled={busy || initiative == null} onClick={() => run(`${base}/initiative`, { initiative })}>Definir iniciativa</Button>
        <Button disabled={busy} onClick={() => run(`${base}/active`)}>Passar o turno para</Button>
        <Button variant="ghost" icon={<Trash2 size={14} />} disabled={busy}
          onClick={() => window.confirm(`Tirar ${f.name} do combate?`) && run(`${base}/remove`)}>Remover</Button>
      </div>
    </section>
  );
}

function CoversPanel({ detail, rules, busy, run }: { detail: EncounterDetail; rules: EncounterRules; busy: boolean; run: Run }) {
  const examples = rules.rules.coverExamples.value;
  const [example, setExample] = useState(0);
  const [damage, setDamage] = useState<Record<string, number | null>>({});
  const pick = examples[example];
  return (
    <section className={styles.panel}>
      <h2 className={styles.panelTitle}>Coberturas</h2>
      <p className={styles.source}>{rules.rules.coverRules.value} ({rules.rules.coverRules.source})</p>
      <div className={styles.row}>
        <Select aria-label="Exemplo de cobertura" value={example} onChange={(e) => setExample(Number(e.target.value))}>
          {examples.map((x, i) => <option key={x.label} value={i}>{x.label} ({x.hp} HP)</option>)}
        </Select>
        <Button icon={<Shield size={14} />} disabled={busy || pick.hp <= 0}
          onClick={() => run('covers', { label: pick.label, material: pick.material, thick: pick.thick, hp: pick.hp })}>Adicionar</Button>
      </div>
      <ul className={styles.covers}>
        {detail.covers.map((c) => (
          <li key={c.id} className={clsx(c.hp <= 0 && styles.dead)}>
            <span className={styles.rowBetween}>
              <span><strong>{c.label}</strong> <span className={styles.source}>{c.hp}/{c.maxHp} HP</span></span>
              <IconButton label={`Remover ${c.label}`} onClick={() => run(`covers/${c.id}/remove`)} disabled={busy}><Trash2 size={13} /></IconButton>
            </span>
            <span className={styles.hpBar} aria-hidden><span style={{ width: `${(c.hp / c.maxHp) * 100}%` }} /></span>
            <span className={styles.muted}>
              Atrás: {detail.queue.filter((f) => f.coverId === c.id).map((f) => f.name).join(', ') || 'ninguém'}
            </span>
            {c.hp > 0 && (
              <span className={styles.row}>
                <NumberInput aria-label="Dano na cobertura" min={0} max={999} allowEmpty value={damage[c.id] ?? null}
                  onChange={(v) => setDamage({ ...damage, [c.id]: v })} className={styles.narrow} />
                <Button disabled={busy || !damage[c.id]} onClick={() => run(`covers/${c.id}/damage`, { amount: damage[c.id] })}>Dano</Button>
              </span>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
