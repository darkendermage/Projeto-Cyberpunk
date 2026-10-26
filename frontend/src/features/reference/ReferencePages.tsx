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
import { t, tv } from '../../i18n';

export function QuickRulesPage() {
  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch={t('Regras Rápidas', 'Quick Rules')}>{t('Regras Rápidas', 'Quick Rules')}</h1>
        <p className={page.lead}>{t('Guia de leitura do catálogo e de uso dos filtros. Para regras completas, consulte sempre os livros oficiais.', 'A guide to reading the catalog and using the filters. For the full rules, always check the official books.')}</p>
      </header>

      <div className={styles.cards}>
        <section className={styles.card}>
          <h2 className="hud-title">{t('Como os filtros combinam', 'How filters combine')}</h2>
          <div className={page.prose}>
            <p>{t('Critérios de campos diferentes são combinados com ', 'Criteria from different fields are combined with ')}<strong>E</strong>{t(': “SMG” + “ROF = 2” mostra só SMGs com ROF 2.', ': “SMG” + “ROF = 2” shows only SMGs with ROF 2.')}</p>
            <p>{t('Dentro de um mesmo campo de seleção, os valores marcados são alternativas (', 'Within the same selection field, the checked values are alternatives (')}<strong>{t('OU', 'OR')}</strong>{t('): “Pistola Média” ou “SMG”. Use o modo ', '): “Medium Pistol” or “SMG”. Use the ')}<strong>{t('Excluir', 'Exclude')}</strong> {t('para retirar valores e, em Propriedades, ', 'mode to remove values and, in Properties, ')}<strong>{t('Todos', 'All')}</strong> {t('para exigir várias ao mesmo tempo.', 'to require several at once.')}</p>
            <p>{t('Campos numéricos aceitam =, ≠, >, ≥, <, ≤, intervalos (“entre”) e “sem valor”. Os números ao lado de cada opção mostram quantos itens você teria ao marcá-la.', 'Numeric fields take =, ≠, >, ≥, <, ≤, ranges (“between”) and “no value”. The numbers next to each option show how many items you\'d get by checking it.')}</p>
            <p>{t('A URL guarda a busca: copie o endereço para compartilhar exatamente o mesmo filtro.', 'The URL keeps the search: copy the address to share exactly the same filter.')}</p>
          </div>
        </section>

        <section className={styles.card}>
          <h2 className="hud-title">{t('Lendo um card', 'Reading a card')}</h2>
          <div className={page.prose}>
            <p><strong>{t('Dano', 'Damage')}</strong> {t('aparece como impresso (ex.: 3d6). O filtro “Dano (nº de d6)” usa o número de dados para comparações.', 'shows as printed (e.g. 3d6). The “Damage (no. of d6)” filter uses the number of dice for comparisons.')}</p>
            <p><strong>ROF</strong> {t('é a cadência em tiro único; “Autofire” guarda o multiplicador quando a arma tem rajada.', 'is the single-shot rate of fire; “Autofire” holds the multiplier when the weapon can fire bursts.')}</p>
            <p><strong>{t('Alcance', 'Range')}</strong> {t('é a maior faixa da tabela de alcance da classe da arma, derivada pelo catálogo — veja Tabelas.', 'is the longest band of the range table for the weapon class, derived by the catalog — see Tables.')}</p>
            <p><strong>HL</strong> {t('é a perda de Humanidade média; os dados aparecem na ficha (ex.: 7 = 2d6).', 'is the average Humanity Loss; the dice show on the item page (e.g. 7 = 2d6).')}</p>
          </div>
        </section>

        <section className={styles.card}>
          <h2 className="hud-title">{t('Origem dos dados', 'Data origin')}</h2>
          <div className={page.prose}>
            <p><strong>{t('Compêndio importado', 'Imported compendium')}</strong>{t(': registros convertidos do compêndio fornecido. Ainda não foram conferidos com os livros.', ': records converted from the provided compendium. Not yet checked against the books.')}</p>
            <p><strong>{t('CONFERIDO', 'CHECKED')}</strong>{t(': itens comparados com os PDFs oficiais (', ': items compared with the official PDFs (')}<em>Core Rulebook</em>, <em>Black Chrome</em>, <em>Black Chrome+</em>, <em>Interface RED</em> 2 a 5, <em>Edgerunners Mission Kit</em>, <em>Danger Gal Dossier</em>, <em>Tales of the RED</em>, <em>12 Days of</em> {t('Gunmas, Cybermas, Gearmas, REDmas, Cutiemas e Vehiclemas, ', 'Gunmas, Cybermas, Gearmas, REDmas, Cutiemas and Vehiclemas, ')}<em>Woodchipper&apos;s Garage</em>, <em>Toggle&apos;s Temple</em>, <em>Hornet&apos;s Pharmacy</em>, <em>All About Agents</em>, <em>Segotari Power</em>, <em>Going Quiet</em>, <em>Cyberware Enhancements</em>, <em>No Place Like Home</em>, <em>Mixing Drinks–Changing Lives</em> e <em>Invented Tech Upgrades</em>{t('). A ficha mostra a página e, nas notas, o que mudou em relação ao compêndio.', '). The item page shows the page and, in the notes, what changed from the compendium.')}</p>
            <p><strong>DEMO</strong> e <strong>{t('Cadastro manual', 'Manual entry')}</strong>{t(': registros criados em versões antigas do programa, que tinham uma área de Administração. O catálogo atual não traz nenhum, mas eles continuam aparecendo se o seu banco ainda os tiver.', ': records created in old versions of the program, which had an Administration area. The current catalog has none, but they still show up if your database has them.')}</p>
          </div>
        </section>

        <section className={styles.card}>
          <h2 className="hud-title">{t('Qualidade das armas', 'Weapon quality')}</h2>
          <div className={page.prose}>
            <p>{t('Segundo as notas do compêndio, armas de ', 'According to the compendium notes, ')}<strong>{t('Qualidade Excelente', 'Excellent Quality')}</strong> {t('dão +1 nas rolagens de ataque e armas de ', 'weapons give +1 to attack rolls and ')}<strong>{t('Baixa Qualidade', 'Poor Quality')}</strong> {t('emperram com 1 no d10.', 'weapons jam on a 1 on the d10.')}</p>
            <p>{t('Use o filtro “Qualidade” ou a propriedade correspondente para encontrá-las.', 'Use the “Quality” filter or the matching property to find them.')}</p>
          </div>
        </section>
      </div>
    </div>
  );
}

const PRICE_TABLE = [
  ['Cheap', '10'], ['Everyday', '20'], ['Costly', '50'], ['Premium', '100'], ['Expensive', '500'],
  ['Very Expensive', t('1.000', '1,000')], ['Luxury', t('5.000', '5,000')], ['Super Luxury', t('10.000 ou mais', '10,000 or more')],
];

const CLASS_TABLE = [
  [t('Pistola Média / SMG', 'Medium Pistol / SMG'), t('Pistola Média', 'Medium Pistol'), '200 m / 400 m', '100 / 200'],
  [t('Pistola Pesada / SMG Pesada', 'Heavy Pistol / Heavy SMG'), t('Pistola Pesada', 'Heavy Pistol'), '200 m / 400 m', '100 / 200'],
  [t('Pistola Muito Pesada', 'Very Heavy Pistol'), t('Pistola Muito Pesada', 'Very Heavy Pistol'), '200 m', '100'],
  [t('Escopeta', 'Shotgun'), t('Slug e Cartucho (Shell)', 'Slug and Shell'), '200 m', '100'],
  [t('Rifle de Assalto / Precisão / Metralhadora', 'Assault / Sniper Rifle / Machine Gun'), 'Rifle', '800 m', '400'],
  [t('Arco / Besta', 'Bow / Crossbow'), t('Flecha / Dardo', 'Arrow / Bolt'), '200 m', '100'],
  [t('Lança-Granadas', 'Grenade Launcher'), t('Granada', 'Grenade'), '400 m', '200'],
  [t('Lança-Foguetes', 'Rocket Launcher'), t('Foguete', 'Rocket'), '800 m', '400'],
  [t('Corpo a corpo', 'Melee'), '—', t('Contato', 'Contact'), t('Adjacente', 'Adjacent')],
];

function CriticalTable({ title, rows }: { title: string; rows: CriticalInjuryRule[] }) {
  return (
    <div>
      <h3 className={styles.subTitle}>{title}</h3>
      <div className={page.tableWrap}>
        <table className={page.table}>
          <thead><tr><th>2d6</th><th>{t('Lesão', 'Injury')}</th><th>{t('Efeito', 'Effect')}</th><th>Quick Fix</th><th>{t('Tratamento', 'Treatment')}</th></tr></thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.roll}>
                <td className={styles.dv}>{r.roll}</td>
                <td className={styles.injuryName}>{tv(r.name)}</td>
                <td>
                  {tv(r.effect)}
                  {r.deathSave && <> <Badge tone="red" title={t('A penalidade base de Death Save aumenta em 1', 'The base Death Save penalty goes up by 1')}>Death Save +1</Badge></>}
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
        <h1 className="hud-title" data-glitch={t('Tabelas', 'Tables')}>{t('Tabelas', 'Tables')}</h1>
        <p className={page.lead}>{t('Referências usadas pelo catálogo e um resumo do que está cadastrado.', 'References used by the catalog and a summary of what\'s in it.')}</p>
      </header>

      <section className={page.section}>
        <h2 className="hud-title">{t('Catálogo por categoria', 'Catalog by category')}</h2>
        {categories.isLoading ? <Spinner /> : (
          <div className={styles.summary}>
            {categories.data?.map((c) => (
              <Link key={c.key} to={`/catalog/${c.key}`} className={styles.summaryItem}>
                <CategoryIcon icon={c.icon} size={22} />
                <span>{tv(c.label)}</span>
                <strong>{c.itemCount}</strong>
              </Link>
            ))}
          </div>
        )}
      </section>

      <section className={page.section}>
        <h2 className="hud-title">{t('Categorias de preço (Cyberpunk RED)', 'Price categories (Cyberpunk RED)')}</h2>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>{t('Categoria', 'Category')}</th><th>{t('Preço de referência (eb)', 'Reference price (eb)')}</th></tr></thead>
            <tbody>{PRICE_TABLE.map(([a, b]) => <tr key={a}><td>{a}</td><td>{b}</td></tr>)}</tbody>
          </table>
        </div>
      </section>

      <section className={page.section}>
        <h2 className="hud-title">{t('Alcance das armas (DV por distância)', 'Weapon range (DV by distance)')}</h2>
        <p className={page.note}>
          {t('Core Rulebook p.173. Em tiles, cada quadrado tem 2 m/yds, a mesma medida que o livro usa para áreas (cartucho de escopeta: 6 m = 3 quadrados; explosivos: 10 × 10 m = 5 × 5 quadrados, p.174). Cada faixa termina em metade do limite em metros, arredondado para baixo: 12 tiles (24 m) ainda é a faixa de 13–25 m.', 'Core Rulebook p.173. In tiles, each square is 2 m/yds, the same measure the book uses for areas (shotgun shell: 6 m = 3 squares; explosives: 10 × 10 m = 5 × 5 squares, p.174). Each band ends at half the limit in meters, rounded down: 12 tiles (24 m) is still the 13–25 m band.')}
        </p>
        <div className={styles.toolbar}><RangeUnitToggle unit={unit} onChange={setUnit} /></div>
        <h3 className={styles.subTitle}>{t('Tiro único', 'Single Shot')}</h3>
        <SingleShotTable unit={unit} />
        <h3 className={styles.subTitle}>Autofire</h3>
        <AutofireTable unit={unit} />
        <ul className={styles.notes}>
          <li>{t('Cartucho de escopeta (Shell): DV13 para atingir todos à frente até ', 'Shotgun shell: DV13 to hit everyone in front up to ')}{unit === 'tiles' ? '3 tiles' : '6 m/yds'} (p.174).</li>
          <li>{t('Explosivos acertam uma área de ', 'Explosives hit an area of ')}{unit === 'tiles' ? '5 × 5 tiles' : '10 × 10 m/yds'} {t('centrada no alvo (p.174).', 'centered on the target (p.174).')}</li>
          <li>{t('Armas corpo a corpo atacam alvos adjacentes. Metralhadoras e armas exóticas de suplementos podem trazer regra própria na descrição.', 'Melee weapons attack adjacent targets. Machine guns and exotic weapons from supplements may have their own rule in the description.')}</li>
        </ul>
      </section>

      <section className={page.section}>
        <h2 className="hud-title">Critical Injuries</h2>
        <ul className={styles.notes}>
          {CRITICAL_RULES.map((rule) => <li key={rule}>{rule}</li>)}
          <li>{t('Quick Fix e tratamento estão no Core p.223. Lesões marcadas com Death Save +1 aumentam a penalidade base de Death Save.', 'Quick Fix and treatment are in the Core p.223. Injuries marked Death Save +1 raise the base Death Save penalty.')}</li>
        </ul>
        {!injuries ? <Spinner /> : (
          <div className={styles.critGrid}>
            <CriticalTable title={t('Corpo (Core p.187)', 'Body (Core p.187)')} rows={injuries.filter((i) => i.location === 'Body')} />
            <CriticalTable title={t('Cabeça (Core p.188)', 'Head (Core p.188)')} rows={injuries.filter((i) => i.location === 'Head')} />
          </div>
        )}
      </section>

      <section className={page.section}>
        <h2 className="hud-title">{t('Valores derivados por classe de arma', 'Values derived by weapon class')}</h2>
        <p className={page.note}>
          {t('O compêndio não traz munição compatível nem alcance por item. O catálogo deriva esses valores da classe da arma (com exceções para armas exóticas e combinadas, descritas nas notas). Confira na tabela de alcance do livro antes de usar em jogo.', 'The compendium has no compatible ammunition or range per item. The catalog derives these values from the weapon class (with exceptions for exotic and combined weapons, described in the notes). Check the book\'s range table before using them in play.')}
        </p>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>{t('Classe', 'Class')}</th><th>{t('Munição compatível', 'Compatible ammunition')}</th><th>{t('Alcance máx. usado no filtro', 'Max. range used in the filter')}</th><th>{t('Em tiles (2 m)', 'In tiles (2 m)')}</th></tr></thead>
            <tbody>{CLASS_TABLE.map(([a, b, c, d]) => <tr key={a}><td>{a}</td><td>{b}</td><td>{c}</td><td>{d}</td></tr>)}</tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

const GLOSSARY: [string, string][] = [
  ['ACPA', t('Armadura de combate assistida (powered armor). No catálogo fica em Armaduras, com SP, HP estrutural e BODY.', 'Assisted combat armor (powered armor). In the catalog it\'s under Armor, with SP, structural HP and BODY.')],
  ['Autofire', t('Modo de rajada. O número (x3, x4...) é o multiplicador usado no ataque automático.', 'Burst mode. The number (x3, x4...) is the multiplier used in the automatic attack.')],
  ['Black ICE', t('Programas hostis que protegem redes e atacam Netrunners ou seus programas.', 'Hostile programs that protect networks and attack Netrunners or their programs.')],
  ['BODY', t('Atributo físico. Algumas armas exigem BODY mínimo; Linear Frames e ACPAs concedem um valor de BODY.', 'Physical STAT. Some weapons need a minimum BODY; Linear Frames and ACPAs grant a BODY value.')],
  ['Borgware', t('Cyberware extremo, geralmente para conversões corporais pesadas.', 'Extreme cyberware, usually for heavy body conversions.')],
  ['Chipware', t('Chips encaixados no Chipware Socket, sem cirurgia.', 'Chips slotted into the Chipware Socket, no surgery needed.')],
  [t('Clínica / Hospital / Mall', 'Clinic / Hospital / Mall'), t('Onde o cyberware é instalado; indica a complexidade da instalação.', 'Where the cyberware is installed; shows how complex the install is.')],
  [t('Concealable (Ocultável)', 'Concealable'), t('A arma pode ser escondida sob a roupa.', 'The weapon can be hidden under clothing.')],
  ['Critical Injury', t('Lesão grave quando dois dados de dano saem 6: 5 de dano bônus direto nos HP e um efeito da tabela (página Tabelas).', 'Serious injury when two damage dice roll 6: 5 bonus damage straight to HP and an effect from the table (Tables page).')],
  ['Fashionware', t('Cyberware estético (Chemskin, Techhair, Light Tattoo...). No catálogo fica em Cyberware, com o tipo Fashionware.', 'Cosmetic cyberware (Chemskin, Techhair, Light Tattoo...). In the catalog it\'s under Cyberware, with the Fashionware type.')],
  ['DV', t('Difficulty Value: o número a superar num teste.', 'Difficulty Value: the number to beat on a check.')],
  ['Eddies (eb)', t('Eurodólares, a moeda de Night City.', 'Eurodollars, the currency of Night City.')],
  ['Foundational', t('Peça base de cyberware que oferece slots para opções (Cybereye, Neural Link, Cyberarm...).', 'Foundational cyberware piece that provides slots for options (Cybereye, Neural Link, Cyberarm...).')],
  ['HL', t('Humanity Loss: perda de Humanidade ao instalar cyberware.', 'Humanity Loss: Humanity lost when installing cyberware.')],
  [t('IP de QG', 'HQ IP'), t('Pontos de melhoria usados para instalar melhorias no quartel-general.', 'Improvement points used to install upgrades in the headquarters.')],
  ['Linear Frame', t('Exoesqueleto que eleva o BODY do usuário.', 'Exoskeleton that raises the user\'s BODY.')],
  [t('Moto (Nomad)', 'Moto (Nomad)'), t('Role Ability do Nomad. O rank define que veículos e upgrades ele pode tirar do Family Motorpool ("Nomad Access" nos suplementos).', 'The Nomad\'s Role Ability. The rank sets which vehicles and upgrades they can take from the Family Motorpool ("Nomad Access" in the supplements).')],
  ['Netrunner', t('Hacker que invade redes com um Cyberdeck. Decks, Programas, Black ICE e Hardware ficam na aba Netrunning.', 'Hacker who breaks into networks with a Cyberdeck. Decks, Programs, Black ICE and Hardware are on the Netrunning tab.')],
  ['ROF', t('Rate of Fire: quantos ataques a arma faz por ação em tiro único.', 'Rate of Fire: how many attacks the weapon makes per action in single shot.')],
  ['SDP', t('Pontos de dano estrutural de veículos.', 'Structural damage points of vehicles.')],
  ['Smartgun', t('Arma com link inteligente, usada com Interface Plugs ou Subdermal Grip.', 'Weapon with a smart link, used with Interface Plugs or a Subdermal Grip.')],
  ['SP', t('Stopping Power: a proteção de uma armadura.', 'Stopping Power: an armor\'s protection.')],
  ['Tile', t('Quadrado de 2 m/yds usado em mapas de batalha; é a medida que o Core usa para áreas (página Tabelas).', '2 m/yds square used on battle maps; it\'s the measure the Core uses for areas (Tables page).')],
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
        <h1 className="hud-title" data-glitch={t('Glossário', 'Glossary')}>{t('Glossário', 'Glossary')}</h1>
        <p className={page.lead}>{t('Termos usados no catálogo.', 'Terms used in the catalog.')}</p>
      </header>
      <label className={styles.search}>
        <Search size={16} />
        <input type="search" value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t('Filtrar termos...', 'Filter terms...')} aria-label={t('Filtrar termos', 'Filter terms')} />
      </label>
      <dl className={styles.glossary}>
        {filtered.map(([term, definition]) => (
          <div key={term}>
            <dt>{term}</dt>
            <dd>{definition}</dd>
          </div>
        ))}
        {filtered.length === 0 && <p className={page.note}>{t('Nenhum termo encontrado.', 'No terms found.')}</p>}
      </dl>
    </div>
  );
}
