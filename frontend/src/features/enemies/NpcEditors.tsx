import { useMemo, useState, type Dispatch } from 'react';
import clsx from 'clsx';
import { Lock, LockOpen, Minus, Plus, Star } from 'lucide-react';
import type { NpcRules, NpcSheet, NpcValidationReport, SkillLine } from '../../api/types';
import { Badge, Button, Field, IconButton, NumberInput, Segmented, Select } from '../../components/ui/ui';
import { type DraftAction, ERA_LABEL, gangsByEra, skillKey, STAT_KEYS } from './npcDraft';
import styles from './EnemyGenerator.module.css';
import { t, tv } from '../../i18n';

export function LockButton({ locked, onToggle, label }: { locked: boolean; onToggle: () => void; label: string }) {
  return (
    <IconButton label={locked ? t(`Destravar ${label}`, `Unlock ${label}`) : t(`Travar ${label} (o gerador não altera)`, `Lock ${label} (the generator won't change it)`)} onClick={onToggle}
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
      <Field label={t('Nome ou apelido', 'Name or nickname')} htmlFor="npc-name" hint={semi ? t('Mantido pelo gerador quando preenchido.', 'Kept by the generator when filled in.') : undefined}>
        <input id="npc-name" value={sheet.name} maxLength={80} onChange={(e) => set({ name: e.target.value })} placeholder={t('Ex.: Pitbull', 'E.g. Pitbull')} />
      </Field>
      <Field label={t('Conceito', 'Concept')} htmlFor="npc-concept">
        <input id="npc-concept" value={sheet.concept ?? ''} maxLength={300} onChange={(e) => set({ concept: e.target.value || null })}
          placeholder={t('Ex.: Policial corrupto da NCPD', 'E.g. Corrupt NCPD cop')} />
      </Field>
      <Field label={t('Gangue', 'Gang')} htmlFor="npc-sheet-gang" hint={t('Rótulo da ficha. Na geração vale a gangue da configuração.', 'Sheet label. During generation, the gang in the settings applies.')}>
        <Select id="npc-sheet-gang" value={sheet.gangId ?? ''} onChange={(e) => set({ gangId: e.target.value || null })}>
          <option value="">{t('Nenhuma', 'None')}</option>
          {gangsByEra(rules.gangs).map(([era, gangs]) => (
            <optgroup key={era} label={ERA_LABEL[era] ?? era}>
              {gangs.map((g) => <option key={g.id} value={g.id}>{tv(g.label)}</option>)}
            </optgroup>
          ))}
        </Select>
      </Field>
      <Field label="Role" htmlFor="npc-role">
        <Select id="npc-role" value={sheet.role ?? ''} onChange={(e) => set({ role: e.target.value || null, roleRank: e.target.value ? Math.max(sheet.roleRank, 1) : 0 })}>
          <option value="">{t('Sem Role Ability', 'No Role Ability')}</option>
          {rules.roles.map((r) => <option key={r} value={r}>{r}</option>)}
        </Select>
      </Field>
      <Field label={t('Rank da Role Ability', 'Role Ability Rank')} htmlFor="npc-rank">
        <NumberInput id="npc-rank" min={0} max={10} value={sheet.roleRank} disabled={!sheet.role}
          onChange={(v) => set({ roleRank: v ?? 0 })} />
      </Field>
      <Field label={t('Idioma nativo (Language 4 grátis)', 'Native language (free Language 4)')} htmlFor="npc-lang">
        <input id="npc-lang" list="npc-languages" value={sheet.nativeLanguage} onChange={(e) => set({ nativeLanguage: e.target.value })} />
      </Field>
      <Field label={t('Região (Local Expert 2 grátis)', 'Region (free Local Expert 2)')} htmlFor="npc-home">
        <input id="npc-home" list="npc-homes" value={sheet.home} onChange={(e) => set({ home: e.target.value })} />
      </Field>
      <datalist id="npc-languages">{rules.languages.map((l) => <option key={l} value={l} />)}</datalist>
      <datalist id="npc-homes">{rules.homes.map((h) => <option key={h} value={h} />)}</datalist>
      {semi && (
        <div className={styles.inline}>
          <LockButton locked={sheet.locks.identity} onToggle={() => dispatch({ type: 'toggleIdentityLock' })} label={t('Role, idioma e região', 'Role, language and region')} />
          <span className={styles.muted}>{t('Travar Role, Rank, idioma e região', 'Lock Role, Rank, language and region')}</span>
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
                <button type="button" aria-label={t(`Diminuir ${k}`, `Decrease ${k}`)} onClick={() => dispatch({ type: 'stat', key: k, value: value - 1 })}><Minus size={12} /></button>
                <NumberInput aria-label={k} value={value} min={0} max={20}
                  onChange={(v) => dispatch({ type: 'stat', key: k, value: v ?? 0 })} />
                <button type="button" aria-label={t(`Aumentar ${k}`, `Increase ${k}`)} onClick={() => dispatch({ type: 'stat', key: k, value: value + 1 })}><Plus size={12} /></button>
              </div>
              {armored !== undefined && armored !== value && (
                <span className={styles.statArmored} title={t('Valor com penalidade de armadura', 'Value with armor penalty')}>({armored}{t(') armadura', ') armor')}</span>
              )}
            </div>
          );
        })}
      </div>
      {d && (
        <div className={styles.derived}>
          <Derived label="HP" value={d.hp} title={t('10 + 5 × média de BODY e WILL, arredondada para cima (CP:R p.79)', '10 + 5 × average of BODY and WILL, rounded up (CP:R p.79)')} />
          <Derived label="Seriously Wounded" value={d.seriouslyWounded} title={t('Metade do HP, para cima (CP:R p.79)', 'Half the HP, rounded up (CP:R p.79)')} />
          <Derived label="Death Save" value={d.deathSave} title={t('Igual a BODY (CP:R p.79)', 'Equal to BODY (CP:R p.79)')} />
          <Derived label={t('SP corpo / cabeça', 'Body / head SP')} value={`${d.spBody} / ${d.spHead}`} />
          <Derived label={t('Peças de cyberware', 'Cyberware pieces')} value={d.cyberwarePieces} />
          <Field label={t('LUCK (fora do orçamento)', 'LUCK (outside the budget)')} htmlFor="npc-luck">
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
        <Segmented<SkillFilter> size="sm" ariaLabel={t('Filtrar perícias', 'Filter skills')} value={filter} onChange={setFilter}
          options={[{ value: 'used', label: t('Com nível', 'With level') }, { value: 'all', label: t('Todas', 'All') }]} />
        <input type="search" placeholder={t('Buscar perícia', 'Search skill')} value={query} onChange={(e) => setQuery(e.target.value)} aria-label={t('Buscar perícia', 'Search skill')} />
        <Select value={specSkill} onChange={(e) => setSpecSkill(e.target.value)} aria-label={t('Perícia com especialização', 'Skill with specialization')}>
          <option value="">{t('Adicionar especialização…', 'Add specialization…')}</option>
          {specialized.map((s) => <option key={s.id} value={s.id}>{tv(s.name)}</option>)}
        </Select>
        <input type="text" placeholder={t('Idioma, região, estilo…', 'Language, region, form…')} value={specText} onChange={(e) => setSpecText(e.target.value)}
          aria-label={t('Especialização', 'Specialization')} onKeyDown={(e) => e.key === 'Enter' && addSpecialized()} />
        <Button onClick={addSpecialized} disabled={!specSkill || !specText.trim()} icon={<Plus size={14} />}>{t('Adicionar', 'Add')}</Button>
      </div>
      <p className={styles.muted}>
        {t('Nível é o valor da perícia; Base = STAT (com penalidade de armadura) + nível. Os níveis gratuitos não custam pontos (', 'Level is the skill\'s value; Base = STAT (with armor penalty) + level. Free levels don\'t cost points (')}{rules.freeSkillsSource}{t('). ★ marca as perícias importantes, cujas bases são comparadas com a faixa sugerida.', '). ★ marks the important skills, whose bases are compared with the suggested range.')}
      </p>
      <div className={styles.tableWrap}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th aria-label={t('Importante', 'Important')}>★</th>
              <th>{t('Perícia', 'Skill')}</th>
              <th>STAT</th>
              <th className={styles.num}>{t('Grátis', 'Free')}</th>
              <th>{t('Nível', 'Level')}</th>
              <th className={styles.num}>{t('Custo', 'Cost')}</th>
              <th className={styles.num}>Base</th>
              {semi && <th aria-label={t('Trava', 'Lock')} />}
            </tr>
          </thead>
          <tbody>
            {rows.map(({ key, def, spec, line, level }) => {
              const isKey = sheet.keySkills.some((k) => k.toLowerCase() === key.toLowerCase());
              const locked = sheet.locks.skills.some((k) => k.toLowerCase() === key.toLowerCase());
              return (
                <tr key={key} className={clsx(hasIssue(report, `skills.${key}`) && styles.rowError)}>
                  <td>
                    <IconButton label={isKey ? t('Desmarcar como importante', 'Unmark as important') : t('Marcar como importante', 'Mark as important')}
                      onClick={() => dispatch({ type: 'toggleKeySkill', key })}>
                      <Star size={14} className={isKey ? styles.starOn : styles.star} fill={isKey ? 'currentColor' : 'none'} />
                    </IconButton>
                  </td>
                  <td>
                    <span className={styles.itemName}>{tv(def!.name)}{spec ? ` (${spec})` : ''}</span>
                    {def!.doubleCost && <> <Badge tone="amber" title={t('Custa 2 pontos por nível (CP:R p.81)', 'Costs 2 points per level (CP:R p.81)')}>x2</Badge></>}
                    {def!.combat && <> <Badge tone="red">{t('combate', 'combat')}</Badge></>}
                  </td>
                  <td>{def!.stat}</td>
                  <td className={styles.num}>{line?.freeLevel || '—'}</td>
                  <td>
                    <NumberInput min={0} max={10} value={level} aria-label={t(`Nível de ${tv(def!.name)}`, `${tv(def!.name)} level`)} allowEmpty
                      title={t('Vazio volta ao nível padrão (básicas: 2)', 'Empty goes back to the default level (basic: 2)')}
                      onChange={(v) => dispatch({ type: 'skill', key, value: v })} />
                  </td>
                  <td className={styles.num}>{line?.cost ?? 0}</td>
                  <td className={styles.num}>{line?.base ?? '—'}</td>
                  {semi && (
                    <td><LockButton locked={locked} onToggle={() => dispatch({ type: 'toggleSkillLock', key })} label={tv(def!.name)} /></td>
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
