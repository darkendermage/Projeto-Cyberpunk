import { useMemo, useState } from 'react';
import { Brain, Clover, History, Package, Plus, Search, SlidersHorizontal, X } from 'lucide-react';
import { problemMessage, useCharacterCommand, useCharacterEvents } from '../../api/characters';
import type { CharacterDetail, CharacterRules, ModifierTarget, StatModifier } from '../../api/characterTypes';
import { Button, IconButton, Segmented, Select } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import {
  filterSkills, formatDate, groupSkills, ITEM_STATE_LABEL, signed, SKILL_FILTERS, skillLabel, STAT_LABEL, type SkillFilter,
} from './characterLabels';
import { CyberEffectsPanel, Portrait, RoleAbilityButton, SheetPanel, StateTag } from './CharacterUi';
import { ArmorPanel, HealthPanel, InjuriesPanel, type Run, WeaponsPanel } from './CombatPanels';
import { ProgressionPanel } from './ProgressionPanel';
import type { SheetEditor } from './useSheetEditor';
import styles from './Characters.module.css';

interface Props { detail: CharacterDetail; rules: CharacterRules; editor: SheetEditor }

export function SheetView({ detail, rules, editor }: Props) {
  const toast = useToast();
  const command = useCharacterCommand(detail.id);
  const locked = !detail.sheet.creation.finalized;

  const run: Run = async (path, body = {}, success) => {
    await editor.flush();
    try {
      const result = await command.mutateAsync({ path, body });
      result.notices.forEach((n) => toast.notify(n, 'info'));
      if (success) toast.notify(success);
      return result;
    } catch (error) {
      toast.notify(problemMessage(error), 'error');
      return null;
    }
  };
  const props = { detail, rules, run, busy: command.isPending, locked };

  return (
    <div className={styles.sheet}>
      <IdentityColumn detail={detail} rules={rules} editor={editor} run={run} busy={command.isPending} />
      <StatColumn detail={detail} />
      <div className={styles.column}>
        <SkillsPanel detail={detail} />
        <ProgressionPanel detail={detail} rules={rules} run={run} busy={command.isPending} />
        <ModifiersPanel detail={detail} editor={editor} />
      </div>
      <div className={styles.sheetRight}>
        <HealthPanel {...props} />
        <ArmorPanel {...props} />
        <WeaponsPanel {...props} />
        <InjuriesPanel {...props} />
        <InventoryPanel {...props} />
        <HistoryPanel id={detail.id} />
      </div>
    </div>
  );
}

// ------------------------------------------------------------ identity, Role, Humanity, LUCK

function IdentityColumn({ detail, rules, editor, run, busy }: { detail: CharacterDetail; rules: CharacterRules; editor: SheetEditor; run: Run; busy: boolean }) {
  const id = editor.draft.identity;
  const r = detail.report;
  const role = r.roles[0];
  const hum = r.humanity;
  const humPct = hum.max > 0 ? Math.max(0, (hum.current / hum.max) * 100) : 0;
  const set = (patch: Partial<typeof id>) => editor.update((s) => ({ ...s, identity: { ...s.identity, ...patch } }));

  return (
    <div className={styles.column}>
      <Portrait detail={detail} />
      <section className={styles.panel}>
        <label className={styles.idField}>
          <span className={styles.idLabel}>Nome</span>
          <input className={styles.input} value={id.name} onChange={(e) => set({ name: e.target.value })} aria-label="Nome" />
        </label>
        <label className={styles.idField}>
          <span className={styles.idLabel}>Handle</span>
          <input className={styles.input} value={id.handle ?? ''} onChange={(e) => set({ handle: e.target.value })} aria-label="Handle" />
        </label>
        {r.roles.map((x) => (
          <div key={x.role} className={styles.idField}>
            <span className={styles.idLabel}>{x.current ? 'Role' : 'Role anterior'}</span>
            <div className={styles.row}>
              <span className={styles.idValue}>{x.role}</span>
              <span className={styles.grow} />
              <span className={styles.abilityRank} title={`Rank de ${x.ability}`}>{x.rank}</span>
            </div>
            <span className={styles.small}>
              Habilidade de Role: <RoleAbilityButton rules={rules} role={x.role} /> <span className={styles.source}>CP:R p.{x.abilityPage}</span>
            </span>
          </div>
        ))}
        {!role && <span className={styles.muted}>Sem Role.</span>}
        <dl className={styles.kv}>
          <dt>Idioma de origem</dt><dd>{id.nativeLanguage ?? '—'}</dd>
          <dt>Bairro</dt><dd>{id.home}</dd>
          <dt>Reputação</dt><dd>{id.reputation}</dd>
          <dt>Saldo</dt><dd>{formatEb(detail.sheet.wallet.balance)}</dd>
        </dl>
      </section>

      <SheetPanel title="Humanidade" icon={<Brain size={16} />}>
        <div className={styles.bigNumbers}>
          <strong>{hum.current}</strong><span className={styles.muted}>/ {hum.max}</span>
          <span className={styles.grow} />
          {hum.cyberpsychosis && <StateTag tone="bad">Cyberpsicose</StateTag>}
        </div>
        <div className={styles.meter} aria-hidden><div className={styles.meterFill} style={{ width: `${humPct}%` }} /></div>
        <dl className={styles.kv}>
          <dt>EMP efetiva</dt><dd>{hum.empEffective} <span className={styles.muted}>(base {hum.empBase})</span></dd>
          <dt>Perdida</dt><dd>{hum.lost}</dd>
          <dt>Recuperada</dt><dd>{hum.recovered}</dd>
          <dt title="Máximo que a terapia recupera com o cyberware atual (CP:R p.230)">Teto da terapia</dt><dd>{hum.therapyCap}</dd>
        </dl>
      </SheetPanel>

      <CyberEffectsPanel detail={detail} />

      <SheetPanel title="LUCK" icon={<Clover size={16} />}>
        <div className={styles.row}>
          <span className={styles.bigNumbers}><strong>{r.luckAvailable}</strong><span className={styles.muted}>/ {r.luck}</span></span>
          <span className={styles.grow} />
          <Button disabled={busy || r.luckAvailable === 0} onClick={() => run('luck', { spent: detail.sheet.combat.luckSpent + 1 })}>Gastar 1</Button>
          <Button variant="ghost" disabled={busy || detail.sheet.combat.luckSpent === 0} onClick={() => run('luck', { spent: 0 })}>Restaurar</Button>
        </div>
      </SheetPanel>
    </div>
  );
}

// ------------------------------------------------------------ STATs

function StatColumn({ detail }: { detail: CharacterDetail }) {
  return (
    <div className={`${styles.column} ${styles.statColumn}`} role="list" aria-label="Atributos">
      {detail.report.stats.map((s) => {
        const tip = [`${STAT_LABEL[s.key] ?? s.key}: base ${s.base}`, ...s.parts.map((p) => `${p.label}: ${signed(p.value)}`)].join('\n');
        return (
          <div key={s.key} className={styles.stat} role="listitem" title={tip}>
            <span className={styles.statKey}>{s.key}</span>
            <span className={styles.statValue} data-changed={s.effective !== s.base}>{s.effective}</span>
            {s.effective !== s.base && <span className={styles.statBase}>base {s.base}</span>}
          </div>
        );
      })}
    </div>
  );
}

// ------------------------------------------------------------ skills

function SkillsPanel({ detail }: { detail: CharacterDetail }) {
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState<SkillFilter>('all');
  const groups = useMemo(() => groupSkills(filterSkills(detail.report.skills, query, filter)), [detail.report.skills, query, filter]);
  const penalty = detail.report.actionPenalty;

  return (
    <SheetPanel title="Perícias" actions={penalty !== 0 && (
      <span className={`${styles.small} ${styles.amber}`} title={detail.report.actionPenalties.map((p) => `${p.label}: ${signed(p.value)}`).join('\n')}>
        {signed(penalty)} em todas as Ações
      </span>
    )}>
      <div className={styles.skillToolbar}>
        <label className={styles.search}>
          <Search size={15} aria-hidden />
          <span className="visually-hidden">Buscar perícia</span>
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Buscar perícia (PT ou EN)…" />
        </label>
        <Segmented ariaLabel="Filtro de perícias" size="sm" value={filter} onChange={setFilter} options={SKILL_FILTERS} />
      </div>
      {groups.length === 0 && <p className={styles.muted}>Nenhuma perícia com esse filtro.</p>}
      <div className={styles.skillGrid}>
        {groups.map((g) => (
          <div key={g.key} className={styles.skillGroup}>
            <table className={styles.table}>
              <caption>{g.label}</caption>
              <thead>
                <tr><th>Perícia</th><th className={styles.num}>LVL</th><th className={styles.num}>MOD</th><th className={styles.num}>BASE</th></tr>
              </thead>
              <tbody>
                {g.lines.map((l) => (
                  <tr key={l.key} className={l.level === 0 ? styles.dim : undefined}>
                    <td title={`${l.name}${l.specialization ? ` (${l.specialization})` : ''} · CP:R p.${l.page}`}>
                      {skillLabel(l)} <span className={styles.muted}>({l.stat})</span>
                      {l.doubleCost && <span className={styles.tag} title="Custa o dobro para subir">x2</span>}
                      {l.roleSkill && <span className={`${styles.tag} ${styles.tagRole}`} title="Perícia da Role">Role</span>}
                      {l.situational.length > 0 && (
                        <span className={`${styles.tag} ${styles.tagCyber}`} title={`Só em certas situações (não está na BASE):\n${l.situational.join('\n')}`}>
                          situacional
                        </span>
                      )}
                    </td>
                    <td className={styles.num}>{l.level || '—'}</td>
                    <td className={styles.num} title={[
                      ...l.modifierParts.map((p) => `${p.label}: ${signed(p.value)}`),
                      ...(l.actionPenalty ? [`Todas as Ações: ${signed(l.actionPenalty)}`] : []),
                    ].join('\n') || undefined}>
                      {l.modifier + l.actionPenalty ? signed(l.modifier + l.actionPenalty) : '—'}
                    </td>
                    <td className={styles.total} title={`${l.stat} ${l.statValue} + nível ${l.level}${l.modifier ? ` ${signed(l.modifier)}` : ''}`}>{l.total}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
      </div>
    </SheetPanel>
  );
}

// ------------------------------------------------------------ temporary modifiers

function ModifiersPanel({ detail, editor }: { detail: CharacterDetail; editor: SheetEditor }) {
  const [target, setTarget] = useState<ModifierTarget>('Stat');
  const [key, setKey] = useState('REF');
  const [value, setValue] = useState('1');
  const [source, setSource] = useState('');
  const mods = editor.draft.modifiers;
  const skills = detail.report.skills.filter((s) => s.level > 0 || s.basic);

  const add = () => {
    const n = Number(value);
    if (!n || !source.trim()) return;
    const mod: StatModifier = { id: `mod${Date.now()}`, target, key: target === 'AllActions' ? '' : key, value: n, source: source.trim(), active: true };
    editor.update((s) => ({ ...s, modifiers: [...s.modifiers, mod] }));
    setSource('');
  };
  const update = (id: string, patch: Partial<StatModifier> | null) => editor.update((s) => ({
    ...s,
    modifiers: patch === null ? s.modifiers.filter((m) => m.id !== id) : s.modifiers.map((m) => (m.id === id ? { ...m, ...patch } : m)),
  }));

  return (
    <SheetPanel title="Modificadores temporários" icon={<SlidersHorizontal size={16} />}>
      <p className={styles.small}>Drogas, equipamento e decisões do Mestre. Mudam o valor efetivo; o valor-base fica intacto.</p>
      {mods.length > 0 && (
        <ul className={styles.list}>
          {mods.map((m) => (
            <li key={m.id} className={styles.listItem} data-tone={m.active ? 'warn' : undefined}>
              <span className={styles.grow}>
                <strong className={styles.mono}>{signed(m.value)}</strong>{' '}
                {m.target === 'AllActions' ? 'em todas as Ações' : m.key} — {m.source}
              </span>
              <Button variant="ghost" onClick={() => update(m.id, { active: !m.active })}>{m.active ? 'Desativar' : 'Ativar'}</Button>
              <IconButton label="Remover modificador" onClick={() => update(m.id, null)}><X size={14} /></IconButton>
            </li>
          ))}
        </ul>
      )}
      <div className={styles.row}>
        <Select value={target} onChange={(e) => { setTarget(e.target.value as ModifierTarget); setKey(e.target.value === 'Skill' ? skills[0]?.key ?? '' : 'REF'); }} aria-label="Alvo">
          <option value="Stat">STAT</option>
          <option value="Skill">Perícia</option>
          <option value="AllActions">Todas as Ações</option>
        </Select>
        {target === 'Stat' && (
          <Select value={key} onChange={(e) => setKey(e.target.value)} aria-label="STAT">
            {detail.report.stats.map((s) => <option key={s.key} value={s.key}>{s.key}</option>)}
          </Select>
        )}
        {target === 'Skill' && (
          <Select value={key} onChange={(e) => setKey(e.target.value)} aria-label="Perícia">
            {skills.map((s) => <option key={s.key} value={s.key}>{skillLabel(s)}</option>)}
          </Select>
        )}
        <input className={`${styles.input} ${styles.inputSmall}`} type="number" value={value} onChange={(e) => setValue(e.target.value)} aria-label="Valor" />
        <input className={styles.input} style={{ maxWidth: 200 }} value={source} placeholder="Origem (ex.: Synthcoke)" onChange={(e) => setSource(e.target.value)} />
        <IconButton label="Adicionar modificador" disabled={!Number(value) || !source.trim()} onClick={add}><Plus size={14} /></IconButton>
      </div>
    </SheetPanel>
  );
}

// ------------------------------------------------------------ inventory

function InventoryPanel({ detail, run, busy }: { detail: CharacterDetail; run: Run; busy: boolean; rules: CharacterRules; locked: boolean }) {
  const items = detail.report.inventory;
  return (
    <SheetPanel title="Equipamento" icon={<Package size={16} />}>
      {items.length === 0 ? <p className={styles.muted}>Inventário vazio.</p> : (
        <table className={styles.table}>
          <thead><tr><th>Item</th><th className={styles.num}>Qtd</th><th>Estado</th></tr></thead>
          <tbody>
            {items.map((i) => {
              const armor = i.sp != null;
              return (
                <tr key={i.instanceId}>
                  <td>
                    {i.name}
                    {i.caliber && <span className={styles.muted}> ({i.caliber})</span>}
                    {i.fashion && <span className={styles.tag}>moda</span>}
                  </td>
                  <td className={styles.num}>{i.rounds ?? i.quantity}</td>
                  <td>
                    <Select style={{ minWidth: 118 }} value={i.state === 'Equipped' && i.worn ? `Equipped:${i.worn}` : i.state} disabled={busy} aria-label={`Estado de ${i.name}`}
                      onChange={(e) => {
                        const [state, worn] = e.target.value.split(':');
                        void run('equip', { instanceId: i.instanceId, state, worn: worn ?? null });
                      }}>
                      {armor ? (
                        <>
                          <option value="Equipped:Body">No corpo</option>
                          <option value="Equipped:Head">Na cabeça</option>
                        </>
                      ) : <option value="Equipped">{ITEM_STATE_LABEL.Equipped}</option>}
                      <option value="Carried">{ITEM_STATE_LABEL.Carried}</option>
                      <option value="Stored">{ITEM_STATE_LABEL.Stored}</option>
                    </Select>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </SheetPanel>
  );
}

// ------------------------------------------------------------ history

const KIND_LABEL: Record<string, string> = { Ledger: 'Dinheiro', Humanity: 'Humanidade', Combat: 'Combate', Injury: 'Lesão', Progression: 'Progressão', Edit: 'Edição' };

function HistoryPanel({ id }: { id: number }) {
  const [open, setOpen] = useState(false);
  const events = useCharacterEvents(id, open);
  return (
    <SheetPanel title="Histórico" icon={<History size={16} />}
      actions={<Button variant="ghost" onClick={() => setOpen(!open)}>{open ? 'Ocultar' : 'Mostrar'}</Button>}>
      {open && (events.data?.length ? (
        <ul className={styles.list}>
          {events.data.slice(0, 60).map((e) => (
            <li key={e.id} className={styles.listItem}>
              <div className={styles.grow}>
                <div className={styles.small}>{e.description}</div>
                <div className={styles.source}>
                  {KIND_LABEL[e.kind]} · {formatDate(e.createdAt)}
                  {e.before != null && e.after != null && ` · ${e.before} → ${e.after}`}
                  {e.isManual && ' · manual'}
                </div>
              </div>
              {e.amount != null && e.amount !== 0 && <span className={`${styles.mono} ${e.amount > 0 ? styles.green : styles.red}`}>{signed(e.amount)}</span>}
            </li>
          ))}
        </ul>
      ) : <p className={styles.muted}>{events.isLoading ? 'Carregando…' : 'Sem registros.'}</p>)}
    </SheetPanel>
  );
}
