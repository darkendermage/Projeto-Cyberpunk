import { useMemo, useState } from 'react';
import clsx from 'clsx';
import { Search } from 'lucide-react';
import page from '../../styles/Page.module.css';
import refStyles from './ReferencePages.module.css';
import { MARTIAL_ARTS, SHARED_MOVES, type MartialArtForm, type SpecialMove } from './martialArts';
import styles from './MartialArtsPage.module.css';

const SOURCES = ['Core Rulebook', 'Interface RED 4', 'Interface RED 5'] as const;
const DAMAGE = [['4 ou menos', '1d6'], ['5 a 6', '2d6'], ['7 a 10', '3d6'], ['11 ou mais', '4d6']];

const matches = (form: MartialArtForm, q: string) =>
  [form.name, form.description, ...form.moves.flatMap((m) => [m.name, m.requirement, m.effect])].some((t) => t.toLowerCase().includes(q));

function Move({ move }: { move: SpecialMove }) {
  return (
    <li className={styles.move}>
      <strong>{move.name}</strong>
      <p className={styles.requirement}><span>Requisito</span> {move.requirement}</p>
      <p>{move.effect}</p>
    </li>
  );
}

export function MartialArtsPage() {
  const [query, setQuery] = useState('');
  const [source, setSource] = useState<string | null>(null);
  const forms = useMemo(() => {
    const q = query.trim().toLowerCase();
    return MARTIAL_ARTS.filter((f) => (!source || f.source === source) && (!q || matches(f, q)));
  }, [query, source]);

  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch="Artes Marciais">Artes Marciais</h1>
        <p className={page.lead}>Estilos de Martial Arts e seus Special Moves, do Core Rulebook e dos Interface RED 4 e 5.</p>
      </header>

      <section className={styles.rules}>
        <div className={page.prose}>
          <p><strong>Martial Arts</strong> é uma perícia de DEX com custo x2. Cada estilo é uma perícia separada, e é preciso ter ao menos 1 ponto nela para usar ataques e Special Moves.</p>
          <p>Ataques de Martial Arts têm <strong>ROF 2</strong> e <strong>ignoram metade da armadura</strong> (arredondando para cima). Ataque e Special Moves usam <strong>DEX + estilo + 1d10</strong> contra DEX + Evasion + 1d10 do defensor, ou contra a DV do movimento. Special Moves não fazem Aimed Shot, salvo indicação.</p>
        </div>
        <table className={clsx(page.table, styles.damage)}>
          <thead><tr><th>BODY</th><th>Dano</th></tr></thead>
          <tbody>{DAMAGE.map(([body, dmg]) => <tr key={body}><td>{body}</td><td>{dmg}</td></tr>)}</tbody>
        </table>
        <div className={styles.shared}>
          <h2 className="hud-title">Movimento compartilhado</h2>
          <ul className={styles.moves}>{SHARED_MOVES.map((m) => <Move key={m.name} move={m} />)}</ul>
          <p className={page.note}>Core Rulebook, p. 178-179.</p>
        </div>
      </section>

      <div className={styles.toolbar}>
        <label className={refStyles.search}>
          <Search size={16} />
          <input type="search" value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Buscar estilo ou movimento..." aria-label="Buscar estilo ou movimento" />
        </label>
        <div className={styles.chips} role="group" aria-label="Filtrar por livro">
          <button type="button" className={clsx(styles.chip, !source && styles.chipActive)} aria-pressed={!source} onClick={() => setSource(null)}>
            Todos <span>{MARTIAL_ARTS.length}</span>
          </button>
          {SOURCES.map((s) => (
            <button key={s} type="button" className={clsx(styles.chip, source === s && styles.chipActive)} aria-pressed={source === s}
              onClick={() => setSource(source === s ? null : s)}>
              {s} <span>{MARTIAL_ARTS.filter((f) => f.source === s).length}</span>
            </button>
          ))}
        </div>
      </div>

      <div className={styles.grid}>
        {forms.map((form) => (
          <article key={form.name} className={styles.form}>
            <header className={styles.formHeader}>
              <h2>{form.name}</h2>
              <span className={styles.source}>{form.source}, p. {form.page}</span>
            </header>
            {form.innate && <p className={styles.innate}>Inata de NPC: não pode ser aprendida.</p>}
            <p className={styles.description}>{form.description}</p>
            <ul className={styles.moves}>{form.moves.map((m) => <Move key={m.name} move={m} />)}</ul>
          </article>
        ))}
        {forms.length === 0 && <p className={page.note}>Nenhum estilo encontrado.</p>}
      </div>
    </div>
  );
}
