import type { NetArchitectureRules, TableRow } from '../../api/netArchitectureTypes';
import type { Ruled } from '../../api/nightMarketTypes';
import { Badge } from '../../components/ui/ui';
import page from '../../styles/Page.module.css';
import { formatEbShort, GROUP_LABEL } from './netArchitecture';
import styles from './NetArchitecture.module.css';

function Ref({ r }: { r: Ruled<unknown> }) {
  return r.complementary
    ? <Badge tone="amber" title={r.note ?? undefined}>complementar</Badge>
    : <span className={styles.source}>{r.source}</span>;
}

/** Every table and rule the generator uses, with its page or the "complementar" badge. */
export function SourcesView({ rules }: { rules: NetArchitectureRules }) {
  const r = rules.rules;
  const ice = new Map(rules.ice.map((i) => [i.slug, i]));
  const rowText = (row: TableRow) =>
    row.contents.map((c) => (c.kind === 'BlackIce' ? ice.get(c.slug ?? '')?.name ?? c.slug : `${c.kind === 'ControlNode' ? 'Control Node' : c.kind} DV${c.dv}`)).join(', ');

  return (
    <>
      <section className={page.section}>
        <h2>Passo 0: dificuldade <Ref r={r.difficulties} /></h2>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>Dificuldade</th><th>DV</th><th>Netrunner com chance</th><th>Pode morrer antes do fundo</th><th>Uso</th></tr></thead>
            <tbody>{r.difficulties.value.map((d) => (
              <tr key={d.id}><td>{d.label}</td><td>DV{d.dv}</td><td>Interface {d.fightingChance}</td><td>{d.mightDie != null ? `${d.mightDie} ou menos` : 'N/A'}</td><td>{d.description}</td></tr>
            ))}</tbody>
          </table>
        </div>
      </section>

      <section className={page.section}>
        <h2>Passo 1: forma</h2>
        <ul className={styles.facts}>
          {[r.shape.floorDice, r.shape.branchOn, r.shape.firstBranchFloor, r.shape.longestBranch].map((x, i) => (
            <li key={i}><span>{['3d6 andares', 'd10: 7 ou mais cria um ramo; rola de novo até falhar', 'Nenhum ramo antes do 3º andar', 'Existe um ramo mais longo (o fundo, onde vai o Virus)'][i]}</span><Ref r={x} /></li>
          ))}
          <li><span>{r.shape.split.value}</span><Ref r={r.shape.split} /></li>
        </ul>
      </section>

      <section className={page.section}>
        <h2>Passo 2: tabelas <Ref r={r.bodyTables} /></h2>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>1d6</th><th>Lobby (andares 1 e 2)</th></tr></thead>
            <tbody>{r.lobbyTable.value.map((row) => <tr key={row.roll}><td>{row.roll}</td><td>{rowText(row)}</td></tr>)}</tbody>
          </table>
        </div>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>3d6</th>{r.bodyTables.value.map((t) => <th key={t.difficulty}>{r.difficulties.value.find((d) => d.id === t.difficulty)?.label}</th>)}</tr></thead>
            <tbody>{r.bodyTables.value[0].rows.map((row, i) => (
              <tr key={row.roll}><td>{row.roll}</td>{r.bodyTables.value.map((t) => <td key={t.difficulty}>{rowText(t.rows[i])}</td>)}</tr>
            ))}</tbody>
          </table>
        </div>
        <ul className={styles.facts}>
          <li><span>Linhas de Black ICE e Password não se repetem na mesma Arquitetura</span><Ref r={r.noRepeat} /></li>
          <li><span>Boss Floor: um andar da dificuldade de cima</span><Ref r={r.bossFloor} /></li>
          <li><span>Trocar um Program por outro de mesmo custo</span><Ref r={r.sameCostSwap} /></li>
        </ul>
      </section>

      <section className={page.section}>
        <h2>Black ICE do catálogo</h2>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>Nome</th><th>Classe</th><th>PER</th><th>SPD</th><th>ATK</th><th>DEF</th><th>REZ</th><th>Efeito</th><th>Custo</th><th>Página</th></tr></thead>
            <tbody>{rules.ice.map((i) => (
              <tr key={i.slug}>
                <td>{i.name}</td><td>{i.class === 'AntiProgram' ? 'Anti-Program' : 'Anti-Personnel'}</td>
                <td>{i.per}</td><td>{i.spd}</td><td>{i.atk}</td><td>{i.def}</td><td>{i.rez}</td><td>{i.effect}</td>
                <td>{i.price != null ? formatEbShort(i.price) : '—'}</td><td className={styles.source}>{i.book} p.{i.page}</td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      </section>

      <section className={page.section}>
        <h2>Demons <Ref r={r.demons} /></h2>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>Demon</th><th>REZ</th><th>Interface</th><th>NET Actions</th><th>Combat Number</th><th>Custo</th><th>Ícone</th></tr></thead>
            <tbody>{r.demons.value.map((d) => (
              <tr key={d.id}><td>{d.name}</td><td>{d.rez}</td><td>{d.interface}</td><td>{d.netActions}</td><td>{d.combatNumber}</td><td>{formatEbShort(d.cost)}</td><td>{d.icon}</td></tr>
            ))}</tbody>
          </table>
        </div>
        <ul className={styles.facts}>
          <li><span>{r.demonAbilities.value}</span><Ref r={r.demonAbilities} /></li>
          <li><span>{r.floorsPerDemon.note}</span><Ref r={r.floorsPerDemon} /></li>
          <li><span>Active Defenses precisam de um Demon</span><Ref r={r.activeNeedsDemon} /></li>
        </ul>
      </section>

      <section className={page.section}>
        <h2>Defesas <Ref r={r.defenses} /></h2>
        <p className={styles.muted}>{r.defenseControl.value} <Ref r={r.defenseControl} /></p>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>Defesa</th><th>Tipo</th><th>Descrição</th><th>Gatilho</th><th>Dados</th><th>Neutralizar</th><th>Página</th></tr></thead>
            <tbody>{r.defenses.value.map((d) => (
              <tr key={d.id}>
                <td>{d.label}<br /><span className={styles.source}>{d.name}</span></td><td>{GROUP_LABEL[d.group]}</td>
                <td>{d.description}{d.loadoutPicks > 0 && <><br /><span className={styles.source}>Escolha {d.loadoutPicks}: {d.loadoutOptions.join('; ')}</span></>}</td>
                <td>{d.trigger}</td><td>{d.data}</td><td>DV{d.counterDv} ({d.counterTime})</td><td className={styles.source}>CP:R p.{d.page}</td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      </section>

      <section className={page.section}>
        <h2>Preços de compra</h2>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>Andares</th><th>Control Nodes</th><th>Portátil</th><th>Por andar</th></tr></thead>
            <tbody>{r.pricing.floors.value.map((b) => (
              <tr key={b.min}><td>{b.min} a {b.max}</td><td>{b.maxControlNodes ?? 'Sem limite'}</td><td>{b.portable ? 'Sim' : 'Não'}</td><td>{formatEbShort(b.costPerFloor)}</td></tr>
            ))}</tbody>
          </table>
        </div>
        <ul className={styles.facts}>
          <li><span>Andares e limite de Control Nodes</span><Ref r={r.pricing.floors} /></li>
          <li><span>Password, File e Control Node: {r.pricing.features.value.map((c) => `DV${c.dv} ${formatEbShort(c.cost)}`).join(' · ')}</span><Ref r={r.pricing.features} /></li>
          <li><span>Black ICE pelo preço do catálogo; 2 no andar custam o dobro cada, 3 o triplo</span><Ref r={r.pricing.iceMultiplier} /></li>
          <li><span>Defesas pela DV para neutralizar: {r.pricing.defenses.value.map((c) => `DV${c.dv} ${formatEbShort(c.cost)}`).join(' · ')}</span><Ref r={r.pricing.defenses} /></li>
          <li><span>Dispositivos civis sem custo extra</span><Ref r={r.pricing.devicesFree} /></li>
          <li><span>{r.pricing.availability.value}</span><Ref r={r.pricing.availability} /></li>
        </ul>
      </section>

      <section className={page.section}>
        <h2>Stealth (Going Quiet)</h2>
        <ul className={styles.facts}>
          <li><span>{r.stealth.watchers.value}</span><Ref r={r.stealth.watchers} /></li>
          {r.stealth.checks.value.map((c) => <li key={c.label}><span><strong>{c.label}:</strong> {c.roll}</span><Ref r={r.stealth.checks} /></li>)}
          <li><span>{r.stealth.placement.value}</span><Ref r={r.stealth.placement} /></li>
          <li><span>{r.stealth.enemyNetrunnerInterface.value}</span><Ref r={r.stealth.enemyNetrunnerInterface} /></li>
        </ul>
      </section>

      <section className={page.section}>
        <h2>Tipos de local e escolhas do gerador</h2>
        <ul className={styles.facts}>
          <li><span>{r.locations.value.map((l) => l.label).join(', ')}</span><Ref r={r.locations} /></li>
          <li><span>Sorteio de dificuldade: {Object.entries(r.generator.difficultyWeights.value).map(([k, v]) => `${k} ${v}`).join(' · ')}</span><Ref r={r.generator.difficultyWeights} /></li>
          <li><span>Um Demon a cada {r.generator.nodesPerDemon.value} Control Nodes, dentro do limite</span><Ref r={r.generator.nodesPerDemon} /></li>
          <li><span>Tipo de Demon por dificuldade</span><Ref r={r.generator.demonWeights} /></li>
          <li><span>Chance de Netrunner inimigo por dificuldade</span><Ref r={r.generator.enemyNetrunnerChance} /></li>
          <li><span>Control Node acima do limite de compra é rolado de novo</span><Ref r={r.generator.respectNodeLimit} /></li>
        </ul>
        <p className={styles.source}>{r.about}</p>
      </section>
    </>
  );
}
