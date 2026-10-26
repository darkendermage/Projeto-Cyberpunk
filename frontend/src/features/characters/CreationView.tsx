import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { CheckCircle2, Dices, Plus, RotateCcw, Undo2 } from 'lucide-react';
import { problemMessage, useCharacterCommand } from '../../api/characters';
import type {
  CharacterDetail, CharacterRules, CharacterSheet, CreationMethod, RoleRules, SkillRule,
} from '../../api/characterTypes';
import { Button, Field, IconButton, Select } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import { languagesFor, METHOD_LABEL, SKILL_CATEGORIES, STAT_LABEL, withCurrent } from './characterLabels';
import { BudgetBar, IssueList, NumberStepper, RoleAbilityButton, SheetPanel } from './CharacterUi';
import { RelationsPanel, RoleLifepathPanels, RollMissingButton, StyleTablesFields, TablesPanel } from './LifepathPanels';
import { StorePicker, type PurchaseInput } from './StorePicker';
import type { SheetEditor } from './useSheetEditor';
import styles from './Characters.module.css';
import { lang, skillName, t, tv } from '../../i18n';

// Lifepath comes right after the Role, before STATs, as in the book (CP:R p.43).
const STEPS = [t('Identidade', 'Identity'), t('Caminho de Vida', 'Lifepath'), 'STATs', t('Perícias', 'Skills'), t('Equipamento', 'Gear'), t('Resumo', 'Summary')];

interface Props { detail: CharacterDetail; rules: CharacterRules; editor: SheetEditor }

export function CreationView({ detail, rules, editor }: Props) {
  const [step, setStep] = useState(0);
  const toast = useToast();
  const navigate = useNavigate();
  const command = useCharacterCommand(detail.id);
  const sheet = editor.draft;
  const role = rules.roles.find((r) => r.id === sheet.roles[0]?.role);
  const method = sheet.creation.method;
  const v = detail.validation;

  /** Saves pending edits first, so a command never runs on an older copy of the sheet. */
  const run = async (path: string, body: Record<string, unknown> = {}, success?: string) => {
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

  const finalize = async () => {
    const result = await run('creation/finalize', {}, t('Criação concluída. Bem-vindo a Night City.', 'Creation complete. Welcome to Night City.'));
    if (result) navigate(`/characters/${detail.id}/ficha`);
  };

  const errors = v.issues.filter((i) => i.severity === 'Error');
  const others = v.issues.filter((i) => i.severity !== 'Error');

  return (
    <div className={styles.creation}>
      <div className={styles.column}>
        <nav className={styles.steps} aria-label={t('Passos da criação', 'Creation steps')}>
          {STEPS.map((label, i) => (
            <button key={label} type="button" className={`${styles.step} ${i === step ? styles.stepActive : ''}`}
              aria-current={i === step ? 'step' : undefined} onClick={() => setStep(i)}>
              <span className={styles.stepNumber}>{i + 1}</span>{label}
            </button>
          ))}
        </nav>

        {step === 0 && <IdentityStep sheet={sheet} rules={rules} editor={editor} onReset={(m, r) => run('creation/reset', { method: m, role: r }, t('Criação reiniciada.', 'Creation restarted.'))} />}
        {step === 1 && <LifepathStep rules={rules} editor={editor} />}
        {step === 2 && role && <StatsStep sheet={sheet} rules={rules} role={role} method={method} editor={editor} run={run} detail={detail} />}
        {step === 3 && role && <SkillsStep sheet={sheet} rules={rules} role={role} method={method} editor={editor} run={run} />}
        {step === 4 && role && <EquipmentStep sheet={sheet} role={role} method={method} editor={editor} run={run} detail={detail} busy={command.isPending} />}
        {step === 5 && (
          <SheetPanel title={t('Resumo e conclusão', 'Summary and completion')} icon={<CheckCircle2 size={16} />}>
            <dl className={styles.kv}>
              <dt>{t('Pontos de vida', 'Hit Points')}</dt><dd>{detail.report.health.maxHp}</dd>
              <dt>{t('Seriously Wounded abaixo de', 'Seriously Wounded below')}</dt><dd>{detail.report.health.seriouslyWoundedThreshold}</dd>
              <dt>Death Save</dt><dd>{detail.report.health.deathSave}</dd>
              <dt>{t('Humanidade', 'Humanity')}</dt><dd>{detail.report.humanity.current} / {detail.report.humanity.max}</dd>
              <dt>{t('EMP efetiva', 'Effective EMP')}</dt><dd>{detail.report.humanity.empEffective}</dd>
              <dt>{t('Saldo atual', 'Current balance')}</dt><dd>{formatEb(sheet.wallet.balance)}</dd>
            </dl>
            {(method === 'Streetrat' || method === 'Edgerunner') && role && (
              <p className={styles.small}>
                {t('Ao concluir você recebe o pacote da Role ', 'On completion you get the starting package of the Role ')}{role.id}{t(': armas, armadura, roupa e cyberware com Humanity Loss pré-definida de ', ': weapons, armor, clothes and cyberware with a preset Humanity Loss of ')}{role.starting.find((s) => s.key === 'cyberware')?.humanityLoss} ({role.starting[0]?.source}, CP:R p.117).
              </p>
            )}
            {method === 'CompletePackage' && (
              <p className={styles.small}>
                {t('Ao concluir, a sobra do orçamento geral vira saldo e a do orçamento de moda se perde (CP:R p.104–105).', 'On completion, what\'s left of the general budget becomes balance and what\'s left of the fashion budget is lost (CP:R p.104–105).')}
              </p>
            )}
            <div className={styles.row}>
              <Button variant="primary" icon={<CheckCircle2 size={16} />} disabled={!v.canFinalize || command.isPending} onClick={finalize}>
                {t('Concluir criação', 'Complete creation')}
              </Button>
              {!v.canFinalize && <span className={`${styles.small} ${styles.amber}`}>{t('Resolva as pendências ao lado para concluir.', 'Resolve the issues on the side to complete.')}</span>}
            </div>
          </SheetPanel>
        )}

        <div className={styles.row}>
          <Button variant="ghost" disabled={step === 0} onClick={() => setStep(step - 1)}>{t('Voltar', 'Back')}</Button>
          <Button disabled={step === STEPS.length - 1} onClick={() => setStep(step + 1)}>{t('Próximo', 'Next')}</Button>
        </div>
      </div>

      <aside className={styles.column}>
        <SheetPanel title={t('Orçamentos', 'Budgets')}>
          {v.stats && <BudgetBar label={t('Pontos de STAT', 'STAT points')} budget={v.stats} />}
          {v.skills && <BudgetBar label={t('Pontos de perícia', 'Skill points')} budget={v.skills} />}
          {v.gear && <BudgetBar label={t('Orçamento geral', 'General budget')} budget={v.gear} money />}
          {v.fashion && <BudgetBar label={t('Orçamento de moda', 'Fashion budget')} budget={v.fashion} money />}
          {!v.stats && !v.skills && !v.gear && (
            <p className={styles.small}>{METHOD_LABEL[method]}{t(': valores do template da Role. Saldo ', ': values from the Role template. Balance ')}{formatEb(sheet.wallet.balance)}.</p>
          )}
        </SheetPanel>
        <SheetPanel title={t(`Pendências (${errors.length})`, `Issues (${errors.length})`)}>
          <IssueList issues={errors} empty={t('Nada impede a conclusão.', 'Nothing blocks completion.')} />
        </SheetPanel>
        {others.length > 0 && <SheetPanel title={t('Avisos', 'Warnings')}><IssueList issues={others} /></SheetPanel>}
      </aside>
    </div>
  );
}

type Run = (path: string, body?: Record<string, unknown>, success?: string) => Promise<CharacterDetail | null>;

// ------------------------------------------------------------ 1. identity

function IdentityStep({ sheet, rules, editor, onReset }: {
  sheet: CharacterSheet; rules: CharacterRules; editor: SheetEditor; onReset: (m: CreationMethod, role: string) => void;
}) {
  const [method, setMethod] = useState(sheet.creation.method);
  const [role, setRole] = useState(sheet.roles[0]?.role ?? 'Solo');
  const id = sheet.identity;
  const set = (patch: Partial<CharacterSheet['identity']>) => editor.update((s) => ({ ...s, identity: { ...s.identity, ...patch } }));
  const languages = languagesFor(rules, sheet.lifepath.culturalRegion);

  return (
    <>
      <SheetPanel title={t('Identidade', 'Identity')}>
        <div className={styles.formGrid}>
          <Field label={t('Nome', 'Name')} htmlFor="id-name"><input id="id-name" className={styles.input} value={id.name} onChange={(e) => set({ name: e.target.value })} /></Field>
          <Field label="Handle" htmlFor="id-handle"><input id="id-handle" className={styles.input} value={id.handle ?? ''} onChange={(e) => set({ handle: e.target.value })} /></Field>
          <Field label={t('Pronomes', 'Pronouns')} htmlFor="id-pron"><input id="id-pron" className={styles.input} value={id.pronouns ?? ''} onChange={(e) => set({ pronouns: e.target.value })} /></Field>
          <Field label={t('Idade', 'Age')} htmlFor="id-age"><input id="id-age" className={styles.input} type="number" min={0} value={id.age ?? ''} onChange={(e) => set({ age: e.target.value ? Number(e.target.value) : null })} /></Field>
          <Field label={t('Conceito', 'Concept')} htmlFor="id-concept"><input id="id-concept" className={styles.input} value={id.concept ?? ''} onChange={(e) => set({ concept: e.target.value })} /></Field>
          <Field label={t('Origem cultural', 'Cultural origin')} htmlFor="id-region" hint="CP:R p.45">
            <Select id="id-region" value={sheet.lifepath.culturalRegion ?? ''}
              onChange={(e) => editor.update((s) => ({ ...s, lifepath: { ...s.lifepath, culturalRegion: e.target.value || null } }))}>
              <option value="">{t('Escolha…', 'Choose…')}</option>
              {rules.culturalOrigins.map((o) => <option key={o.region} value={o.region}>{o.region}</option>)}
            </Select>
          </Field>
          <Field label={t('Idioma de origem', 'Native language')} htmlFor="id-lang" hint={t(`Language ${rules.originLanguageLevel}, sem custo`, `Language ${rules.originLanguageLevel}, free`)}>
            <Select id="id-lang" value={id.nativeLanguage ?? ''} onChange={(e) => set({ nativeLanguage: e.target.value })}>
              <option value="">{t('Escolha…', 'Choose…')}</option>
              {withCurrent(languages, id.nativeLanguage).map((l) => <option key={l} value={l}>{l}</option>)}
            </Select>
          </Field>
          <Field label={t('Bairro (Local Expert)', 'Neighborhood (Local Expert)')} htmlFor="id-home">
            <Select id="id-home" value={id.home} onChange={(e) => set({ home: e.target.value })}>
              {!id.home && <option value="">{t('Escolha…', 'Choose…')}</option>}
              {withCurrent(rules.homes, id.home).map((h) => <option key={h} value={h}>{h}</option>)}
            </Select>
          </Field>
        </div>
      </SheetPanel>

      <SheetPanel title={t('Método e Role', 'Method and Role')}>
        <p className={styles.small}>
          {t('Trocar o método ou a Role reinicia STATs, perícias e compras da criação. Identidade e Lifepath são mantidos.', 'Changing the method or the Role restarts STATs, skills and creation purchases. Identity and Lifepath are kept.')}
        </p>
        <div className={styles.row}>
          <Select value={method} onChange={(e) => setMethod(e.target.value as CreationMethod)} aria-label={t('Método', 'Method')}>
            {rules.methods.map((m) => <option key={m.method} value={m.method}>{METHOD_LABEL[m.method]}</option>)}
          </Select>
          <Select value={role} onChange={(e) => setRole(e.target.value)} aria-label="Role">
            {rules.roles.map((r) => <option key={r.id} value={r.id}>{r.id} — {r.ability}</option>)}
          </Select>
          <Button icon={<RotateCcw size={15} />} disabled={method === sheet.creation.method && role === sheet.roles[0]?.role}
            onClick={() => onReset(method, role)}>{t('Aplicar e reiniciar', 'Apply and restart')}</Button>
        </div>
        <p className={styles.small}>{t('Habilidade de Role de ', 'Role Ability of ')}{role}: <RoleAbilityButton rules={rules} role={role} /> {t('(clique para ver o que faz)', '(click to see what it does)')}</p>
      </SheetPanel>
    </>
  );
}

// ------------------------------------------------------------ 2. Lifepath

/**
 * The Lifepath step (CP:R p.43): general tables, style, friends/enemies/lovers and the Role's flowchart. Optional: the
 * book says "roll or choose" and nothing here blocks finishing creation. The same answers show on the Caminho de Vida tab.
 */
function LifepathStep({ rules, editor }: { rules: CharacterRules; editor: SheetEditor }) {
  return (
    <>
      <SheetPanel title={t('Caminho de vida', 'Lifepath')} actions={<RollMissingButton rules={rules} editor={editor} />}>
        <p className={styles.small}>
          {t('Role ou escolha cada tabela; dá para editar tudo depois na aba Caminho de Vida. É opcional e não impede a conclusão (CP:R p.43). "Rolar o que falta" só preenche o que está em branco.', 'Roll or choose each table; you can edit everything later on the Lifepath tab. It\'s optional and doesn\'t block completion (CP:R p.43). "Roll what\'s missing" only fills in what\'s blank.')}
        </p>
      </SheetPanel>
      <div className={styles.lifepathStep}>
        <div className={styles.column}>
          <TablesPanel rules={rules} editor={editor} />
          <SheetPanel title={t('Estilo', 'Style')}>
            <div className={styles.column} style={{ gap: 8 }}><StyleTablesFields rules={rules} editor={editor} /></div>
          </SheetPanel>
        </div>
        <div className={styles.column}>
          <RoleLifepathPanels rules={rules} editor={editor} onlyCurrent />
          <RelationsPanel rules={rules} editor={editor} />
        </div>
      </div>
    </>
  );
}

// ------------------------------------------------------------ 3. STATs

function StatsStep({ sheet, rules, role, method, editor, run, detail }: {
  sheet: CharacterSheet; rules: CharacterRules; role: RoleRules; method: CreationMethod; editor: SheetEditor; run: Run; detail: CharacterDetail;
}) {
  const [manual, setManual] = useState<Record<string, string>>({});
  const order = rules.statOrder;

  if (method === 'CompletePackage') {
    const m = rules.methods.find((x) => x.method === 'CompletePackage')!;
    const o = sheet.creation.overrides;
    const min = o.statMin ?? m.statMin ?? 2;
    const max = o.statMax ?? m.statMax ?? 8;
    return (
      <SheetPanel title={t('Compra de STATs', 'Buying STATs')} actions={<span className={styles.source}>{m.source}</span>}>
        <p className={styles.small}>
          {t('Distribua ', 'Spend ')}{o.statPoints ?? m.statPoints} {t('pontos entre os dez STATs, de ', 'points across the ten STATs, from ')}{min} a {max} {t('cada (CP:R p.78).', 'each (CP:R p.78).')}
        </p>
        <div className={styles.statEditor}>
          {order.map((k) => (
            <NumberStepper key={k} label={k} title={STAT_LABEL[k]} value={sheet.stats[k] ?? min} min={min} max={max}
              onChange={(value) => editor.update((s) => ({ ...s, stats: { ...s.stats, [k]: value } }))} />
          ))}
        </div>
      </SheetPanel>
    );
  }

  const rolls = sheet.creation.statRolls;
  const row = sheet.creation.templateRow;
  return (
    <SheetPanel title={method === 'Streetrat' ? t('Linha da tabela da Role', 'Row of the Role table') : t('Rolagem de cada STAT', 'Roll each STAT')}
      actions={method === 'Streetrat'
        ? <Button icon={<Dices size={15} />} onClick={() => run('creation/template-row', { row: null })}>{t('Rolar 1d10', 'Roll 1d10')}</Button>
        : <Button icon={<Dices size={15} />} onClick={() => run('creation/stat-rolls', { rolls: null })}>{t('Rolar todos', 'Roll all')}</Button>}>
      <p className={styles.small}>
        {method === 'Streetrat'
          ? t('Role 1d10 ou escolha uma linha: todos os STATs vêm dela, sem trocas (CP:R p.73).', 'Roll 1d10 or pick a row: every STAT comes from it, no swaps (CP:R p.73).')
          : t('Role 1d10 para cada STAT e leia o valor na coluna dele (CP:R p.77). Também dá para digitar dados físicos.', 'Roll 1d10 for each STAT and read the value in its column (CP:R p.77). You can also type in physical dice.')}
      </p>
      <table className={`${styles.table} ${styles.statTable}`}>
        <caption>{role.id} · {role.statTableSource}</caption>
        <thead><tr><th>d10</th>{order.map((k) => <th key={k} title={STAT_LABEL[k]}>{k}</th>)}</tr></thead>
        <tbody>
          {role.statTable.map((values, i) => (
            <tr key={i} data-active={method === 'Streetrat' && row === i + 1}
              onClick={method === 'Streetrat' ? () => run('creation/template-row', { row: i + 1 }) : undefined}>
              <td className={styles.num}>{i + 1}</td>
              {values.map((value, c) => {
                const picked = method === 'Edgerunner' && rolls[order[c]] === i + 1;
                return <td key={c} className={styles.num} style={picked ? { color: 'var(--cyan)', background: 'var(--red-haze)', fontWeight: 700 } : undefined}>{value}</td>;
              })}
            </tr>
          ))}
        </tbody>
      </table>
      {method === 'Edgerunner' && (
        <div className={styles.row}>
          {order.map((k) => (
            <label key={k} className={styles.small}>
              {k}{' '}
              <input className={`${styles.input} ${styles.inputSmall}`} type="number" min={1} max={10}
                value={manual[k] ?? rolls[k] ?? ''} onChange={(e) => setManual({ ...manual, [k]: e.target.value })} />
            </label>
          ))}
          <Button disabled={Object.keys(manual).length === 0} onClick={async () => {
            const given = Object.fromEntries(order.map((k) => [k, Number(manual[k] ?? rolls[k])]).filter(([, n]) => Number(n) >= 1));
            if (await run('creation/stat-rolls', { rolls: given })) setManual({});
          }}>{t('Aplicar dados digitados', 'Apply typed dice')}</Button>
        </div>
      )}
      <div className={styles.statEditor}>
        {order.map((k) => (
          <div key={k} className={styles.stat}>
            <span className={styles.statKey}>{k}</span>
            <span className={styles.statValue}>{detail.sheet.stats[k] ?? '—'}</span>
          </div>
        ))}
      </div>
    </SheetPanel>
  );
}

// ------------------------------------------------------------ 4. skills

function SkillsStep({ sheet, rules, role, method, editor, run }: {
  sheet: CharacterSheet; rules: CharacterRules; role: RoleRules; method: CreationMethod; editor: SheetEditor; run: Run;
}) {
  const byId = useMemo(() => new Map(rules.skills.map((s) => [s.id, s])), [rules.skills]);
  const m = rules.methods.find((x) => x.method === method)!;
  const max = sheet.creation.overrides.skillMax ?? m.skillMax ?? 6;
  const native = sheet.identity.nativeLanguage ? `language:${sheet.identity.nativeLanguage}` : null;
  const setLevel = (key: string, level: number) => editor.update((s) => {
    const skills = { ...s.skills };
    if (level <= 0) delete skills[key]; else skills[key] = level;
    return { ...s, skills };
  });

  const choices = role.choiceSkills.length > 0 && method !== 'CompletePackage' && (
    <SheetPanel title={t('Especializações do template', 'Template specializations')}>
      {role.choiceSkills.map((id) => <ChoiceInput key={id} skill={byId.get(id)!} current={sheet.creation.skillChoices[id]}
        onPick={(spec) => run('creation/skill-choice', { skillId: id, specialization: spec })} />)}
    </SheetPanel>
  );

  if (method === 'Streetrat') {
    return (
      <>
        {choices}
        <SheetPanel title={t('Perícias do template', 'Template skills')} actions={<span className={styles.source}>{role.skillsSource}</span>}>
          <p className={styles.small}>{t('No Streetrat as perícias vêm prontas; só as especializações em aberto são escolhidas.', 'In Streetrat the skills come ready; only the open specializations are chosen.')}</p>
          <SkillTable keys={Object.keys(sheet.skills)} sheet={sheet} byId={byId} readOnly />
        </SheetPanel>
      </>
    );
  }

  if (method === 'Edgerunner') {
    const keys = Object.keys(sheet.skills).filter((k) => k !== native);
    return (
      <>
        {choices}
        <SheetPanel title={t('Pontos nas perícias da Role', 'Points in the Role skills')} actions={<span className={styles.source}>{m.source}</span>}>
          <p className={styles.small}>
            {m.skillPoints} {t('pontos entre as 20 perícias da Role, de ', 'points across the 20 Role skills, from ')}{m.skillMin} a {max} {t('cada; perícias (x2) custam o dobro (CP:R p.88).', 'each; (x2) skills cost double (CP:R p.88).')}
          </p>
          <SkillTable keys={keys} sheet={sheet} byId={byId} min={m.skillMin ?? 2} max={max} onChange={setLevel} />
        </SheetPanel>
      </>
    );
  }

  return (
    <SheetPanel title={t('Compra de perícias', 'Buying skills')} actions={<span className={styles.source}>{m.source}</span>}>
      <p className={styles.small}>
        {m.skillPoints} {t('pontos em qualquer perícia, até ', 'points in any skill, up to ')}{max}{t('. As 13 básicas ficam em 2 ou mais; (x2) custa o dobro (CP:R p.90).', '. The 13 basic ones stay at 2 or more; (x2) costs double (CP:R p.90).')}
      </p>
      {SKILL_CATEGORIES.map((c) => {
        const defs = rules.skills.filter((s) => s.category === c.key);
        const keys = defs.flatMap((d) => d.specialized ? Object.keys(sheet.skills).filter((k) => k.split(':')[0] === d.id) : [d.id]);
        return (
          <div key={c.key} className={styles.skillGroup}>
            <SkillTable caption={c.label} keys={keys} sheet={sheet} byId={byId} min={0} max={max} onChange={setLevel}
              basic={rules.basicSkills.map((k) => k.replace('@home', sheet.identity.home))} />
            {defs.filter((d) => d.specialized).map((d) => <AddSpecialization key={d.id} skill={d} onAdd={(spec) => setLevel(`${d.id}:${spec}`, 1)} />)}
          </div>
        );
      })}
    </SheetPanel>
  );
}

function SkillTable({ keys, sheet, byId, min = 0, max = 6, onChange, readOnly, caption, basic = [] }: {
  keys: string[]; sheet: CharacterSheet; byId: Map<string, SkillRule>; min?: number; max?: number;
  onChange?: (key: string, level: number) => void; readOnly?: boolean; caption?: string; basic?: string[];
}) {
  const native = sheet.identity.nativeLanguage ? `language:${sheet.identity.nativeLanguage}`.toLowerCase() : null;
  return (
    <table className={styles.table}>
      {caption && <caption>{caption}</caption>}
      <thead><tr><th>{t('Perícia', 'Skill')}</th><th className={styles.num}>STAT</th><th className={styles.num}>{t('Custo', 'Cost')}</th><th className={styles.num}>{t('Nível', 'Level')}</th></tr></thead>
      <tbody>
        {keys.map((key) => {
          const [id, spec] = key.split(':');
          const def = byId.get(id);
          const level = sheet.skills[key] ?? 0;
          const isNative = key.toLowerCase() === native;
          const floor = basic.some((b) => b.toLowerCase() === key.toLowerCase()) ? Math.max(min, 2) : min;
          return (
            <tr key={key} className={level === 0 ? styles.dim : undefined}>
              <td title={tv(def?.name)}>
                {def ? skillName(def.name, def.namePt) : id}{spec && t(` (${spec === '@choose' ? 'a escolher' : spec})`, ` (${spec === '@choose' ? 'to choose' : spec})`)}
                {isNative && <span className={styles.tag}>{t('origem · 4 grátis', 'native · 4 free')}</span>}
              </td>
              <td className={styles.num}>{def?.stat}</td>
              <td className={styles.num}>{def?.doubleCost ? 'x2' : 'x1'}</td>
              <td className={styles.num}>
                {readOnly || !onChange || isNative ? <span className={styles.mono}>{level}</span> : (
                  <span className={styles.row} style={{ flexWrap: 'nowrap', justifyContent: 'center' }}>
                    <IconButton label={t(`Diminuir ${tv(def?.name)}`, `Decrease ${tv(def?.name)}`)} disabled={level <= floor} onClick={() => onChange(key, level - 1)}>−</IconButton>
                    <span className={styles.mono}>{level}</span>
                    <IconButton label={t(`Aumentar ${tv(def?.name)}`, `Increase ${tv(def?.name)}`)} disabled={level >= max} onClick={() => onChange(key, level + 1)}>+</IconButton>
                  </span>
                )}
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

function ChoiceInput({ skill, current, onPick }: { skill: SkillRule; current?: string; onPick: (spec: string) => void }) {
  const [value, setValue] = useState(current ?? '');
  return (
    <div className={styles.row}>
      <span className={styles.grow}>{skillName(skill.name, skill.namePt)}{lang === 'pt' && skill.namePt && <span className={styles.muted}> ({skill.name})</span>}</span>
      <input className={styles.input} style={{ maxWidth: 220 }} value={value} placeholder={t('Especialização', 'Specialization')} onChange={(e) => setValue(e.target.value)} />
      <Button disabled={!value.trim() || value === current} onClick={() => onPick(value.trim())}>{t('Definir', 'Set')}</Button>
    </div>
  );
}

function AddSpecialization({ skill, onAdd }: { skill: SkillRule; onAdd: (spec: string) => void }) {
  const [value, setValue] = useState('');
  return (
    <div className={styles.row} style={{ marginTop: 6 }}>
      <input className={styles.input} style={{ maxWidth: 240 }} value={value} placeholder={t(`Nova: ${skillName(skill.name, skill.namePt)}…`, `New: ${skill.name}…`)}
        aria-label={t(`Adicionar especialização de ${tv(skill.name)}`, `Add a ${tv(skill.name)} specialization`)} onChange={(e) => setValue(e.target.value)} />
      <IconButton label={t('Adicionar', 'Add')} disabled={!value.trim()} onClick={() => { onAdd(value.trim()); setValue(''); }}><Plus size={14} /></IconButton>
    </div>
  );
}

// ------------------------------------------------------------ 5. equipment

function EquipmentStep({ sheet, role, method, editor, run, detail, busy }: {
  sheet: CharacterSheet; role: RoleRules; method: CreationMethod; editor: SheetEditor; run: Run; detail: CharacterDetail; busy: boolean;
}) {
  const purchases = [
    ...detail.report.inventory.filter((i) => i.origin === 'Purchase').map((i) => ({ id: i.instanceId, name: i.name, paid: i.pricePaid, pool: i.paidFrom, qty: i.rounds ?? i.quantity })),
    ...detail.sheet.cyberware.filter((c) => c.origin === 'Purchase').map((c) => ({
      id: c.instanceId, name: detail.report.cyberware.find((l) => l.instanceId === c.instanceId)?.name ?? '?', paid: c.pricePaid, pool: c.paidFrom, qty: 1,
    })),
  ];
  const buy = (input: PurchaseInput) => run('purchase', { ...input });

  return (
    <>
      {method !== 'CompletePackage' && role.starting.map((section) => (
        <SheetPanel key={section.key} title={tv(section.label)}
          actions={<span className={styles.source}>{section.source}{section.humanityLoss != null && ` · HL ${section.humanityLoss}`}</span>}>
          <ul className={styles.list}>
            {section.groups.map((g) => {
              const key = `${section.key}:${g.index}`;
              const picked = sheet.creation.startingChoices[key];
              return (
                <li key={key} className={styles.listItem} data-tone={g.options.length > 1 && picked === undefined ? 'warn' : 'ok'}>
                  {g.options.length === 1 ? <span>{tv(g.options[0].label)}</span> : (
                    <div className={styles.optionGroup} role="radiogroup" aria-label={t('Escolha uma opção', 'Choose an option')}>
                      {g.options.map((o) => (
                        <button key={o.index} type="button" role="radio" aria-checked={picked === o.index}
                          className={`${styles.choice} ${picked === o.index ? styles.choiceActive : ''}`} style={{ padding: '6px 10px' }}
                          onClick={() => editor.update((s) => ({ ...s, creation: { ...s.creation, startingChoices: { ...s.creation.startingChoices, [key]: o.index } } }))}>
                          {tv(o.label)}
                        </button>
                      ))}
                      <span className={styles.small}>{picked === undefined ? t('escolha uma', 'choose one') : ''}</span>
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        </SheetPanel>
      ))}

      {purchases.length > 0 && (
        <SheetPanel title={t('Compras da criação', 'Creation purchases')}>
          <table className={styles.table}>
            <thead><tr><th>Item</th><th className={styles.num}>{t('Qtd', 'Qty')}</th><th>{t('Pago com', 'Paid with')}</th><th className={styles.num}>{t('Preço', 'Price')}</th><th /></tr></thead>
            <tbody>
              {purchases.map((p) => (
                <tr key={p.id}>
                  <td>{tv(p.name)}</td>
                  <td className={styles.num}>{p.qty}</td>
                  <td className={styles.small}>{p.pool === 'CreationFashion' ? t('Moda', 'Fashion') : p.pool === 'CreationGear' ? t('Geral', 'General') : t('Saldo', 'Balance')}</td>
                  <td className={styles.total}>{formatEb(p.paid)}</td>
                  <td><IconButton label={t(`Desfazer compra de ${tv(p.name)}`, `Undo purchase of ${tv(p.name)}`)} disabled={busy} onClick={() => run('purchase/undo', { instanceId: p.id })}><Undo2 size={15} /></IconButton></td>
                </tr>
              ))}
            </tbody>
          </table>
        </SheetPanel>
      )}

      <SheetPanel title={method === 'CompletePackage' ? t('Comprar equipamento e cyberware', 'Buy gear and cyberware') : t(`Gastar os ${formatEb(sheet.wallet.balance)} iniciais (opcional)`, `Spend the starting ${formatEb(sheet.wallet.balance)} (optional)`)}>
        <StorePicker busy={busy} creationBudgets={method === 'CompletePackage'} onBuy={buy} creating
          available={{ balance: sheet.wallet.balance, gear: detail.validation.gear?.remaining, fashion: detail.validation.fashion?.remaining }} />
      </SheetPanel>
    </>
  );
}
