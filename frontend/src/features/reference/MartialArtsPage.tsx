import { useMemo, useState } from 'react';
import clsx from 'clsx';
import { Search } from 'lucide-react';
import page from '../../styles/Page.module.css';
import refStyles from './ReferencePages.module.css';
import { useCharacterRules } from '../../api/characters';
import type { MartialArtForm, SpecialMove } from '../../api/characterTypes';
import { ErrorState, Spinner } from '../../components/ui/ui';
import { MOVE_KIND_LABEL } from './martialArts';
import styles from './MartialArtsPage.module.css';
import { t, tv } from '../../i18n';

const SOURCES = ['Core Rulebook', 'Interface RED 4', 'Interface RED 5'] as const;
const DAMAGE = [[t('4 ou menos', '4 or less'), '1d6'], ['5 a 6', '2d6'], ['7 a 10', '3d6'], [t('11 ou mais', '11 or more'), '4d6']];

const matches = (form: MartialArtForm, q: string) =>
  [form.name, form.description, ...form.moves.flatMap((m) => [m.name, m.requirement, m.effect])].some((entry) => entry.toLowerCase().includes(q));

function Move({ move }: { move: SpecialMove }) {
  return (
    <li className={styles.move}>
      <strong>{tv(move.name)}</strong>{move.kind !== 'check' && <span className={styles.source}> · {MOVE_KIND_LABEL[move.kind]}</span>}{move.dv != null && <span className={styles.source}> · DV{move.dv}</span>}
      <p className={styles.requirement}><span>{t('Requisito', 'Requirement')}</span> {tv(move.requirement)}</p>
      <p>{tv(move.effect)}</p>
    </li>
  );
}

// The forms come from the server rules (martial-arts-rules.json), the same ones the table uses for the moves.
export function MartialArtsPage() {
  const rules = useCharacterRules();
  const [query, setQuery] = useState('');
  const [source, setSource] = useState<string | null>(null);
  const all = rules.data?.martialArts;
  const shared = rules.data?.sharedMoves ?? [];
  const forms = useMemo(() => {
    const q = query.trim().toLowerCase();
    return (all ?? []).filter((f) => (!source || f.source === source) && (!q || matches(f, q)));
  }, [all, query, source]);
  if (rules.isLoading) return <div className={page.page}><Spinner /></div>;
  if (rules.error) return <div className={page.page}><ErrorState error={rules.error} onRetry={() => rules.refetch()} /></div>;

  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch={t('Artes Marciais', 'Martial Arts')}>{t('Artes Marciais', 'Martial Arts')}</h1>
        <p className={page.lead}>{t('Estilos de Martial Arts e seus Special Moves, do Core Rulebook e dos Interface RED 4 e 5.', 'Martial Arts forms and their Special Moves, from the Core Rulebook and Interface RED 4 and 5.')}</p>
      </header>

      <section className={styles.rules}>
        <div className={page.prose}>
          <p><strong>Martial Arts</strong> {t('é uma perícia de DEX com custo x2. Cada estilo é uma perícia separada, e é preciso ter ao menos 1 ponto nela para usar ataques e Special Moves.', 'is a DEX skill with x2 cost. Each form is a separate skill, and you need at least 1 point in it to use its attacks and Special Moves.')}</p>
          <p>{t('Ataques de Martial Arts têm ', 'Martial Arts attacks have ')}<strong>ROF 2</strong> e <strong>{t('ignoram metade da armadura', 'ignore half the armor')}</strong> {t('(arredondando para cima). Ataque e Special Moves usam ', '(rounding up). Attacks and Special Moves use ')}<strong>{t('DEX + estilo + 1d10', 'DEX + form + 1d10')}</strong> {t('contra DEX + Evasion + 1d10 do defensor, ou contra a DV do movimento. Special Moves não fazem Aimed Shot, salvo indicação.', 'against the defender\'s DEX + Evasion + 1d10, or against the move\'s DV. Special Moves can\'t be Aimed Shots unless noted.')}</p>
        </div>
        <table className={clsx(page.table, styles.damage)}>
          <thead><tr><th>BODY</th><th>{t('Dano', 'Damage')}</th></tr></thead>
          <tbody>{DAMAGE.map(([body, dmg]) => <tr key={body}><td>{body}</td><td>{dmg}</td></tr>)}</tbody>
        </table>
        <div className={styles.shared}>
          <h2 className="hud-title">{t('Movimento compartilhado', 'Shared move')}</h2>
          <ul className={styles.moves}>{shared.map((m) => <Move key={m.id} move={m} />)}</ul>
          <p className={page.note}>{t('Core Rulebook, p. 178-179.', 'Core Rulebook, p. 178-179.')}</p>
        </div>
      </section>

      <div className={styles.toolbar}>
        <label className={refStyles.search}>
          <Search size={16} />
          <input type="search" value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t('Buscar estilo ou movimento...', 'Search form or move...')} aria-label={t('Buscar estilo ou movimento', 'Search form or move')} />
        </label>
        <div className={styles.chips} role="group" aria-label={t('Filtrar por livro', 'Filter by book')}>
          <button type="button" className={clsx(styles.chip, !source && styles.chipActive)} aria-pressed={!source} onClick={() => setSource(null)}>
            {t('Todos ', 'All ')}<span>{all?.length ?? 0}</span>
          </button>
          {SOURCES.map((s) => (
            <button key={s} type="button" className={clsx(styles.chip, source === s && styles.chipActive)} aria-pressed={source === s}
              onClick={() => setSource(source === s ? null : s)}>
              {s} <span>{(all ?? []).filter((f) => f.source === s).length}</span>
            </button>
          ))}
        </div>
      </div>

      <div className={styles.grid}>
        {forms.map((form) => (
          <article key={form.name} className={styles.form}>
            <header className={styles.formHeader}>
              <h2>{tv(form.name)}</h2>
              <span className={styles.source}>{form.source}, p. {form.page}</span>
            </header>
            {form.innate && <p className={styles.innate}>{t('Inata de NPC: não pode ser aprendida.', 'NPC-innate: can\'t be learned.')}</p>}
            <p className={styles.description}>{tv(form.description)}</p>
            <ul className={styles.moves}>{form.moves.map((m) => <Move key={m.id} move={m} />)}</ul>
          </article>
        ))}
        {forms.length === 0 && <p className={page.note}>{t('Nenhum estilo encontrado.', 'No form found.')}</p>}
      </div>
    </div>
  );
}
