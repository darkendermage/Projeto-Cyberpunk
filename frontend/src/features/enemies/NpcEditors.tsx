import { useMemo, useState, type Dispatch } from 'react';
import clsx from 'clsx';
import { Lock, LockOpen, Minus, Plus, Star } from 'lucide-react';
import type { NpcRules, NpcSheet, NpcValidationReport, SkillLine } from '../../api/types';
import { Badge, Button, Field, IconButton, NumberInput, Segmented, Select } from '../../components/ui/ui';
import { type DraftAction, skillKey, STAT_KEYS } from './npcDraft';
import styles from './EnemyGenerator.module.css';

export function LockButton({ locked, onToggle, label }: { locked: boolean; onToggle: () => void; label: string }) {
  return (
    <IconButton label={locked ? `Destravar ${label}` : `Travar ${label} (o gerador não altera)`} onClick={onToggle}
      active={locked} className={clsx(locked && styles.lockOn)}>
      {locked ? <Lock size={14} /> : <LockOpen size={14} />}
    </IconButton>
  );
}

const hasIssue = (report: NpcValidationReport | undefined, field: string) =>
  report?.issues.some((i) => i.severity === 'Error' && !i.overridden && i.field === field) ?? false;

// ---------------------------------------------------------------- identity

export function IdentityEditor({
  rules, sheet, dispatch, semi,
}: { rules: NpcRules; sheet: NpcSheet; dispatch: Dispatch<DraftAction>; semi: boolean }) {
  const set = (patch: Extract<DraftAction, { type: 'set' }>['patch']) => dispatch({ type: 'set', patch });
  return (
    <div className={styles.formGrid}>
      <Field label="Nome ou apelido" htmlFor="npc-name" hint={semi ? 'Mantido pelo gerador quando preenchido.' : undefined}>
        <input id="npc-name" value={sheet.name} maxLength={80} onChange={(e) => set({ name: e.target.value })} placeholder="Ex.: Pitbull" />
      </Field>
      <Field label="Conceito" htmlFor="npc-concept">
        <input id="npc-concept" value={sheet.concept ?? ''} maxLength={300} onChange={(e) => set({ concept: e.target.value || null })}
          placeholder="Ex.: Policial corrupto da NCPD" />
      </Field>
      <Field label="Role" htmlFor="npc-role">
        <Select id="npc-role" value={sheet.role ?? ''} onChange={(e) => set({ role: e.target.value || null, roleRank: e.target.value ? Math.max(sheet.roleRank, 1) : 0 })}>
          <option value="">Sem Role Ability</option>
          {rules.roles.map((r) => <option key={r} value={r}>{r}</option>)}
        </Select>
      </Field>
      <Field label="Rank da Role Ability" htmlFor="npc-rank">
        <NumberInput id="npc-rank" min={0} max={10} value={sheet.roleRank} disabled={!sheet.role}
          onChange={(v) => set({ roleRank: v ?? 0 })} />
      </Field>
      <Field label="Idioma nativo (Language 4 grátis)" htmlFor="npc-lang">
        <input id="npc-lang" list="npc-languages" value={sheet.nativeLanguage} onChange={(e) => set({ nativeLanguage: e.target.value })} />
      </Field>
      <Field label="Região (Local Expert 2 grátis)" htmlFor="npc-home">
        <input id="npc-home" list="npc-homes" value={sheet.home} onChange={(e) => set({ home: e.target.value })} />
      </Field>
      <datalist id="npc-languages">{rules.languages.map((l) => <option key={l} value={l} />)}</datalist>
      <datalist id="npc-homes">{rules.homes.map((h) => <option key={h} value={h} />)}</datalist>
      {semi && (
        <div className={styles.inline}>
          <LockButton locked={sheet.locks.identity} onToggle={() => dispatch({ type: 'toggleIdentityLock' })} label="Role, idioma e região" />
          <span className={styles.muted}>Travar Role, Rank, idioma e região</span>
        </div>
      )}
    </div>
  );
}

// ---------------------------------------------------------------- STATs

export function StatsEditor({
  sheet, report, dispatch, semi,
}: { sheet: NpcSheet; report?: NpcValidationReport; dispatch: Dispatch<DraftAction>; semi: boolean }) {
  const d = report?.derived;
  const locked = (k: string) => sheet.locks.stats.some((s) => s.toUpperCase() === k);
  return (
    <>
      <div className={styles.stats}>
        {STAT_KEYS.map((k) => {
          const value = sheet.stats[k] ?? 0;
          const armored = d?.effectiveStats[k];
          return (
            <div key={k} className={clsx(styles.stat, hasIssue(report, `stats.${k}`) && styles.statError)}>
              <span className={styles.statKey}>
                {k}
                {semi && <LockButton locked={locked(k)} onToggle={() => dispatch({ type: 'toggleStatLock', key: k })} label={k} />}
              </span>
              <div className={styles.stepper}>
                <button type="button" aria-label={`Diminuir ${k}`} onClick={() => dispatch({ type: 'stat', key: k, value: value - 1 })}><Minus size={12} /></button>
                <NumberInput aria-label={k} value={value} min={0} max={20}
                  onChange={(v) => dispatch({ type: 'stat', key: k, value: v ?? 0 })} />
                <button type="button" aria-label={`Aumentar ${k}`} onClick={() => dispatch({ type: 'stat', key: k, value: value + 1 })}><Plus size={12} /></button>
              </div>
              {armored !== undefined && armored !== value && (
                <span className={styles.statArmored} title="Valor com penalidade de armadura">({armored}) armadura</span>
              )}
            </div>
          );
        })}
      </div>
      {d && (
        <div className={styles.derived}>
          <Derived label="HP" value={d.hp} title="10 + 5 × média de BODY e WILL, arredondada para cima (CP:R p.79)" />
          <Derived label="Seriously Wounded" value={d.seriouslyWounded} title="Metade do HP, para cima (CP:R p.79)" />
          <Derived label="Death Save" value={d.deathSave} title="Igual a BODY (CP:R p.79)" />
          <Derived label="SP corpo / cabeça" value={`${d.spBody} / ${d.spHead}`} />
          <Derived label="Peças de cyberware" value={d.cyberwarePieces} />
          <Field label="LUCK (fora do orçamento)" htmlFor="npc-luck">
            <NumberInput id="npc-luck" min={0} max={10} value={sheet.luck} placeholder="—" allowEmpty
              onChange={(v) => dispatch({ type: 'set', patch: { luck: v } })} />
          </Field>
        </div>
      )}
    </>
  );
}

function Derived({ label, value, title }: { label: string; value: number | string; title?: string }) {
  return (
    <div className={styles.derivedItem} title={title}>
      <div className={styles.derivedLabel}>{label}</div>
      <div className={styles.derivedValue}>{value}</div>
    </div>
  );
}

// ---------------------------------------------------------------- skills

type SkillFilter = 'used' | 'all';

export function SkillsEditor({
  rules, sheet, report, dispatch, semi,
}: { rules: NpcRules; sheet: NpcSheet; report?: NpcValidationReport; dispatch: Dispatch<DraftAction>; semi: boolean }) {
  const [filter, setFilter] = useState<SkillFilter>('used');
  const [query, setQuery] = useState('');
  const [specSkill, setSpecSkill] = useState('');
  const [specText, setSpecText] = useState('');

  const lines = useMemo(() => new Map((report?.skillLines ?? []).map((l) => [l.key.toLowerCase(), l])), [report]);
  const rows = useMemo(() => {
    const keys = new Set<string>();
    for (const s of rules.skills) if (!s.specialized) keys.add(s.id);
    for (const k of Object.keys(sheet.skills)) keys.add(k);
    for (const l of report?.skillLines ?? []) keys.add(l.key);
    const info = new Map(rules.skills.map((s) => [s.id, s]));
    return [...keys]
      .map((key) => {
        const [id, spec] = key.split(':');
        const def = info.get(id.toLowerCase());
        const line: SkillLine | undefined = lines.get(key.toLowerCase());
        const level = sheet.skills[key] ?? line?.level ?? 0;
        return { key, def, spec, line, level };
      })
      .filter((r) => r.def)
      .filter((r) => filter === 'all' || r.level > 0 || sheet.keySkills.includes(r.key))
      .filter((r) => !query || `${r.def!.name} ${r.spec ?? ''}`.toLowerCase().includes(query.toLowerCase()))
      .sort((a, b) => a.def!.category.localeCompare(b.def!.category) || a.def!.name.localeCompare(b.def!.name) || (a.spec ?? '').localeCompare(b.spec ?? ''));
  }, [rules, sheet.skills, sheet.keySkills, report, lines, filter, query]);

  const specialized = rules.skills.filter((s) => s.specialized);
  const addSpecialized = () => {
    if (!specSkill || !specText.trim()) return;
    dispatch({ type: 'skill', key: skillKey(specSkill, specText), value: 1 });
    setSpecText('');
  };

  return (
    <>
      <div className={styles.skillTools}>
        <Segmented<SkillFilter> size="sm" ariaLabel="Filtrar perícias" value={filter} onChange={setFilter}
          options={[{ value: 'used', label: 'Com nível' }, { value: 'all', label: 'Todas' }]} />
        <input type="search" placeholder="Buscar perícia" value={query} onChange={(e) => setQuery(e.target.value)} aria-label="Buscar perícia" />
        <Select value={specSkill} onChange={(e) => setSpecSkill(e.target.value)} aria-label="Perícia com especialização">
          <option value="">Adicionar especialização…</option>
          {specialized.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </Select>
        <input type="text" placeholder="Idioma, região, estilo…" value={specText} onChange={(e) => setSpecText(e.target.value)}
          aria-label="Especialização" onKeyDown={(e) => e.key === 'Enter' && addSpecialized()} />
        <Button onClick={addSpecialized} disabled={!specSkill || !specText.trim()} icon={<Plus size={14} />}>Adicionar</Button>
      </div>
      <p className={styles.muted}>
        Nível é o valor da perícia; Base = STAT (com penalidade de armadura) + nível. Os níveis gratuitos não custam pontos ({rules.freeSkillsSource}).
        ★ marca as perícias importantes, cujas bases são comparadas com a faixa sugerida.
      </p>
      <div className={styles.tableWrap}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th aria-label="Importante">★</th>
              <th>Perícia</th>
              <th>STAT</th>
              <th className={styles.num}>Grátis</th>
              <th>Nível</th>
              <th className={styles.num}>Custo</th>
              <th className={styles.num}>Base</th>
              {semi && <th aria-label="Trava" />}
            </tr>
          </thead>
          <tbody>
            {rows.map(({ key, def, spec, line, level }) => {
              const isKey = sheet.keySkills.some((k) => k.toLowerCase() === key.toLowerCase());
              const locked = sheet.locks.skills.some((k) => k.toLowerCase() === key.toLowerCase());
              return (
                <tr key={key} className={clsx(hasIssue(report, `skills.${key}`) && styles.rowError)}>
                  <td>
                    <IconButton label={isKey ? 'Desmarcar como importante' : 'Marcar como importante'}
                      onClick={() => dispatch({ type: 'toggleKeySkill', key })}>
                      <Star size={14} className={isKey ? styles.starOn : styles.star} fill={isKey ? 'currentColor' : 'none'} />
                    </IconButton>
                  </td>
                  <td>
                    <span className={styles.itemName}>{def!.name}{spec ? ` (${spec})` : ''}</span>
                    {def!.doubleCost && <> <Badge tone="amber" title="Custa 2 pontos por nível (CP:R p.81)">x2</Badge></>}
                    {def!.combat && <> <Badge tone="red">combate</Badge></>}
                  </td>
                  <td>{def!.stat}</td>
                  <td className={styles.num}>{line?.freeLevel || '—'}</td>
                  <td>
                    <NumberInput min={0} max={10} value={level} aria-label={`Nível de ${def!.name}`} allowEmpty
                      title="Vazio volta ao nível padrão (básicas: 2)"
                      onChange={(v) => dispatch({ type: 'skill', key, value: v })} />
                  </td>
                  <td className={styles.num}>{line?.cost ?? 0}</td>
                  <td className={styles.num}>{line?.base ?? '—'}</td>
                  {semi && (
                    <td><LockButton locked={locked} onToggle={() => dispatch({ type: 'toggleSkillLock', key })} label={def!.name} /></td>
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </>
  );
}
