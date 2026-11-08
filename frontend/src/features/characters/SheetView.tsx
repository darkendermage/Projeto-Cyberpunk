import { useMemo, useState } from 'react';
import { Brain, Clover, Eye, History, Minus, Plus, RotateCcw, Search, SlidersHorizontal, X } from 'lucide-react';
import { problemMessage, useCharacterCommand, useCharacterEvents } from '../../api/characters';
import type { CharacterDetail, CharacterRules, ModifierTarget, StatModifier } from '../../api/characterTypes';
import { Button, IconButton, Modal, Segmented, Select } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import {
  filterSkills, formatDate, groupSkills, signed, SKILL_FILTERS, skillLabel, STAT_LABEL, type SkillFilter,
} from './characterLabels';
import { CyberEffectsPanel, Portrait, RoleAbilityButton, SheetPanel, StateTag } from './CharacterUi';
import { ArmorPanel, HealthPanel, InjuriesPanel, type Run, WeaponsPanel } from './CombatPanels';
import { RoleSplitDialog } from './RoleSplitDialog';
import { splitTotal } from './roleSplit';
import { ProgressionPanel } from './ProgressionPanel';
import { paginate } from './gearLayout';
import { Pager } from './Pager';
import type { SheetEditor } from './useSheetEditor';
import styles from './Characters.module.css';
import { t, tv } from '../../i18n';

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
      <StatColumn detail={detail} run={run} busy={command.isPending} />
      <div className={styles.column}>
        <SkillsPanel detail={detail} />
      </div>
      <div className={styles.sheetRight}>
        <HealthPanel {...props} />
        <ModifiersPanel detail={detail} editor={editor} />
        <ArmorPanel {...props} />
        <WeaponsPanel {...props} />
        <InjuriesPanel {...props} />
        <ProgressionPanel detail={detail} rules={rules} run={run} busy={command.isPending} />
        <HistoryPanel id={detail.id} run={run} busy={command.isPending} />
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
      <Portrait detail={detail} onFocus={(portraitFocus) => set({ portraitFocus })} />
      <WalletBlock detail={detail} />
      <section className={styles.panel}>
        <label className={styles.idField}>
          <span className={styles.idLabel}>{t('Nome', 'Name')}</span>
          <input className={styles.input} value={id.name} onChange={(e) => set({ name: e.target.value })} aria-label={t('Nome', 'Name')} />
        </label>
        <label className={styles.idField}>
          <span className={styles.idLabel}>Handle</span>
          <input className={styles.input} value={id.handle ?? ''} onChange={(e) => set({ handle: e.target.value })} aria-label="Handle" />
        </label>
        {r.roles.map((x) => (
          <div key={x.role} className={styles.idField}>
            <span className={styles.idLabel}>{x.current ? 'Role' : t('Role anterior', 'Previous Role')}</span>
            <div className={styles.row}>
              <span className={styles.idValue}>{x.role}</span>
              <span className={styles.grow} />
              <span className={styles.abilityRank} title={t(`Rank de ${x.ability}`, `${x.ability} Rank`)}>{x.rank}</span>
            </div>
            <span className={styles.small}>
              {t('Habilidade de Role: ', 'Role Ability: ')}<RoleAbilityButton rules={rules} role={x.role} /> <span className={styles.source}>CP:R p.{x.abilityPage}</span>
              <RoleSplitButton detail={detail} rules={rules} role={x.role} rank={x.rank} run={run} busy={busy} />
            </span>
          </div>
        ))}
        {!role && <span className={styles.muted}>{t('Sem Role.', 'No Role.')}</span>}
        <dl className={styles.kv}>
          <dt>{t('Idioma de origem', 'Native language')}</dt><dd>{id.nativeLanguage ?? '—'}</dd>
          <dt>{t('Bairro', 'Neighborhood')}</dt><dd>{id.home}</dd>
          <dt>{t('Reputação', 'Reputation')}</dt><dd>{id.reputation}</dd>
        </dl>
      </section>

      <SheetPanel title={t('Humanidade', 'Humanity')} icon={<Brain size={16} />}>
        <div className={styles.bigNumbers}>
          <strong>{hum.current}</strong><span className={styles.muted}>/ {hum.max}</span>
          <span className={styles.grow} />
          {hum.cyberpsychosis && <StateTag tone="bad">{t('Cyberpsicose', 'Cyberpsychosis')}</StateTag>}
        </div>
        <div className={styles.meter} aria-hidden><div className={styles.meterFill} style={{ width: `${humPct}%` }} /></div>
        <dl className={styles.kv}>
          <dt>{t('EMP efetiva', 'Effective EMP')}</dt><dd>{hum.empEffective} <span className={styles.muted}>(base {hum.empBase})</span></dd>
          <dt>{t('Perdida', 'Lost')}</dt><dd>{hum.lost}</dd>
          <dt>{t('Recuperada', 'Recovered')}</dt><dd>{hum.recovered}</dd>
          <dt title={t('Máximo que a terapia recupera com o cyberware atual (CP:R p.230)', 'Most that therapy recovers with the current cyberware (CP:R p.230)')}>{t('Teto da terapia', 'Therapy cap')}</dt><dd>{hum.therapyCap}</dd>
        </dl>
      </SheetPanel>

      <CyberEffectsPanel detail={detail} />

    </div>
  );
}

/** "Divide" next to a Role Ability whose Rank is divided (Tech specialties, Solo Combat Awareness), with the current split. */
function RoleSplitButton({ detail, rules, role, rank, run, busy }: { detail: CharacterDetail; rules: CharacterRules; role: string; rank: number; run: Run; busy: boolean }) {
  const [open, setOpen] = useState(false);
  const rule = rules.roles.find((r) => r.id === role)?.abilityInfo.split;
  if (!rule) return null;
  const split = detail.sheet.roles.find((r) => r.role === role)?.split ?? {};
  const used = Object.values(split).reduce((a, b) => a + b, 0);
  const shown = rule.keys.filter((k) => (split[k.id] ?? 0) > 0).map((k) => `${t(k.labelPt ?? k.label, k.label)} ${split[k.id]}`).join(' · ');
  return (
    <>
      {' · '}
      <button type="button" className={styles.linkButton} onClick={() => setOpen(true)} title={shown || undefined}>
        {t(`Distribuir (${used}/${splitTotal(rule, rank)})`, `Divide (${used}/${splitTotal(rule, rank)})`)}
      </button>
      {shown && <div className={styles.source}>{shown}</div>}
      {open && (
        <RoleSplitDialog open onClose={() => setOpen(false)} role={role} rank={rank} rule={rule} split={split} busy={busy}
          onSave={async (next) => { if (await run('role-split', { role, split: next }, t('Distribuição salva.', 'Split saved.'))) setOpen(false); }} />
      )}
    </>
  );
}

/** Money and Improvement Points side by side, at the top of the Character tab. */
function WalletBlock({ detail }: { detail: CharacterDetail }) {
  const { ip, ipEarned } = detail.sheet.progress;
  return (
    <section className={`${styles.panel} ${styles.walletBlock}`} aria-label={t('Carteira', 'Wallet')}>
      <div className={styles.walletItem}>
        <span className={styles.idLabel}>{t('Saldo', 'Balance')}</span>
        <strong className={styles.walletValue}>{formatEb(detail.sheet.wallet.balance)}</strong>
      </div>
      <div className={styles.walletItem}>
        <span className={styles.idLabel}>IP</span>
        <strong className={`${styles.walletValue} ${styles.cyan}`}>{ip}</strong>
        <span className={styles.muted}>{t(`de ${ipEarned} ganhos`, `of ${ipEarned} earned`)}</span>
      </div>
    </section>
  );
}

// ------------------------------------------------------------ STATs

function StatColumn({ detail, run, busy }: { detail: CharacterDetail; run: Run; busy: boolean }) {
  const r = detail.report;
  return (
    <div className={`${styles.column} ${styles.statColumn}`} role="list" aria-label={t('Atributos', 'STATs')}>
      {detail.report.stats.map((s) => {
        const tip = [`${STAT_LABEL[s.key] ?? s.key}: base ${s.base}`, ...s.parts.map((p) => `${tv(p.label)}: ${signed(p.value)}`)].join('\n');
        return (
          <div key={s.key} className={styles.stat} role="listitem" title={tip}>
            <span className={styles.statKey}>{s.key}</span>
            <span className={styles.statValue} data-changed={s.effective > s.base ? 'up' : s.effective < s.base ? 'down' : undefined}>{s.effective}</span>
            {s.effective !== s.base && <span className={styles.statBase}>base {s.base}</span>}
          </div>
        );
      })}
      {/* LUCK to spend this session (CP:R p.130), compact under the STATs. */}
      <div className={`${styles.stat} ${styles.luckTile}`} role="listitem" title={t('LUCK disponível nesta sessão; gasta-se em Checks (CP:R p.130).', 'LUCK left this session; spent on Checks (CP:R p.130).')}>
        <span className={styles.statKey}><Clover size={11} aria-hidden /> {t('Sorte', 'Luck')}</span>
        <span className={styles.luckValue}><strong>{r.luckAvailable}</strong><span className={styles.muted}>/{r.luck}</span></span>
        <span className={styles.luckButtons}>
          <IconButton label={t('Gastar 1 de LUCK', 'Spend 1 LUCK')} disabled={busy || r.luckAvailable === 0} onClick={() => run('luck', { spent: detail.sheet.combat.luckSpent + 1 })}><Minus size={13} /></IconButton>
          <IconButton label={t('Restaurar a LUCK', 'Restore LUCK')} disabled={busy || detail.sheet.combat.luckSpent === 0} onClick={() => run('luck', { spent: 0 })}><RotateCcw size={13} /></IconButton>
        </span>
      </div>
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
    <SheetPanel title={t('Perícias', 'Skills')} actions={penalty !== 0 && (
      <span className={`${styles.small} ${styles.amber}`} title={detail.report.actionPenalties.map((p) => `${p.label}: ${signed(p.value)}`).join('\n')}>
        {signed(penalty)} {t('em todas as Ações', 'to all Actions')}
      </span>
    )}>
      <div className={styles.skillToolbar}>
        <label className={styles.search}>
          <Search size={15} aria-hidden />
          <span className="visually-hidden">{t('Buscar perícia', 'Search skill')}</span>
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t('Buscar perícia (PT ou EN)…', 'Search skill (EN or PT)…')} />
        </label>
        <Segmented ariaLabel={t('Filtro de perícias', 'Skill filter')} size="sm" value={filter} onChange={setFilter} options={SKILL_FILTERS} />
      </div>
      {groups.length === 0 && <p className={styles.muted}>{t('Nenhuma perícia com esse filtro.', 'No skills with this filter.')}</p>}
      <div className={styles.skillGrid}>
        {groups.map((g) => (
          <div key={g.key} className={styles.skillGroup}>
            <table className={styles.table}>
              <caption>{tv(g.label)}</caption>
              <thead>
                <tr><th>{t('Perícia', 'Skill')}</th><th className={styles.num}>LVL</th><th className={styles.num}>MOD</th><th className={styles.num}>BASE</th></tr>
              </thead>
              <tbody>
                {g.lines.map((l) => (
                  <tr key={l.key} className={l.level === 0 ? styles.dim : undefined}>
                    <td title={`${tv(l.name)}${l.specialization ? ` (${l.specialization})` : ''} · CP:R p.${l.page}`}>
                      {skillLabel(l)} <span className={styles.muted}>({l.stat})</span>
                      {l.doubleCost && <span className={styles.tag} title={t('Custa o dobro para subir', 'Costs double to raise')}>x2</span>}
                      {l.roleSkill && <span className={`${styles.tag} ${styles.tagRole}`} title={t('Perícia da Role', 'Role skill')}>Role</span>}
                      {l.situational.length > 0 && (
                        <span className={`${styles.tag} ${styles.tagCyber}`} title={t(`Só em certas situações (não está na BASE):\n${l.situational.join('\n')}`, `Only in certain situations (not in BASE):\n${l.situational.join('\n')}`)}>
                          {t('situacional', 'situational')}
                        </span>
                      )}
                    </td>
                    <td className={styles.num}>{l.level || '—'}</td>
                    <td className={styles.num} title={[
                      ...l.modifierParts.map((p) => `${tv(p.label)}: ${signed(p.value)}`),
                      ...(l.actionPenalty ? [t(`Todas as Ações: ${signed(l.actionPenalty)}`, `All Actions: ${signed(l.actionPenalty)}`)] : []),
                    ].join('\n') || undefined}>
                      {l.modifier + l.actionPenalty ? signed(l.modifier + l.actionPenalty) : '—'}
                    </td>
                    <td className={styles.total} title={t(`${l.stat} ${l.statValue} + nível ${l.level}${l.modifier ? ` ${signed(l.modifier)}` : ''}`, `${l.stat} ${l.statValue} + level ${l.level}${l.modifier ? ` ${signed(l.modifier)}` : ''}`)}>{l.total}</td>
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

/** Temporary modifiers, compact: the active ones at a glance, the list and the form in a window. */
function ModifiersPanel({ detail, editor }: { detail: CharacterDetail; editor: SheetEditor }) {
  const [open, setOpen] = useState(false);
  const mods = editor.draft.modifiers;
  const active = mods.filter((m) => m.active);
  return (
    <SheetPanel title={t('Modificadores temporários', 'Temporary modifiers')} icon={<SlidersHorizontal size={16} />}
      actions={<Button variant="ghost" onClick={() => setOpen(true)}>{t('Gerenciar', 'Manage')}</Button>}>
      {active.length === 0
        ? <span className={`${styles.small} ${styles.muted}`}>{mods.length === 0 ? t('Nenhum modificador.', 'No modifiers.') : t(`${mods.length} desativado(s).`, `${mods.length} turned off.`)}</span>
        : (
          <div className={styles.row} style={{ gap: 6 }}>
            {active.map((m) => (
              <span key={m.id} className={styles.tag} title={m.source}>
                <strong className={styles.mono}>{signed(m.value)}</strong> {m.target === 'AllActions' ? t('todas as Ações', 'all Actions') : m.key}
              </span>
            ))}
          </div>
        )}
      <Modal open={open} onClose={() => setOpen(false)} title={t('Modificadores temporários', 'Temporary modifiers')} wide>
        <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
          <ModifiersEditor detail={detail} editor={editor} />
        </div>
      </Modal>
    </SheetPanel>
  );
}

function ModifiersEditor({ detail, editor }: { detail: CharacterDetail; editor: SheetEditor }) {
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
    <>
      <p className={styles.small}>{t('Drogas, equipamento e decisões do Mestre. Mudam o valor efetivo; o valor-base fica intacto.', 'Drugs, gear and GM rulings. They change the effective value; the base value stays untouched.')}</p>
      {mods.length > 0 && (
        <ul className={styles.list}>
          {mods.map((m) => (
            <li key={m.id} className={styles.listItem} data-tone={m.active ? 'warn' : undefined}>
              <span className={styles.grow}>
                <strong className={styles.mono}>{signed(m.value)}</strong>{' '}
                {m.target === 'AllActions' ? t('em todas as Ações', 'to all Actions') : m.key} — {m.source}
              </span>
              <Button variant="ghost" onClick={() => update(m.id, { active: !m.active })}>{m.active ? t('Desativar', 'Turn off') : t('Ativar', 'Turn on')}</Button>
              <IconButton label={t('Remover modificador', 'Remove modifier')} onClick={() => update(m.id, null)}><X size={14} /></IconButton>
            </li>
          ))}
        </ul>
      )}
      <div className={styles.row}>
        <Select value={target} onChange={(e) => { setTarget(e.target.value as ModifierTarget); setKey(e.target.value === 'Skill' ? skills[0]?.key ?? '' : 'REF'); }} aria-label={t('Alvo', 'Target')}>
          <option value="Stat">STAT</option>
          <option value="Skill">{t('Perícia', 'Skill')}</option>
          <option value="AllActions">{t('Todas as Ações', 'All Actions')}</option>
        </Select>
        {target === 'Stat' && (
          <Select value={key} onChange={(e) => setKey(e.target.value)} aria-label="STAT">
            {detail.report.stats.map((s) => <option key={s.key} value={s.key}>{s.key}</option>)}
          </Select>
        )}
        {target === 'Skill' && (
          <Select value={key} onChange={(e) => setKey(e.target.value)} aria-label={t('Perícia', 'Skill')}>
            {skills.map((s) => <option key={s.key} value={s.key}>{skillLabel(s)}</option>)}
          </Select>
        )}
        <input className={`${styles.input} ${styles.inputSmall}`} type="number" value={value} onChange={(e) => setValue(e.target.value)} aria-label={t('Valor', 'Value')} />
        <input className={styles.input} style={{ maxWidth: 200 }} value={source} placeholder={t('Origem (ex.: Synthcoke)', 'Source (e.g. Synthcoke)')} onChange={(e) => setSource(e.target.value)} />
        <IconButton label={t('Adicionar modificador', 'Add modifier')} disabled={!Number(value) || !source.trim()} onClick={add}><Plus size={14} /></IconButton>
      </div>
    </>
  );
}

// ------------------------------------------------------------ history

const KIND_LABEL: Record<string, string> = { Ledger: t('Dinheiro', 'Money'), Humanity: t('Humanidade', 'Humanity'), Combat: t('Combate', 'Combat'), Injury: t('Lesão', 'Injury'), Progression: t('Progressão', 'Progression'), Edit: t('Edição', 'Edit') };

/** The character's history; IP spends on skills and Role Ranks can be undone, latest step first. */
function HistoryPanel({ id, run, busy }: { id: number; run: Run; busy: boolean }) {
  const [open, setOpen] = useState(false);
  const [page, setPage] = useState(0);
  const events = useCharacterEvents(id, open);
  const shown = paginate(events.data ?? [], page);
  return (
    <SheetPanel title={t('Histórico', 'History')} icon={<History size={16} />}
      actions={<Button variant="ghost" icon={<Eye size={14} />} onClick={() => { setPage(0); setOpen(true); }}>{t('Visualizar', 'View')}</Button>}>
      <span className={`${styles.small} ${styles.muted}`}>{t('Dinheiro, IP, combate, lesões e edições da ficha. Gastos de IP podem ser desfeitos.', 'Money, IP, combat, injuries and sheet edits. IP spends can be undone.')}</span>
      <Modal open={open} onClose={() => setOpen(false)} title={t('Histórico', 'History')} wide>
      <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
      {events.data?.length ? (
        <>
        <ul className={styles.list}>
          {shown.rows.map((e) => (
            <li key={e.id} className={styles.listItem}>
              <div className={styles.grow}>
                <div className={styles.small}>{tv(e.description)}</div>
                <div className={styles.source}>
                  {KIND_LABEL[e.kind]} · {formatDate(e.createdAt)}
                  {e.before != null && e.after != null && ` · ${e.before} → ${e.after}`}
                  {e.isManual && ' · manual'}
                  {e.undone && <> · <strong>{t('desfeito', 'undone')}</strong></>}
                </div>
              </div>
              {e.amount != null && e.amount !== 0 && <span className={`${styles.mono} ${e.amount > 0 ? styles.green : styles.red}`}>{signed(e.amount)}</span>}
              {e.canUndo && (
                <Button variant="ghost" disabled={busy} title={t('Desfazer e devolver os IP', 'Undo and give the IP back')} onClick={() => {
                  const what = tv(e.description).replace(/\.$/, '');
                  if (window.confirm(t(`Desfazer "${what}" e devolver ${-(e.amount ?? 0)} IP?`, `Undo "${what}" and give back ${-(e.amount ?? 0)} IP?`)))
                    void run('progression/undo', { eventId: e.id }, t('Desfeito. IP devolvidos.', 'Undone. IP returned.'));
                }}>{t('Desfazer', 'Undo')}</Button>
              )}
            </li>
          ))}
        </ul>
        <Pager page={shown.page} pages={shown.pages} rows={shown.rows.length} total={events.data.length} noun={t('registros', 'entries')} onPage={setPage} />
        </>
      ) : <p className={styles.muted}>{events.isLoading ? t('Carregando…', 'Loading…') : t('Sem registros.', 'No entries.')}</p>}
      </div>
      </Modal>
    </SheetPanel>
  );
}
