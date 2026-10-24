import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { Search } from 'lucide-react';
import { useCategories } from '../../api/hooks';
import { Badge, Spinner } from '../../components/ui/ui';
import { CategoryIcon } from '../../components/art/CategoryIcon';
import { useCharacterRules } from '../../api/characters';
import type { CriticalInjuryRule } from '../../api/characterTypes';
import { CRITICAL_RULES } from './criticalInjuries';
import { useRangeUnit } from '../../lib/hooks';
import { AutofireTable, RangeUnitToggle, SingleShotTable } from './RangeTables';
import page from '../../styles/Page.module.css';
import styles from './ReferencePages.module.css';

export function QuickRulesPage() {
  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch="Regras Rápidas">Regras Rápidas</h1>
        <p className={page.lead}>Guia de leitura do catálogo e de uso dos filtros. Para regras completas, consulte sempre os livros oficiais.</p>
      </header>

      <div className={styles.cards}>
        <section className={styles.card}>
          <h2 className="hud-title">Como os filtros combinam</h2>
          <div className={page.prose}>
            <p>Critérios de campos diferentes são combinados com <strong>E</strong>: “SMG” + “ROF = 2” mostra só SMGs com ROF 2.</p>
            <p>Dentro de um mesmo campo de seleção, os valores marcados são alternativas (<strong>OU</strong>): “Pistola Média” ou “SMG”.
              Use o modo <strong>Excluir</strong> para retirar valores e, em Propriedades, <strong>Todos</strong> para exigir várias ao mesmo tempo.</p>
            <p>Campos numéricos aceitam =, ≠, &gt;, ≥, &lt;, ≤, intervalos (“entre”) e “sem valor”. Os números ao lado de cada opção mostram quantos itens você teria ao marcá-la.</p>
            <p>A URL guarda a busca: copie o endereço para compartilhar exatamente o mesmo filtro.</p>
          </div>
        </section>

        <section className={styles.card}>
          <h2 className="hud-title">Lendo um card</h2>
          <div className={page.prose}>
            <p><strong>Dano</strong> aparece como impresso (ex.: 3d6). O filtro “Dano (nº de d6)” usa o número de dados para comparações.</p>
            <p><strong>ROF</strong> é a cadência em tiro único; “Autofire” guarda o multiplicador quando a arma tem rajada.</p>
            <p><strong>Alcance</strong> é a maior faixa da tabela de alcance da classe da arma, derivada pelo catálogo — veja Tabelas.</p>
            <p><strong>HL</strong> é a perda de Humanidade média; os dados aparecem na ficha (ex.: 7 = 2d6).</p>
          </div>
        </section>

        <section className={styles.card}>
          <h2 className="hud-title">Origem dos dados</h2>
          <div className={page.prose}>
            <p><strong>Compêndio importado</strong>: registros convertidos do compêndio fornecido. Ainda não foram conferidos com os livros.</p>
            <p><strong>CONFERIDO</strong>: itens comparados com os PDFs oficiais (<em>Core Rulebook</em>, <em>Black Chrome</em>, <em>Black Chrome+</em>, <em>Interface RED</em> 2 a 5, <em>Edgerunners Mission Kit</em>, <em>Danger Gal Dossier</em>, <em>Tales of the RED</em>, <em>12 Days of</em> Gunmas, Cybermas, Gearmas, REDmas, Cutiemas e Vehiclemas, <em>Woodchipper&apos;s Garage</em>, <em>Toggle&apos;s Temple</em>, <em>Hornet&apos;s Pharmacy</em>, <em>All About Agents</em>, <em>Segotari Power</em>, <em>Going Quiet</em>, <em>Cyberware Enhancements</em>, <em>No Place Like Home</em>, <em>Mixing Drinks–Changing Lives</em> e <em>Invented Tech Upgrades</em>). A ficha mostra a página e, nas notas, o que mudou em relação ao compêndio.</p>
            <p><strong>DEMO</strong>: registros de demonstração. O catálogo não traz mais nenhum; a origem continua disponível para quem cadastrar exemplos na Administração.</p>
            <p><strong>Cadastro manual</strong>: itens criados na área de Administração.</p>
          </div>
        </section>

        <section className={styles.card}>
          <h2 className="hud-title">Qualidade das armas</h2>
          <div className={page.prose}>
            <p>Segundo as notas do compêndio, armas de <strong>Qualidade Excelente</strong> dão +1 nas rolagens de ataque e armas de <strong>Baixa Qualidade</strong> emperram com 1 no d10.</p>
            <p>Use o filtro “Qualidade” ou a propriedade correspondente para encontrá-las.</p>
          </div>
        </section>
      </div>
    </div>
  );
}

const PRICE_TABLE = [
  ['Cheap', '10'], ['Everyday', '20'], ['Costly', '50'], ['Premium', '100'], ['Expensive', '500'],
  ['Very Expensive', '1.000'], ['Luxury', '5.000'], ['Super Luxury', '10.000 ou mais'],
];

const CLASS_TABLE = [
  ['Pistola Média / SMG', 'Pistola Média', '200 m / 400 m', '100 / 200'],
  ['Pistola Pesada / SMG Pesada', 'Pistola Pesada', '200 m / 400 m', '100 / 200'],
  ['Pistola Muito Pesada', 'Pistola Muito Pesada', '200 m', '100'],
  ['Escopeta', 'Slug e Cartucho (Shell)', '200 m', '100'],
  ['Rifle de Assalto / Precisão / Metralhadora', 'Rifle', '800 m', '400'],
  ['Arco / Besta', 'Flecha / Dardo', '200 m', '100'],
  ['Lança-Granadas', 'Granada', '400 m', '200'],
  ['Lança-Foguetes', 'Foguete', '800 m', '400'],
  ['Corpo a corpo', '—', 'Contato', 'Adjacente'],
];

function CriticalTable({ title, rows }: { title: string; rows: CriticalInjuryRule[] }) {
  return (
    <div>
      <h3 className={styles.subTitle}>{title}</h3>
      <div className={page.tableWrap}>
        <table className={page.table}>
          <thead><tr><th>2d6</th><th>Lesão</th><th>Efeito</th><th>Quick Fix</th><th>Tratamento</th></tr></thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.roll}>
                <td className={styles.dv}>{r.roll}</td>
                <td className={styles.injuryName}>{r.name}</td>
                <td>
                  {r.effect}
                  {r.deathSave && <> <Badge tone="red" title="A penalidade base de Death Save aumenta em 1">Death Save +1</Badge></>}
                </td>
                <td>{r.quickFix ?? '—'}</td>
                <td>{r.treatment}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export function TablesPage() {
  const categories = useCategories();
  // The injury tables come from the rules file on the server, the same data the character sheet uses.
  const injuries = useCharacterRules().data?.criticalInjuries;
  const [unit, setUnit] = useRangeUnit();
  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch="Tabelas">Tabelas</h1>
        <p className={page.lead}>Referências usadas pelo catálogo e um resumo do que está cadastrado.</p>
      </header>

      <section className={page.section}>
        <h2 className="hud-title">Catálogo por categoria</h2>
        {categories.isLoading ? <Spinner /> : (
          <div className={styles.summary}>
            {categories.data?.map((c) => (
              <Link key={c.key} to={`/catalog/${c.key}`} className={styles.summaryItem}>
                <CategoryIcon icon={c.icon} size={22} />
                <span>{c.label}</span>
                <strong>{c.itemCount}</strong>
              </Link>
            ))}
          </div>
        )}
      </section>

      <section className={page.section}>
        <h2 className="hud-title">Categorias de preço (Cyberpunk RED)</h2>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>Categoria</th><th>Preço de referência (eb)</th></tr></thead>
            <tbody>{PRICE_TABLE.map(([a, b]) => <tr key={a}><td>{a}</td><td>{b}</td></tr>)}</tbody>
          </table>
        </div>
      </section>

      <section className={page.section}>
        <h2 className="hud-title">Alcance das armas (DV por distância)</h2>
        <p className={page.note}>
          Core Rulebook p.173. Em tiles, cada quadrado tem 2 m/yds, a mesma medida que o livro usa para áreas
          (cartucho de escopeta: 6 m = 3 quadrados; explosivos: 10 × 10 m = 5 × 5 quadrados, p.174). Cada faixa termina em metade
          do limite em metros, arredondado para baixo: 12 tiles (24 m) ainda é a faixa de 13–25 m.
        </p>
        <div className={styles.toolbar}><RangeUnitToggle unit={unit} onChange={setUnit} /></div>
        <h3 className={styles.subTitle}>Tiro único</h3>
        <SingleShotTable unit={unit} />
        <h3 className={styles.subTitle}>Autofire</h3>
        <AutofireTable unit={unit} />
        <ul className={styles.notes}>
          <li>Cartucho de escopeta (Shell): DV13 para atingir todos à frente até {unit === 'tiles' ? '3 tiles' : '6 m/yds'} (p.174).</li>
          <li>Explosivos acertam uma área de {unit === 'tiles' ? '5 × 5 tiles' : '10 × 10 m/yds'} centrada no alvo (p.174).</li>
          <li>Armas corpo a corpo atacam alvos adjacentes. Metralhadoras e armas exóticas de suplementos podem trazer regra própria na descrição.</li>
        </ul>
      </section>

      <section className={page.section}>
        <h2 className="hud-title">Critical Injuries</h2>
        <ul className={styles.notes}>
          {CRITICAL_RULES.map((rule) => <li key={rule}>{rule}</li>)}
          <li>Quick Fix e tratamento estão no Core p.223. Lesões marcadas com Death Save +1 aumentam a penalidade base de Death Save.</li>
        </ul>
        {!injuries ? <Spinner /> : (
          <div className={styles.critGrid}>
            <CriticalTable title="Corpo (Core p.187)" rows={injuries.filter((i) => i.location === 'Body')} />
            <CriticalTable title="Cabeça (Core p.188)" rows={injuries.filter((i) => i.location === 'Head')} />
          </div>
        )}
      </section>

      <section className={page.section}>
        <h2 className="hud-title">Valores derivados por classe de arma</h2>
        <p className={page.note}>
          O compêndio não traz munição compatível nem alcance por item. O catálogo deriva esses valores da classe da arma
          (com exceções para armas exóticas e combinadas, descritas nas notas). Confira na tabela de alcance do livro antes de usar em jogo.
        </p>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>Classe</th><th>Munição compatível</th><th>Alcance máx. usado no filtro</th><th>Em tiles (2 m)</th></tr></thead>
            <tbody>{CLASS_TABLE.map(([a, b, c, d]) => <tr key={a}><td>{a}</td><td>{b}</td><td>{c}</td><td>{d}</td></tr>)}</tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

const GLOSSARY: [string, string][] = [
  ['ACPA', 'Armadura de combate assistida (powered armor). No catálogo fica em Armaduras, com SP, HP estrutural e BODY.'],
  ['Autofire', 'Modo de rajada. O número (x3, x4...) é o multiplicador usado no ataque automático.'],
  ['Black ICE', 'Programas hostis que protegem redes e atacam Netrunners ou seus programas.'],
  ['BODY', 'Atributo físico. Algumas armas exigem BODY mínimo; Linear Frames e ACPAs concedem um valor de BODY.'],
  ['Borgware', 'Cyberware extremo, geralmente para conversões corporais pesadas.'],
  ['Chipware', 'Chips encaixados no Chipware Socket, sem cirurgia.'],
  ['Clínica / Hospital / Mall', 'Onde o cyberware é instalado; indica a complexidade da instalação.'],
  ['Concealable (Ocultável)', 'A arma pode ser escondida sob a roupa.'],
  ['Critical Injury', 'Lesão grave quando dois dados de dano saem 6: 5 de dano bônus direto nos HP e um efeito da tabela (página Tabelas).'],
  ['Fashionware', 'Cyberware estético (Chemskin, Techhair, Light Tattoo...). No catálogo fica em Cyberware, com o tipo Fashionware.'],
  ['DV', 'Difficulty Value: o número a superar num teste.'],
  ['Eddies (eb)', 'Eurodólares, a moeda de Night City.'],
  ['Foundational', 'Peça base de cyberware que oferece slots para opções (Cybereye, Neural Link, Cyberarm...).'],
  ['HL', 'Humanity Loss: perda de Humanidade ao instalar cyberware.'],
  ['IP de QG', 'Pontos de melhoria usados para instalar melhorias no quartel-general.'],
  ['Linear Frame', 'Exoesqueleto que eleva o BODY do usuário.'],
  ['Moto (Nomad)', 'Role Ability do Nomad. O rank define que veículos e upgrades ele pode tirar do Family Motorpool ("Nomad Access" nos suplementos).'],
  ['Netrunner', 'Hacker que invade redes com um Cyberdeck. Decks, Programas, Black ICE e Hardware ficam na aba Netrunning.'],
  ['ROF', 'Rate of Fire: quantos ataques a arma faz por ação em tiro único.'],
  ['SDP', 'Pontos de dano estrutural de veículos.'],
  ['Smartgun', 'Arma com link inteligente, usada com Interface Plugs ou Subdermal Grip.'],
  ['SP', 'Stopping Power: a proteção de uma armadura.'],
  ['Tile', 'Quadrado de 2 m/yds usado em mapas de batalha; é a medida que o Core usa para áreas (página Tabelas).'],
];

export function GlossaryPage() {
  const [query, setQuery] = useState('');
  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return q ? GLOSSARY.filter(([t, d]) => t.toLowerCase().includes(q) || d.toLowerCase().includes(q)) : GLOSSARY;
  }, [query]);

  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch="Glossário">Glossário</h1>
        <p className={page.lead}>Termos usados no catálogo.</p>
      </header>
      <label className={styles.search}>
        <Search size={16} />
        <input type="search" value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Filtrar termos..." aria-label="Filtrar termos" />
      </label>
      <dl className={styles.glossary}>
        {filtered.map(([term, definition]) => (
          <div key={term}>
            <dt>{term}</dt>
            <dd>{definition}</dd>
          </div>
        ))}
        {filtered.length === 0 && <p className={page.note}>Nenhum termo encontrado.</p>}
      </dl>
    </div>
  );
}
