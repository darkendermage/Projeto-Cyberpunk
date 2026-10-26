import { useCallback, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router';
import {
  Activity, ArrowLeft, Bandage, ChevronDown, ChevronUp, Copy, Dices, Flag, GripVertical, HeartPulse, IdCard, Shield, SkipForward, Skull, Swords,
  Trash2, UserPlus, X,
} from 'lucide-react';
import clsx from 'clsx';
import { problemMessage } from '../../api/characters';
import { encounterApi, encounterKeys, useEncounter, useEncounterCommand } from '../../api/encounters';
import type { EncounterDetail, EncounterRules, FighterView, InjuryLocation } from '../../api/encounterTypes';
import { Badge, Button, Checkbox, Collapsible, ErrorState, IconButton, NumberInput, Select, Spinner, Switch } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import { EmbeddedCharacter, type CharacterTab } from '../characters/CharacterPage';
import page from '../../styles/Page.module.css';
import { AddCombatantsDialog } from './AddCombatantsDialog';
import { AttackPanel, AttackResultView } from './AttackPanel';
import { CONDITIONS, hpPercent, WOUND_LABEL, WOUND_TONE } from './combat';
import { FighterSheet } from './FighterSheet';
import styles from './Combat.module.css';
import { t, tv } from '../../i18n';

export type Run = (path: string, body?: Record<string, unknown>, done?: string, method?: 'POST' | 'PUT') => void;

/** A sheet opened in its own tab next to the table; tab is the section a PC sheet shows. */
interface SheetTab { fighterId: string; tab: CharacterTab }

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
  const [sheets, setSheets] = useState<SheetTab[]>([]);
  const [current, setCurrent] = useState<string | null>(null);
  const [dragId, setDragId] = useState<string | null>(null);
  const [drop, setDrop] = useState<{ id: string; after: boolean } | null>(null);
  const busy = command.isPending;
  const canMove = !busy && detail.queue.some((f) => f.initiative != null);
  const active = detail.queue.find((f) => f.id === detail.activeId) ?? null;
  const selected = detail.queue.find((f) => f.id === (selectedId ?? detail.activeId)) ?? detail.queue[0] ?? null;
  // Someone who leaves the fight takes their tab along.
  const open = sheets.filter((s) => detail.queue.some((f) => f.id === s.fighterId));
  const shown = detail.queue.find((f) => f.id === current && open.some((s) => s.fighterId === f.id)) ?? null;

  const openSheet = (id: string) => {
    setSheets((list) => (list.some((s) => s.fighterId === id) ? list : [...list, { fighterId: id, tab: 'ficha' }]));
    setCurrent(id);
  };
  const closeSheet = (id: string) => {
    setSheets((list) => list.filter((s) => s.fighterId !== id));
    if (current === id) setCurrent(null);
  };

  const run: Run = (path, body = {}, done, method) =>
    command.mutate({ path, body, method }, {
      onSuccess: (d) => {
        if (done) toast.notify(done);
        d.notices.forEach((n) => toast.notify(n, 'info'));
      },
      onError: (error) => toast.notify(problemMessage(error), 'error'),
    });

  // Moving takes the neighbor's Initiative on the server, so a drop that keeps the same place sends nothing.
  const move = (id: string, beforeId: string | null) => {
    const at = detail.queue.findIndex((f) => f.id === id);
    if (beforeId === id || beforeId === (detail.queue[at + 1]?.id ?? null)) return;
    run(`combatants/${id}/move`, { beforeId });
  };

  const dropOn = (index: number, after: boolean) => {
    if (dragId) move(dragId, after ? detail.queue[index + 1]?.id ?? null : detail.queue[index].id);
    setDragId(null);
    setDrop(null);
  };

  const remove = async () => {
    if (!window.confirm(t(`Excluir "${detail.name}"? As fichas dos PCs não mudam.`, `Delete "${detail.name}"? The PCs' sheets don't change.`))) return;
    await encounterApi.remove(detail.id);
    navigate('/combat');
  };

  const copyLog = async () => {
    await navigator.clipboard.writeText(detail.log.map((l) => `[R${l.round}] ${l.text}`).join('\n'));
    toast.notify(t('Log copiado.', 'Log copied.'));
  };

  return (
    <div className={page.page}>
      <Link to="/combat" className={styles.back}><ArrowLeft size={14} /> {t('Combates', 'Fights')}</Link>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>{tv(detail.name)}</h1>
          <span className={styles.row}>
            <Badge tone={detail.status === 'Active' ? 'red' : 'default'}>{detail.status === 'Active' ? t(`Rodada ${detail.round}`, `Round ${detail.round}`) : t('Encerrado', 'Ended')}</Badge>
            {active && <span className={styles.muted}>{t('Turno de ', 'Turn of ')}<strong>{tv(active.name)}</strong></span>}
          </span>
        </div>
        <div className={styles.actions}>
          <Button icon={<UserPlus size={15} />} onClick={() => setAdding(true)}>{t('Combatentes', 'Combatants')}</Button>
          <Button icon={<Dices size={15} />} disabled={busy || detail.queue.length === 0}
            onClick={() => run('initiative', {}, t('Iniciativa rolada (REF + 1d10).', 'Initiative rolled (REF + 1d10).'))}>{t('Rolar iniciativa', 'Roll initiative')}</Button>
          <Button variant="primary" icon={<SkipForward size={15} />} disabled={busy || detail.queue.every((f) => f.initiative == null)}
            onClick={() => run('next-turn')}>{t('Próximo turno', 'Next turn')}</Button>
          <Button variant="ghost" icon={<Flag size={15} />} disabled={busy} onClick={() => run('end')}>
            {detail.status === 'Active' ? t('Encerrar', 'End') : t('Reabrir', 'Reopen')}
          </Button>
          <Button variant="ghost" icon={<Trash2 size={15} />} onClick={remove}>{t('Excluir', 'Delete')}</Button>
        </div>
      </header>

      {open.length > 0 && (
        <nav className={styles.sheetTabs} aria-label={t('Abas da mesa', 'Table tabs')}>
          <button type="button" className={clsx(styles.sheetTab, !shown && styles.sheetTabActive)} aria-current={!shown ? 'page' : undefined}
            onClick={() => setCurrent(null)}>
            <Swords size={14} /> {t('Mesa', 'Table')}
          </button>
          {open.map((s) => {
            const f = detail.queue.find((x) => x.id === s.fighterId)!;
            return (
              <span key={s.fighterId} className={clsx(styles.sheetTab, shown?.id === f.id && styles.sheetTabActive, f.side === 'Enemy' ? styles.enemy : styles.ally)}
                onAuxClick={(e) => { if (e.button === 1) closeSheet(f.id); }}>
                <button type="button" className={styles.sheetTabName} aria-current={shown?.id === f.id ? 'page' : undefined}
                  onClick={() => setCurrent(f.id)}>
                  <IdCard size={14} /> {tv(f.name)}
                </button>
                <IconButton label={t(`Fechar a ficha de ${tv(f.name)}`, `Close ${tv(f.name)}'s sheet`)} className={styles.sheetTabClose} onClick={() => closeSheet(f.id)}><X size={13} /></IconButton>
              </span>
            );
          })}
        </nav>
      )}

      {shown && (
        <SheetTabView key={shown.id} f={shown} encounterId={detail.id} tab={open.find((s) => s.fighterId === shown.id)!.tab}
          onTab={(tab) => setSheets((list) => list.map((s) => (s.fighterId === shown.id ? { ...s, tab } : s)))} />
      )}

      <div className={styles.tableView} hidden={shown != null}>
      {detail.queue.length === 0 ? (
        <section className={styles.panel}>
          <p className={styles.muted}>{t('Ninguém na mesa ainda. Adicione PCs, NPCs salvos, inimigos gerados ou genéricos.', 'Nobody at the table yet. Add PCs, saved NPCs, generated or generic enemies.')}</p>
          <div><Button variant="primary" icon={<UserPlus size={15} />} onClick={() => setAdding(true)}>{t('Adicionar combatentes', 'Add combatants')}</Button></div>
        </section>
      ) : (
        <div className={styles.layout}>
          <section className={styles.panel} aria-label={t('Fila de iniciativa', 'Initiative Queue')}>
            <h2 className={styles.panelTitle}>{t('Fila de iniciativa', 'Initiative Queue')}</h2>
            <ol className={styles.queue}>
              {detail.queue.map((f, i) => (
                <li key={f.id} draggable={canMove}
                  className={clsx(styles.queueItem, dragId === f.id && styles.dragging,
                    drop?.id === f.id && (drop.after ? styles.dropAfter : styles.dropBefore))}
                  onDragStart={(e) => { e.dataTransfer.effectAllowed = 'move'; e.dataTransfer.setData('text/plain', f.id); setDragId(f.id); }}
                  onDragEnd={() => { setDragId(null); setDrop(null); }}
                  onDragOver={(e) => {
                    if (!dragId) return;
                    e.preventDefault();
                    const box = e.currentTarget.getBoundingClientRect();
                    const after = e.clientY > box.top + box.height / 2;
                    if (drop?.id !== f.id || drop.after !== after) setDrop({ id: f.id, after });
                  }}
                  onDrop={(e) => { e.preventDefault(); dropOn(i, drop?.id === f.id && drop.after); }}>
                  {canMove && <GripVertical size={14} className={styles.grip} aria-hidden />}
                  <QueueRow f={f} active={f.id === detail.activeId} selected={f.id === selected?.id}
                    cover={detail.covers.find((c) => c.id === f.coverId)?.label} onSelect={() => setSelectedId(f.id)} onOpen={() => openSheet(f.id)} />
                  {canMove && (
                    <span className={styles.moveButtons}>
                      <IconButton label={t(`Subir ${tv(f.name)} na fila`, `Move ${tv(f.name)} up the queue`)} disabled={i === 0} className={styles.moveButton}
                        onClick={() => move(f.id, detail.queue[i - 1].id)}><ChevronUp size={13} /></IconButton>
                      <IconButton label={t(`Descer ${tv(f.name)} na fila`, `Move ${tv(f.name)} down the queue`)} disabled={i === detail.queue.length - 1} className={styles.moveButton}
                        onClick={() => move(f.id, detail.queue[i + 2]?.id ?? null)}><ChevronDown size={13} /></IconButton>
                    </span>
                  )}
                </li>
              ))}
            </ol>
            {canMove && <span className={styles.source}>{t('Arraste (ou use ↑/↓) para reordenar: quem é movido assume a iniciativa do vizinho.', 'Drag (or use ↑/↓) to reorder: whoever is moved takes the neighbor\'s Initiative.')}</span>}
            <span className={styles.source}>{t('Duplo clique abre a ficha numa aba.', 'Double-click opens the sheet in a tab.')}</span>
            <span className={styles.source}>{tv(rules.rules.initiative.value)} · {tv(rules.rules.initiativeTies.value)} ({rules.rules.initiative.source})</span>
          </section>

          <div className={styles.main}>
            <AttackPanel key={selected?.id} detail={detail} rules={rules} attacker={selected} busy={busy} run={run} />
            {detail.lastAttack && <AttackResultView result={detail.lastAttack} />}
            {selected && <FighterPanel key={selected.id} f={selected} detail={detail} rules={rules} busy={busy} run={run}
              onSheet={() => openSheet(selected.id)} />}
          </div>
        </div>
      )}

      <div className={styles.columns}>
        <CoversPanel detail={detail} rules={rules} busy={busy} run={run} />
        <section className={styles.panel}>
          <div className={styles.rowBetween}>
            <h2 className={styles.panelTitle}>Log</h2>
            <IconButton label={t('Copiar log', 'Copy log')} onClick={copyLog}><Copy size={14} /></IconButton>
          </div>
          <ol className={styles.log}>
            {[...detail.log].reverse().map((l, i) => (
              <li key={`${l.at}-${i}`}><span className={styles.source}>R{l.round}</span> {tv(l.text)}</li>
            ))}
          </ol>
        </section>
      </div>

      <Collapsible title={t('Ações do turno (CP:R p.168–169)', 'Turn actions (CP:R p.168–169)')}>
        <ul className={styles.actionsHelp}>
          {rules.rules.actions.value.map((a) => <li key={a.name}><strong>{tv(a.label)}</strong> <span className={styles.source}>{tv(a.name)}</span> — {tv(a.text)}</li>)}
        </ul>
        <p className={styles.muted}>{tv(rules.rules.turn.value)}</p>
      </Collapsible>
      </div>

      <AddCombatantsDialog open={adding} onClose={() => setAdding(false)} rules={rules} busy={busy} run={run} />
    </div>
  );
}

function QueueRow({ f, active, selected, cover, onSelect, onOpen }: {
  f: FighterView; active: boolean; selected: boolean; cover?: string; onSelect: () => void; onOpen: () => void;
}) {
  return (
    <>
      <button type="button" onClick={onSelect} onDoubleClick={onOpen}
        className={clsx(styles.queueRow, active && styles.queueActive, selected && styles.queueSelected, f.side === 'Enemy' ? styles.enemy : styles.ally, f.dead && styles.dead)}>
        <span className={styles.init}>{f.initiative ?? '—'}</span>
        <span className={styles.queueMain}>
          <span className={styles.queueName}>{f.dead && <Skull size={13} />} {tv(f.name)}</span>
          <span className={styles.hpBar} aria-hidden><span style={{ width: `${hpPercent(f)}%` }} data-state={f.woundState} /></span>
          <span className={styles.queueMeta}>
            HP {f.hp}/{f.maxHp} · SP {f.spBody}/{f.spHead}
            {f.woundState !== 'Unhurt' && <> · <span className={styles[`w${f.woundState}`]}>{WOUND_LABEL[f.woundState]}</span></>}
            {cover && <> · <Shield size={11} /> {cover}</>}
            {f.conditions.length > 0 && <> · {f.conditions.map((c) => tv(c.replace('OnFire:', '🔥 '))).join(', ')}</>}
          </span>
        </span>
        {f.mustDeathSave && !f.dead && <span className={styles.deathFlag} title={t('Death Save no início do turno', 'Death Save at the start of the turn')}>DS</span>}
      </button>
    </>
  );
}

/**
 * One sheet tab: a PC opens its full sheet (edits save as on the sheet page); NPCs and generic enemies show what they
 * joined the fight with. A PC sheet that moves ahead of the table (an edit made here) reloads the table.
 */
function SheetTabView({ f, encounterId, tab, onTab }: { f: FighterView; encounterId: number; tab: CharacterTab; onTab: (tab: CharacterTab) => void }) {
  const client = useQueryClient();
  const known = f.characterVersion;
  const onVersion = useCallback((version: number) => {
    if (known != null && version !== known) void client.invalidateQueries({ queryKey: encounterKeys.detail(encounterId) });
  }, [client, encounterId, known]);
  if (f.kind === 'Pc' && f.characterId) return <EmbeddedCharacter id={f.characterId} tab={tab} onTab={onTab} onVersion={onVersion} />;
  return <FighterSheet f={f} />;
}

function FighterPanel({ f, detail, rules, busy, run, onSheet }: {
  f: FighterView; detail: EncounterDetail; rules: EncounterRules; busy: boolean; run: Run; onSheet: () => void;
}) {
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
        <h2 className={styles.panelTitle}>{tv(f.name)}</h2>
        <span className={styles.row}>
          <Badge tone={f.side === 'Enemy' ? 'red' : 'cyan'}>{f.side === 'Enemy' ? t('Inimigo', 'Enemy') : t('Aliado', 'Ally')}</Badge>
          <Badge tone={WOUND_TONE[f.woundState]}>{WOUND_LABEL[f.woundState]}</Badge>
          <Button variant="ghost" icon={<IdCard size={14} />} onClick={onSheet}>{t('Abrir ficha em aba', 'Open sheet in a tab')}</Button>
        </span>
      </div>
      <dl className={styles.stats}>
        <div><dt>HP</dt><dd>{f.hp}/{f.maxHp}</dd></div>
        <div><dt>{t('SP corpo', 'Body SP')}</dt><dd>{f.spBody}/{f.maxSpBody}</dd></div>
        <div><dt>{t('SP cabeça', 'Head SP')}</dt><dd>{f.spHead}/{f.maxSpHead}</dd></div>
        <div><dt>REF</dt><dd>{f.ref}</dd></div>
        <div><dt>DEX</dt><dd>{f.dex}</dd></div>
        <div><dt>MOVE</dt><dd>{f.move}</dd></div>
        <div><dt>Evasion</dt><dd>{f.evasion}</dd></div>
        <div><dt>Death Save</dt><dd>&lt; {f.deathSaveTarget} (+{f.deathSavePenalty})</dd></div>
        <div><dt>{t('Penalidade', 'Penalty')}</dt><dd>{f.actionPenalty}</dd></div>
      </dl>
      {f.source && <span className={styles.source}>{f.source}</span>}
      {f.injuries.length > 0 && (
        <ul className={styles.injuries}>
          {f.injuries.map((i) => <li key={i.name}><strong>{tv(i.name)}</strong> ({i.location === 'Head' ? t('cabeça', 'head') : t('corpo', 'body')}) — {tv(i.effect)}</li>)}
        </ul>
      )}

      <div className={styles.block}>
        <span className={styles.label}>{t('Dano manual', 'Manual damage')}</span>
        <div className={styles.row}>
          <NumberInput aria-label={t('Dano', 'Damage')} min={0} max={999} allowEmpty value={amount} onChange={setAmount} className={styles.narrow} />
          <Select aria-label={t('Local', 'Location')} value={location} onChange={(e) => setLocation(e.target.value as InjuryLocation)}>
            <option value="Body">{t('Corpo', 'Body')}</option>
            <option value="Head">{t('Cabeça (×2)', 'Head (×2)')}</option>
          </Select>
          <Switch checked={halve} onChange={setHalve} label={t('Metade do SP', 'Half SP')} />
          <Switch checked={bypass} onChange={setBypass} label={t('Ignora armadura', 'Ignores armor')} />
          <Switch checked={critical} onChange={setCritical} label={t('Crítico', 'Critical')} />
          <Button icon={<Swords size={14} />} disabled={busy || amount == null}
            onClick={() => run(`${base}/damage`, { amount, location, bypassArmor: bypass, halveArmor: halve, critical })}>{t('Aplicar', 'Apply')}</Button>
          <Button icon={<Bandage size={14} />} disabled={busy || !amount} onClick={() => run(`${base}/heal`, { amount })}>{t('Curar', 'Heal')}</Button>
        </div>
      </div>

      <div className={styles.block}>
        <span className={styles.label}>{t('Ferimentos graves', 'Serious wounds')}</span>
        <div className={styles.row}>
          <Button icon={<Activity size={14} />} disabled={busy || f.woundState === 'Unhurt' || f.dead}
            onClick={() => run(`${base}/stabilize`)}>{t('Estabilizar', 'Stabilize')}{f.stabilizationDv ? ` (DV${f.stabilizationDv})` : ''}</Button>
          <NumberInput aria-label={t('d10 do Death Save', 'Death Save d10')} min={1} max={10} allowEmpty value={roll} onChange={setRoll} className={styles.narrow} placeholder="d10" />
          <Button icon={<HeartPulse size={14} />} disabled={busy || !f.mustDeathSave} onClick={() => run(`${base}/death-save`, { roll })}>
            {roll ? t('Registrar', 'Record') : t('Rolar', 'Roll')} Death Save
          </Button>
        </div>
      </div>

      <div className={styles.block}>
        <span className={styles.label}>{t('Condições, cobertura e ambiente', 'Conditions, cover and environment')}</span>
        <div className={styles.row}>
          {CONDITIONS.map((c) => (
            <Checkbox key={c} checked={f.conditions.includes(c)} label={c} onChange={(on) => run(`${base}/condition`, { condition: c, on })} />
          ))}
        </div>
        <div className={styles.row}>
          <Select aria-label={t('Atrás de', 'Behind')} value={f.coverId ?? ''} disabled={busy}
            onChange={(e) => run(`${base}/cover`, { coverId: e.target.value || null })}>
            <option value="">{t('Sem cobertura', 'No cover')}</option>
            {detail.covers.filter((c) => c.hp > 0).map((c) => <option key={c.id} value={c.id}>{tv(c.label)} ({c.hp} HP)</option>)}
          </Select>
          <Select aria-label={t('Fogo', 'Fire')} value={fire ?? ''} disabled={busy}
            onChange={(e) => e.target.value
              ? run(`${base}/condition`, { condition: e.target.value, on: true })
              : fire && run(`${base}/condition`, { condition: fire, on: false })}>
            <option value="">{t('Sem fogo', 'No fire')}</option>
            {rules.rules.environment.value.filter((x) => x.id.startsWith('fire')).map((x) => <option key={x.id} value={`OnFire:${x.id}`}>{tv(x.label)}</option>)}
          </Select>
        </div>
        <div className={styles.row}>
          <Select aria-label={t('Dano de ambiente', 'Environmental damage')} value={environment} onChange={(e) => setEnvironment(e.target.value)}>
            {rules.rules.environment.value.map((x) => <option key={x.id} value={x.id}>{tv(x.label)} ({tv(x.damage)})</option>)}
          </Select>
          <Button disabled={busy} onClick={() => run(`${base}/environment`, { environmentId: environment })}>{t('Aplicar ambiente', 'Apply environment')}</Button>
        </div>
        <span className={styles.source}>{tv(rules.rules.environment.value.find((x) => x.id === environment)?.note)} ({rules.rules.environment.source})</span>
      </div>

      <div className={styles.row}>
        <NumberInput aria-label={t('Iniciativa', 'Initiative')} min={-20} max={60} allowEmpty value={initiative} onChange={setInitiative} className={styles.narrow} />
        <Button disabled={busy || initiative == null} onClick={() => run(`${base}/initiative`, { initiative })}>{t('Definir iniciativa', 'Set initiative')}</Button>
        <Button disabled={busy} onClick={() => run(`${base}/active`)}>{t('Passar o turno para', 'Pass the turn to')}</Button>
        <Button variant="ghost" icon={<Trash2 size={14} />} disabled={busy}
          onClick={() => window.confirm(t(`Tirar ${f.name} do combate?`, `Take ${f.name} out of the fight?`)) && run(`${base}/remove`)}>{t('Remover', 'Remove')}</Button>
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
      <h2 className={styles.panelTitle}>{t('Coberturas', 'Cover')}</h2>
      <p className={styles.source}>{tv(rules.rules.coverRules.value)} ({rules.rules.coverRules.source})</p>
      <div className={styles.row}>
        <Select aria-label={t('Exemplo de cobertura', 'Cover example')} value={example} onChange={(e) => setExample(Number(e.target.value))}>
          {examples.map((x, i) => <option key={x.label} value={i}>{tv(x.label)} ({x.hp} HP)</option>)}
        </Select>
        <Button icon={<Shield size={14} />} disabled={busy || pick.hp <= 0}
          onClick={() => run('covers', { label: pick.label, material: pick.material, thick: pick.thick, hp: pick.hp })}>{t('Adicionar', 'Add')}</Button>
      </div>
      <ul className={styles.covers}>
        {detail.covers.map((c) => (
          <li key={c.id} className={clsx(c.hp <= 0 && styles.dead)}>
            <span className={styles.rowBetween}>
              <span><strong>{tv(c.label)}</strong> <span className={styles.source}>{c.hp}/{c.maxHp} HP</span></span>
              <IconButton label={t(`Remover ${tv(c.label)}`, `Remove ${tv(c.label)}`)} onClick={() => run(`covers/${c.id}/remove`)} disabled={busy}><Trash2 size={13} /></IconButton>
            </span>
            <span className={styles.hpBar} aria-hidden><span style={{ width: `${(c.hp / c.maxHp) * 100}%` }} /></span>
            <span className={styles.muted}>
              {t('Atrás: ', 'Behind: ')}{detail.queue.filter((f) => f.coverId === c.id).map((f) => tv(f.name)).join(', ') || t('ninguém', 'nobody')}
            </span>
            {c.hp > 0 && (
              <span className={styles.row}>
                <NumberInput aria-label={t('Dano na cobertura', 'Damage to the cover')} min={0} max={999} allowEmpty value={damage[c.id] ?? null}
                  onChange={(v) => setDamage({ ...damage, [c.id]: v })} className={styles.narrow} />
                <Button disabled={busy || !damage[c.id]} onClick={() => run(`covers/${c.id}/damage`, { amount: damage[c.id] })}>{t('Dano', 'Damage')}</Button>
              </span>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
