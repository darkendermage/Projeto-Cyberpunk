import { useState } from 'react';
import clsx from 'clsx';
import { AlertOctagon, AlertTriangle, CheckCircle2, Info, ShieldAlert } from 'lucide-react';
import type {
  BudgetLine, GangInfo, GenerationConflict, NpcIssue, NpcLevel, NpcRules, NpcSheet, NpcValidationReport, RuleFact,
} from '../../api/types';
import { Badge, Button, Collapsible, Spinner, Switch } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { ERA_LABEL, LEVEL_LABEL } from './npcDraft';
import styles from './EnemyGenerator.module.css';
import { t, tv } from '../../i18n';

const LEVELS: NpcLevel[] = ['Mook', 'Lieutenant', 'MiniBoss', 'Boss'];

// ---------------------------------------------------------------- category

export function LevelPicker({
  rules, level, hardened, onChange,
}: { rules: NpcRules; level: NpcLevel; hardened: boolean; onChange: (level: NpcLevel, hardened: boolean) => void }) {
  return (
    <div className={styles.toolbarGroup}>
      <div className={styles.levels} role="radiogroup" aria-label={t('Categoria do inimigo', 'Enemy category')}>
        {LEVELS.map((l) => {
          const entry = rules.templates.find((x) => x.level === l && x.hardened === hardened);
          return (
            <button key={l} type="button" role="radio" aria-checked={level === l}
              className={clsx(styles.level, level === l && styles.levelActive)} onClick={() => onChange(l, hardened)}>
              <span className={styles.levelName}>{LEVEL_LABEL[l]}</span>
              {entry && (
                <>
                  <span className={styles.levelNumbers}>
                    {entry.values.statPoints} {t('STAT · máx ', 'STAT · max ')}{entry.values.maxStat} · {entry.values.skillPoints} {t('perícia', 'skill')}
                  </span>
                  <span className={styles.levelNumbers}>
                    {formatEb(entry.values.gearBudget)}{t('eb equip. · ', 'eb gear · ')}{entry.values.cyberwareBudget === null ? t('cyber ilimitado', 'unlimited cyber') : `${formatEb(entry.values.cyberwareBudget)}eb cyber`}
                  </span>
                  <span className={styles.levelSummary}>{tv(entry.summary)}</span>
                </>
              )}
            </button>
          );
        })}
      </div>
      <Switch checked={hardened} onChange={(h) => onChange(level, h)}
        label={t('Hardened (para Crews otimizadas para combate, DGD p.150–152)', 'Hardened (for combat-optimized Crews, DGD p.150–152)')} />
    </div>
  );
}

// ---------------------------------------------------------------- budgets

function Budget({ name, line, unit }: { name: string; line: BudgetLine; unit?: string }) {
  const total = line.total;
  const pct = total === null ? 0 : total === 0 ? 100 : Math.min(100, (line.used / total) * 100);
  const fmt = (n: number) => (unit ? `${formatEb(n)}${unit}` : String(n));
  return (
    <div className={clsx(styles.budget, line.exceeded && styles.budgetOver, total !== null && line.used === total && styles.budgetFull)}
      title={line.source}>
      <span className={styles.budgetName}>{name}</span>
      <span className={styles.budgetValue}>{fmt(line.used)} / {total === null ? '∞' : fmt(total)}</span>
      <div className={styles.budgetBar} aria-hidden><div className={styles.budgetFill} style={{ width: `${pct}%` }} /></div>
      <span className={styles.budgetRemaining}>
        {total === null ? t('Orçamento ilimitado', 'Unlimited budget')
          : line.exceeded ? t(`Excedido em ${fmt(line.used - total)}`, `Over by ${fmt(line.used - total)}`)
          : t(`Restam ${fmt(total - line.used)}`, `${fmt(total - line.used)} left`)}
      </span>
    </div>
  );
}

export function BudgetBars({ report }: { report: NpcValidationReport }) {
  return (
    <div className={styles.budgets}>
      <Budget name="STATs" line={report.stats} />
      <Budget name="Perícias" line={report.skills} />
      <Budget name="Equipamento" line={report.gear} unit="eb" />
      <Budget name="Cyberware" line={report.cyberware} unit="eb" />
    </div>
  );
}

// ---------------------------------------------------------------- validation

const SEVERITY_LABEL = { Error: t('Erros impeditivos', 'Blocking errors'), Warning: t('Avisos', 'Warnings'), Info: t('Informações', 'Info') } as const;

export function StatusBanner({ report, pending }: { report: NpcValidationReport; pending: boolean }) {
  const [cls, icon, text] = !report.isValid
    ? [styles.statusInvalid, <AlertOctagon key="i" size={18} />, t(`Inválido · ${report.errorCount} erro(s)`, `Invalid · ${report.errorCount} error(s)`)]
    : report.status === 'ValidWithExceptions'
      ? [styles.statusExceptions, <ShieldAlert key="i" size={18} />, t('Válido com exceções do Mestre', 'Valid with GM exceptions')]
      : [styles.statusValid, <CheckCircle2 key="i" size={18} />, t('Mecanicamente válido', 'Mechanically valid')];
  return (
    <div className={clsx(styles.status, cls)} role="status">
      {icon}
      <span>{text}</span>
      {pending && <span className={styles.statusPending}><Spinner label={t('Validando', 'Validating')} /></span>}
    </div>
  );
}

export function ValidationPanel({
  report, onException, onRemoveException,
}: { report: NpcValidationReport; onException: (key: string, justification: string) => void; onRemoveException: (key: string) => void }) {
  const [showInfo, setShowInfo] = useState(false);
  const groups = (['Error', 'Warning', 'Info'] as const)
    .map((s) => ({ severity: s, issues: report.issues.filter((i) => i.severity === s) }))
    .filter((g) => g.issues.length > 0 && (g.severity !== 'Info' || showInfo));
  const infoCount = report.issues.filter((i) => i.severity === 'Info').length;

  return (
    <div className={styles.issues}>
      {groups.length === 0 && <p className={styles.muted}>{t('Nenhuma violação encontrada.', 'No violations found.')}</p>}
      {groups.map((g) => (
        <div key={g.severity} className={styles.issues}>
          <p className={styles.issueGroup}>{SEVERITY_LABEL[g.severity]} ({g.issues.length})</p>
          {g.issues.map((issue) => (
            <IssueRow key={`${issue.key}-${issue.message}`} issue={issue} onException={onException} onRemoveException={onRemoveException} />
          ))}
        </div>
      ))}
      {infoCount > 0 && (
        <Switch checked={showInfo} onChange={setShowInfo} label={t(`Mostrar informações (${infoCount})`, `Show info (${infoCount})`)} />
      )}
    </div>
  );
}

function IssueRow({
  issue, onException, onRemoveException,
}: { issue: NpcIssue; onException: (key: string, justification: string) => void; onRemoveException: (key: string) => void }) {
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState('');
  const Icon = issue.severity === 'Error' ? AlertOctagon : issue.severity === 'Warning' ? AlertTriangle : Info;

  return (
    <div className={clsx(styles.issue, styles[`issue_${issue.severity}`], issue.overridden && styles.issueOverridden)}>
      <div className={styles.issueHead}>
        <Icon size={15} aria-hidden />
        <span>{tv(issue.message)}</span>
      </div>
      {issue.suggestion && <span className={styles.issueMeta}><strong>{t('Sugestão:', 'Suggestion:')}</strong> {tv(issue.suggestion)}</span>}
      {issue.source && <span className={styles.issueMeta}><strong>{t('Fonte:', 'Source:')}</strong> {issue.source}</span>}
      {issue.severity === 'Error' && !issue.overridable && (
        <span className={styles.issueMeta}>{t('Regra mecânica: não aceita exceção.', 'Mechanical rule: no exceptions allowed.')}</span>
      )}
      {issue.overridden ? (
        <div className={styles.inline}>
          <Badge tone="amber">{t('Exceção do Mestre', 'GM exception')}</Badge>
          <span className={styles.issueMeta}>“{issue.justification}”</span>
          <Button variant="ghost" onClick={() => onRemoveException(issue.key)}>{t('Remover exceção', 'Remove exception')}</Button>
        </div>
      ) : issue.overridable && (editing ? (
        <div className={styles.exceptionForm}>
          <textarea value={text} onChange={(e) => setText(e.target.value)} maxLength={500} data-autofocus
            placeholder={t('Por que este NPC foge da orientação? (obrigatório)', 'Why does this NPC break the guideline? (required)')} aria-label={t('Justificativa da exceção', 'Reason for the exception')} />
          <div className={styles.inline}>
            <Button variant="primary" disabled={text.trim().length < 3}
              onClick={() => { onException(issue.key, text); setEditing(false); setText(''); }}>
              {t('Registrar exceção', 'Record exception')}
            </Button>
            <Button variant="ghost" onClick={() => setEditing(false)}>{t('Cancelar', 'Cancel')}</Button>
          </div>
        </div>
      ) : (
        <div>
          <Button variant="ghost" onClick={() => setEditing(true)}>{t('Aceitar como exceção do Mestre…', 'Accept as a GM exception…')}</Button>
        </div>
      ))}
    </div>
  );
}

export function ConflictsPanel({ conflicts }: { conflicts: GenerationConflict[] }) {
  if (conflicts.length === 0) return null;
  return (
    <div className={styles.conflicts} role="alert">
      <h3>{t('Não foi possível gerar o NPC', 'Couldn\'t generate the NPC')}</h3>
      <ul>
        {conflicts.map((c, i) => (
          <li key={`${c.code}-${i}`}>
            {tv(c.message)}
            {c.suggestion && <span className={styles.itemSub}>{t('Ajuste possível: ', 'Possible fix: ')}{tv(c.suggestion)}</span>}
            {c.source && <span className={styles.itemSub}>{t('Fonte: ', 'Source: ')}{c.source}</span>}
          </li>
        ))}
      </ul>
    </div>
  );
}

// ---------------------------------------------------------------- gangs

/** Who the gang is, how it looks and what the books say it carries. */
export function GangPanel({ gang, rules }: { gang: GangInfo; rules: NpcRules }) {
  const typical = gang.preferredProfiles
    .map((id) => rules.profiles.find((p) => p.id === id)?.label)
    .filter(Boolean)
    .slice(0, 3);
  return (
    <div className={styles.gangPanel}>
      <div className={styles.inline}>
        <span className={styles.gangName}>{tv(gang.label)}</span>
        <Badge tone="cyan">{tv(gang.kind)}</Badge>
        <Badge tone={gang.era === '2077' ? 'amber' : 'default'}>{ERA_LABEL[gang.era] ?? gang.era}</Badge>
      </div>
      <p className={styles.gangText}>{tv(gang.description)}</p>
      <p className={styles.gangAesthetic}><strong>{t('Estética.', 'Look.')}</strong> {gang.aesthetic}</p>
      <div className={styles.inline}>
        {gang.territory.length > 0 && <span className={styles.issueMeta}><strong>{t('Território:', 'Turf:')}</strong> {gang.territory.join(', ')}</span>}
        {gang.languages.length > 0 && <span className={styles.issueMeta}><strong>{t('Idiomas:', 'Languages:')}</strong> {gang.languages.join(', ')}</span>}
        {typical.length > 0 && <span className={styles.issueMeta}><strong>{t('Arquétipos típicos:', 'Typical archetypes:')}</strong> {typical.join(', ')}</span>}
      </div>
      <Collapsible title={t('Nos livros e no gerador', 'In the books and in the generator')}>
        <FactsTable facts={gang.facts} />
      </Collapsible>
    </div>
  );
}

// ---------------------------------------------------------------- sources

function FactsTable({ facts }: { facts: RuleFact[] }) {
  return (
    <div className={styles.tableWrap}>
      <table className={styles.table}>
        <thead><tr><th>{t('Parâmetro', 'Parameter')}</th><th>{t('Valor', 'Value')}</th><th>{t('Origem', 'Source')}</th></tr></thead>
        <tbody>
          {facts.map((f) => (
            <tr key={f.key}>
              <td>{tv(f.label)}</td>
              <td className={styles.itemName}>{tv(f.value)}</td>
              <td>
                {f.complementary
                  ? <><Badge tone="amber" title={t('Regra adotada pela aplicação, não presente nos livros', 'Rule adopted by the app, not in the books')}>{t('Complementar', 'Complementary')}</Badge> {tv(f.note)}</>
                  : <span className={styles.sourceTag}>{f.source}</span>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function RulesSources({ rules, sheet }: { rules: NpcRules; sheet: NpcSheet }) {
  const template = rules.templates.find((entry) => entry.level === sheet.level && entry.hardened === sheet.hardened);
  return (
    <Collapsible title={t('Fontes e parâmetros', 'Sources and parameters')}>
      <p className={styles.muted}>{rules.about}</p>
      <p className={styles.muted}>
        {Object.entries(rules.books).map(([k, v]) => `${k} = ${v}`).join(' · ')}
      </p>
      {template && (
        <>
          <p className={styles.issueGroup}>Template {tv(template.name)}</p>
          <FactsTable facts={template.facts} />
        </>
      )}
      <p className={styles.issueGroup}>{t('Regras gerais', 'General rules')}</p>
      <FactsTable facts={rules.generalRules} />
    </Collapsible>
  );
}
