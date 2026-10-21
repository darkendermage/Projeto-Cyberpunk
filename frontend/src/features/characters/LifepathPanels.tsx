// Lifepath panels shared by the Caminho de Vida tab and the Lifepath step of character creation.
import { Briefcase, Dices, Plus, Trash2, Users } from 'lucide-react';
import type { CharacterRules, Relationship } from '../../api/characterTypes';
import { Button, ComboBox, IconButton, Select } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import { SheetPanel } from './CharacterUi';
import {
  LIFEPATH_GROUPS, RELATIONSHIP_SECTIONS, rollMissing, rollOption, rollRelationship, rollRelationshipCount, STYLE_TABLES, tempId,
  visibleQuestions,
} from './lifepath';
import type { SheetEditor } from './useSheetEditor';
import styles from './Characters.module.css';

// ------------------------------------------------------------ a table answer: pick, roll or write

export function TableField({ id, label, options, value, onChange, dice, hint }: {
  id: string; label: string; options: readonly string[]; value: string | null | undefined; onChange: (v: string) => void; dice?: string; hint?: string;
}) {
  return (
    <div className={styles.tableField}>
      <label className={styles.idLabel} htmlFor={id} title={hint}>{label}</label>
      <div className={styles.tableFieldInput}>
        <ComboBox id={id} value={value ?? ''} onChange={onChange} options={options} />
        {dice !== 'choose' && options.length > 0 && (
          <IconButton label={`Rolar ${dice ?? '1d10'}: ${label}`} onClick={() => onChange(rollOption(options))}><Dices size={14} /></IconButton>
        )}
      </div>
    </div>
  );
}

export function TablesPanel({ rules, editor }: { rules: CharacterRules; editor: SheetEditor }) {
  const answers = editor.draft.lifepath.answers;
  const set = (id: string, value: string) =>
    editor.update((s) => ({ ...s, lifepath: { ...s.lifepath, answers: { ...s.lifepath.answers, [id]: value } } }));
  return (
    <>
      {LIFEPATH_GROUPS.map((g) => (
        <SheetPanel key={g.title} title={g.title} actions={<span className={styles.source}>{rules.relations.source}</span>}>
          <div className={styles.fieldGrid}>
            {g.tables.map((id) => {
              const t = rules.lifepathTables.find((x) => x.id === id);
              if (!t) return null;
              return <TableField key={id} id={`lp-${id}`} label={t.labelPt} hint={`${t.label} · CP:R p.${t.page}`} dice={t.dice}
                options={t.options} value={answers[id]} onChange={(v) => set(id, v)} />;
            })}
          </div>
        </SheetPanel>
      ))}
    </>
  );
}

// ------------------------------------------------------------ relationships

export function RelationsPanel({ rules, editor }: { rules: CharacterRules; editor: SheetEditor }) {
  const toast = useToast();
  const all = editor.draft.lifepath.relationships;
  const r = rules.relations;
  const setAll = (next: Relationship[]) => editor.update((s) => ({ ...s, lifepath: { ...s.lifepath, relationships: next } }));
  return (
    <SheetPanel title="Relações" icon={<Users size={16} />} actions={<span className={styles.source}>CP:R p.51–52</span>}>
      {RELATIONSHIP_SECTIONS.map((section) => {
        const list = all.filter((x) => x.kind === section.kind);
        const canRoll = section.kind === 'Friend' || section.kind === 'Enemy' || section.kind === 'Lover';
        return (
          <div key={section.kind} className={styles.column} style={{ gap: 6 }}>
            <div className={styles.row}>
              <h4 className={`${styles.idLabel} ${styles.grow}`} style={{ margin: 0 }}>{section.title} ({list.length})</h4>
              {canRoll && (
                <Button variant="ghost" icon={<Dices size={14} />} title="Rola 1d10 − 7 (mínimo 0) e sorteia cada um"
                  onClick={() => {
                    const n = rollRelationshipCount();
                    if (n === 0) return toast.notify(`${section.title}: 1d10 − 7 deu zero desta vez.`, 'info');
                    setAll([...all, ...Array.from({ length: n }, () => rollRelationship(section.kind, r))]);
                  }}>Rolar</Button>
              )}
              <IconButton label={`Adicionar em ${section.title}`} onClick={() => setAll([...all, { id: tempId(), kind: section.kind, name: '' }])}><Plus size={14} /></IconButton>
            </div>
            {list.length === 0 && <span className={`${styles.small} ${styles.muted}`}>{section.empty}</span>}
            {list.map((rel) => (
              <RelationshipCard key={rel.id} rel={rel} rules={rules}
                onChange={(next) => setAll(all.map((x) => (x.id === rel.id ? next : x)))}
                onRemove={() => setAll(all.filter((x) => x.id !== rel.id))} />
            ))}
          </div>
        );
      })}
    </SheetPanel>
  );
}

function RelationshipCard({ rel, rules, onChange, onRemove }: {
  rel: Relationship; rules: CharacterRules; onChange: (r: Relationship) => void; onRemove: () => void;
}) {
  const r = rules.relations;
  const field = (key: keyof Relationship, label: string, options: readonly string[] = []) => (
    <TableField id={`rel-${rel.id}-${key}`} label={label} options={options} value={rel[key] as string | null}
      onChange={(v) => onChange({ ...rel, [key]: v })} dice={options.length ? '1d10' : 'choose'} />
  );
  return (
    <div className={styles.relation} data-kind={rel.kind}>
      <div className={styles.relationHead}>
        <input className={styles.input} value={rel.name} placeholder="Nome" aria-label="Nome" onChange={(e) => onChange({ ...rel, name: e.target.value })} />
        <IconButton label={`Remover ${rel.name || 'registro'}`} onClick={onRemove}><Trash2 size={14} /></IconButton>
      </div>
      {rel.kind === 'Friend' && field('relation', 'Relação com você', r.friendRelationship)}
      {rel.kind === 'Lover' && field('outcome', 'Como terminou', r.loveOutcome)}
      {rel.kind === 'Enemy' && (
        <>
          {field('relation', 'Quem é', r.enemyWho)}
          {field('cause', 'O que causou', r.enemyCause)}
          <div className={styles.tableField}>
            <span className={styles.idLabel}>Quem foi prejudicado</span>
            <Select value={rel.wronged ?? ''} onChange={(e) => onChange({ ...rel, wronged: e.target.value || null })} aria-label="Quem foi prejudicado">
              <option value="">—</option>
              <option value="Você">Você</option>
              <option value="Ele(a)">A outra pessoa</option>
              <option value="Ambos">Ambos</option>
            </Select>
          </div>
          {field('resources', 'O que pode jogar contra você', r.enemyResources)}
          {field('intent', 'O que vai fazer', r.enemyRevenge)}
        </>
      )}
      {(rel.kind === 'Ally' || rel.kind === 'Contact') && (
        <>
          {field('relation', 'Quem é')}
          {field('resources', 'Recursos e influência')}
          {field('intent', 'Objetivos')}
        </>
      )}
      <input className={styles.input} value={rel.notes ?? ''} placeholder="Observações" aria-label="Observações" onChange={(e) => onChange({ ...rel, notes: e.target.value })} />
    </div>
  );
}

// ------------------------------------------------------------ Role-based Lifepath

/**
 * One Role-based Lifepath per Role the character has, oldest first. The book only runs it at creation (CP:R p.53) and
 * says nothing about it when multiclassing (p.143): the creation one stays, a later Role's is optional (complementary rule).
 */
export function RoleLifepathPanels({ rules, editor, onlyCurrent }: { rules: CharacterRules; editor: SheetEditor; onlyCurrent?: boolean }) {
  const roles = onlyCurrent ? editor.draft.roles.slice(0, 1) : [...editor.draft.roles].reverse();
  const fact = rules.facts.find((f) => f.key === 'roleLifepath');
  return (
    <>
      {roles.map((r, i) => {
        const role = rules.roles.find((x) => x.id === r.role);
        return role && <RoleLifepathPanel key={role.id} role={role} editor={editor} later={i > 0} note={fact?.note ?? null} />;
      })}
    </>
  );
}

function RoleLifepathPanel({ role, editor, later, note }: { role: CharacterRules['roles'][number]; editor: SheetEditor; later: boolean; note: string | null }) {
  const answers = editor.draft.roleLifepath[role.id] ?? {};
  const set = (id: string, value: string) => editor.update((s) => ({
    ...s, roleLifepath: { ...s.roleLifepath, [role.id]: { ...(s.roleLifepath[role.id] ?? {}), [id]: value } },
  }));
  return (
    <SheetPanel title={`Caminho de vida: ${role.id}`} icon={<Briefcase size={16} />} actions={<span className={styles.source}>{role.lifepathSource}</span>}>
      {later && (
        <p className={styles.small} title={note ?? undefined}>
          Role assumida por multiclasse. Preencher é opcional: o livro não define Lifepath para isso.{' '}
          <span className={styles.source}>Regra complementar</span>
        </p>
      )}
      {visibleQuestions(role.lifepath, answers).map((q) => (
        q.dice === 'choose'
          ? (
            <div key={q.id} className={styles.tableField}>
              <span className={styles.idLabel} title={q.label}>{q.labelPt}</span>
              <div className={styles.optionGroup} role="radiogroup" aria-label={q.labelPt}>
                {q.options.map((o) => (
                  <button key={o} type="button" role="radio" aria-checked={answers[q.id] === o}
                    className={`${styles.choice} ${answers[q.id] === o ? styles.choiceActive : ''}`} style={{ padding: '4px 10px' }}
                    onClick={() => set(q.id, o)}>{o}</button>
                ))}
              </div>
            </div>
          )
          : <TableField key={q.id} id={`rl-${role.id}-${q.id}`} label={q.labelPt} hint={q.label} dice={q.dice} options={q.options}
            value={answers[q.id]} onChange={(v) => set(q.id, v)} />
      ))}
    </SheetPanel>
  );
}

/** The three style tables of the Lifepath (clothing, hair, affectation, CP:R p.47), stored in the style section. */
export function StyleTablesFields({ rules, editor }: { rules: CharacterRules; editor: SheetEditor }) {
  const style = editor.draft.style;
  return (
    <>
      {STYLE_TABLES.map((id) => {
        const t = rules.lifepathTables.find((x) => x.id === id);
        if (!t) return null;
        return <TableField key={id} id={`st-${id}`} label={t.labelPt} hint={`${t.label} · CP:R p.${t.page}`} dice={t.dice} options={t.options}
          value={style[id]} onChange={(v) => editor.update((s) => ({ ...s, style: { ...s.style, [id]: v } }))} />;
      })}
    </>
  );
}

/** Rolls every empty Lifepath answer at once, keeping what the player already chose. */
export function RollMissingButton({ rules, editor }: { rules: CharacterRules; editor: SheetEditor }) {
  const toast = useToast();
  return (
    <Button icon={<Dices size={15} />} title="Rola só as tabelas em branco; o que já foi escolhido fica"
      onClick={() => {
        const { sheet, filled } = rollMissing(editor.draft, rules, editor.draft.roles[0]?.role ?? null);
        if (filled === 0) return toast.notify('Nada faltando: todas as tabelas roláveis já têm resposta.', 'info');
        editor.update(() => sheet);
        toast.notify(`${filled} ${filled === 1 ? 'resposta rolada' : 'respostas roladas'}. Ajuste o que quiser.`);
      }}>
      Rolar o que falta
    </Button>
  );
}

