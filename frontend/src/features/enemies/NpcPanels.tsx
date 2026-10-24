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

const LEVELS: NpcLevel[] = ['Mook', 'Lieutenant', 'MiniBoss', 'Boss'];

// ---------------------------------------------------------------- category

export function LevelPicker({
  rules, level, hardened, onChange,
}: { rules: NpcRules; level: NpcLevel; hardened: boolean; onChange: (level: NpcLevel, hardened: boolean) => void }) {
  return (
    <div className={styles.toolbarGroup}>
      <div className={styles.levels} role="radiogroup" aria-label="Categoria do inimigo">
        {LEVELS.map((l) => {
          const t = rules.templates.find((x) => x.level === l && x.hardened === hardened);
          return (
            <button key={l} type="button" role="radio" aria-checked={level === l}
              className={clsx(styles.level, level === l && styles.levelActive)} onClick={() => onChange(l, hardened)}>
              <span className={styles.levelName}>{LEVEL_LABEL[l]}</span>
              {t && (
                <>
                  <span className={styles.levelNumbers}>
                    {t.values.statPoints} STAT · máx {t.values.maxStat} · {t.values.skillPoints} perícia
                  </span>
                  <span className={styles.levelNumbers}>
                    {formatEb(t.values.gearBudget)}eb equip. · {t.values.cyberwareBudget === null ? 'cyber ilimitado' : `${formatEb(t.values.cyberwareBudget)}eb cyber`}
                  </span>
                  <span className={styles.levelSummary}>{t.summary}</span>
                </>
              )}
            </button>
          );
        })}
      </div>
      <Switch checked={hardened} onChange={(h) => onChange(level, h)}
        label="Hardened (para Crews otimizadas para combate, DGD p.150–152)" />
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
        {total === null ? 'Orçamento ilimitado'
          : line.exceeded ? `Excedido em ${fmt(line.used - total)}`
          : `Restam ${fmt(total - line.used)}`}
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

const SEVERITY_LABEL = { Error: 'Erros impeditivos', Warning: 'Avisos', Info: 'Informações' } as const;

export function StatusBanner({ report, pending }: { report: NpcValidationReport; pending: boolean }) {
  const [cls, icon, text] = !report.isValid
    ? [styles.statusInvalid, <AlertOctagon key="i" size={18} />, `Inválido · ${report.errorCount} erro(s)`]
    : report.status === 'ValidWithExceptions'
      ? [styles.statusExceptions, <ShieldAlert key="i" size={18} />, 'Válido com exceções do Mestre']
      : [styles.statusValid, <CheckCircle2 key="i" size={18} />, 'Mecanicamente válido'];
  return (
    <div className={clsx(styles.status, cls)} role="status">
      {icon}
      <span>{text}</span>
      {pending && <span className={styles.statusPending}><Spinner label="Validando" /></span>}
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
      {groups.length === 0 && <p className={styles.muted}>Nenhuma violação encontrada.</p>}
      {groups.map((g) => (
        <div key={g.severity} className={styles.issues}>
          <p className={styles.issueGroup}>{SEVERITY_LABEL[g.severity]} ({g.issues.length})</p>
          {g.issues.map((issue) => (
            <IssueRow key={`${issue.key}-${issue.message}`} issue={issue} onException={onException} onRemoveException={onRemoveException} />
          ))}
        </div>
      ))}
      {infoCount > 0 && (
        <Switch checked={showInfo} onChange={setShowInfo} label={`Mostrar informações (${infoCount})`} />
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
        <span>{issue.message}</span>
      </div>
      {issue.suggestion && <span className={styles.issueMeta}><strong>Sugestão:</strong> {issue.suggestion}</span>}
      {issue.source && <span className={styles.issueMeta}><strong>Fonte:</strong> {issue.source}</span>}
      {issue.severity === 'Error' && !issue.overridable && (
        <span className={styles.issueMeta}>Regra mecânica: não aceita exceção.</span>
      )}
      {issue.overridden ? (
        <div className={styles.inline}>
          <Badge tone="amber">Exceção do Mestre</Badge>
          <span className={styles.issueMeta}>“{issue.justification}”</span>
          <Button variant="ghost" onClick={() => onRemoveException(issue.key)}>Remover exceção</Button>
        </div>
      ) : issue.overridable && (editing ? (
        <div className={styles.exceptionForm}>
          <textarea value={text} onChange={(e) => setText(e.target.value)} maxLength={500} data-autofocus
            placeholder="Por que este NPC foge da orientação? (obrigatório)" aria-label="Justificativa da exceção" />
          <div className={styles.inline}>
            <Button variant="primary" disabled={text.trim().length < 3}
              onClick={() => { onException(issue.key, text); setEditing(false); setText(''); }}>
              Registrar exceção
            </Button>
            <Button variant="ghost" onClick={() => setEditing(false)}>Cancelar</Button>
          </div>
        </div>
      ) : (
        <div>
          <Button variant="ghost" onClick={() => setEditing(true)}>Aceitar como exceção do Mestre…</Button>
        </div>
      ))}
    </div>
  );
}

export function ConflictsPanel({ conflicts }: { conflicts: GenerationConflict[] }) {
  if (conflicts.length === 0) return null;
  return (
    <div className={styles.conflicts} role="alert">
      <h3>Não foi possível gerar o NPC</h3>
      <ul>
        {conflicts.map((c, i) => (
          <li key={`${c.code}-${i}`}>
            {c.message}
            {c.suggestion && <span className={styles.itemSub}>Ajuste possível: {c.suggestion}</span>}
            {c.source && <span className={styles.itemSub}>Fonte: {c.source}</span>}
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
        <span className={styles.gangName}>{gang.label}</span>
        <Badge tone="cyan">{gang.kind}</Badge>
        <Badge tone={gang.era === '2077' ? 'amber' : 'default'}>{ERA_LABEL[gang.era] ?? gang.era}</Badge>
      </div>
      <p className={styles.gangText}>{gang.description}</p>
      <p className={styles.gangAesthetic}><strong>Estética.</strong> {gang.aesthetic}</p>
      <div className={styles.inline}>
        {gang.territory.length > 0 && <span className={styles.issueMeta}><strong>Território:</strong> {gang.territory.join(', ')}</span>}
        {gang.languages.length > 0 && <span className={styles.issueMeta}><strong>Idiomas:</strong> {gang.languages.join(', ')}</span>}
        {typical.length > 0 && <span className={styles.issueMeta}><strong>Arquétipos típicos:</strong> {typical.join(', ')}</span>}
      </div>
      <Collapsible title="Nos livros e no gerador">
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
        <thead><tr><th>Parâmetro</th><th>Valor</th><th>Origem</th></tr></thead>
        <tbody>
          {facts.map((f) => (
            <tr key={f.key}>
              <td>{f.label}</td>
              <td className={styles.itemName}>{f.value}</td>
              <td>
                {f.complementary
                  ? <><Badge tone="amber" title="Regra adotada pela aplicação, não presente nos livros">Complementar</Badge> {f.note}</>
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
  const template = rules.templates.find((t) => t.level === sheet.level && t.hardened === sheet.hardened);
  return (
    <Collapsible title="Fontes e parâmetros">
      <p className={styles.muted}>{rules.about}</p>
      <p className={styles.muted}>
        {Object.entries(rules.books).map(([k, v]) => `${k} = ${v}`).join(' · ')}
      </p>
      {template && (
        <>
          <p className={styles.issueGroup}>Template {template.name}</p>
          <FactsTable facts={template.facts} />
        </>
      )}
      <p className={styles.issueGroup}>Regras gerais</p>
      <FactsTable facts={rules.generalRules} />
    </Collapsible>
  );
}
