import type { NetArchitectureRules, TableRow } from '../../api/netArchitectureTypes';
import type { Ruled } from '../../api/nightMarketTypes';
import { Badge } from '../../components/ui/ui';
import page from '../../styles/Page.module.css';
import { formatEbShort, GROUP_LABEL } from './netArchitecture';
import styles from './NetArchitecture.module.css';
import { t, tv } from '../../i18n';

function Ref({ r }: { r: Ruled<unknown> }) {
  return r.complementary
    ? <Badge tone="amber" title={tv(r.note) ?? undefined}>{t('complementar', 'complementary')}</Badge>
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
        <h2>{t('Passo 0: dificuldade ', 'Step 0: difficulty ')}<Ref r={r.difficulties} /></h2>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>{t('Dificuldade', 'Difficulty')}</th><th>DV</th><th>{t('Netrunner com chance', 'Netrunner with a fighting chance')}</th><th>{t('Pode morrer antes do fundo', 'Might die before the bottom')}</th><th>{t('Uso', 'Use')}</th></tr></thead>
            <tbody>{r.difficulties.value.map((d) => (
              <tr key={d.id}><td>{tv(d.label)}</td><td>DV{d.dv}</td><td>Interface {d.fightingChance}</td><td>{d.mightDie != null ? t(`${d.mightDie} ou menos`, `${d.mightDie} or less`) : 'N/A'}</td><td>{tv(d.description)}</td></tr>
            ))}</tbody>
          </table>
        </div>
      </section>

      <section className={page.section}>
        <h2>{t('Passo 1: forma', 'Step 1: shape')}</h2>
        <ul className={styles.facts}>
          {[r.shape.floorDice, r.shape.branchOn, r.shape.firstBranchFloor, r.shape.longestBranch].map((x, i) => (
            <li key={i}><span>{[t('3d6 andares', '3d6 floors'), t('d10: 7 ou mais cria um ramo; rola de novo até falhar', 'd10: 7 or more creates a branch; roll again until it fails'), t('Nenhum ramo antes do 3º andar', 'No branch before the 3rd floor'), t('Existe um ramo mais longo (o fundo, onde vai o Virus)', 'There\'s a longest branch (the bottom, where the Virus goes)')][i]}</span><Ref r={x} /></li>
          ))}
          <li><span>{tv(r.shape.split.value)}</span><Ref r={r.shape.split} /></li>
        </ul>
      </section>

      <section className={page.section}>
        <h2>{t('Passo 2: tabelas ', 'Step 2: tables ')}<Ref r={r.bodyTables} /></h2>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>1d6</th><th>{t('Lobby (andares 1 e 2)', 'Lobby (floors 1 and 2)')}</th></tr></thead>
            <tbody>{r.lobbyTable.value.map((row) => <tr key={row.roll}><td>{row.roll}</td><td>{rowText(row)}</td></tr>)}</tbody>
          </table>
        </div>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>3d6</th>{r.bodyTables.value.map((entry) => <th key={entry.difficulty}>{tv(r.difficulties.value.find((d) => d.id === entry.difficulty)?.label)}</th>)}</tr></thead>
            <tbody>{r.bodyTables.value[0].rows.map((row, i) => (
              <tr key={row.roll}><td>{row.roll}</td>{r.bodyTables.value.map((entry) => <td key={entry.difficulty}>{rowText(entry.rows[i])}</td>)}</tr>
            ))}</tbody>
          </table>
        </div>
        <ul className={styles.facts}>
          <li><span>{t('Linhas de Black ICE e Password não se repetem na mesma Arquitetura', 'Black ICE and Password rows don\'t repeat in the same Architecture')}</span><Ref r={r.noRepeat} /></li>
          <li><span>{t('Boss Floor: um andar da dificuldade de cima', 'Boss Floor: a floor of the next difficulty up')}</span><Ref r={r.bossFloor} /></li>
          <li><span>{t('Trocar um Program por outro de mesmo custo', 'Swap a Program for another of the same cost')}</span><Ref r={r.sameCostSwap} /></li>
        </ul>
      </section>

      <section className={page.section}>
        <h2>{t('Black ICE do catálogo', 'Catalog Black ICE')}</h2>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>{t('Nome', 'Name')}</th><th>{t('Classe', 'Class')}</th><th>PER</th><th>SPD</th><th>ATK</th><th>DEF</th><th>REZ</th><th>{t('Efeito', 'Effect')}</th><th>{t('Custo', 'Cost')}</th><th>{t('Página', 'Page')}</th></tr></thead>
            <tbody>{rules.ice.map((i) => (
              <tr key={i.slug}>
                <td>{tv(i.name)}</td><td>{i.class === 'AntiProgram' ? 'Anti-Program' : 'Anti-Personnel'}</td>
                <td>{i.per}</td><td>{i.spd}</td><td>{i.atk}</td><td>{i.def}</td><td>{i.rez}</td><td>{tv(i.effect)}</td>
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
            <thead><tr><th>Demon</th><th>REZ</th><th>Interface</th><th>NET Actions</th><th>Combat Number</th><th>{t('Custo', 'Cost')}</th><th>{t('Ícone', 'Icon')}</th></tr></thead>
            <tbody>{r.demons.value.map((d) => (
              <tr key={d.id}><td>{tv(d.name)}</td><td>{d.rez}</td><td>{d.interface}</td><td>{d.netActions}</td><td>{d.combatNumber}</td><td>{formatEbShort(d.cost)}</td><td>{d.icon}</td></tr>
            ))}</tbody>
          </table>
        </div>
        <ul className={styles.facts}>
          <li><span>{tv(r.demonAbilities.value)}</span><Ref r={r.demonAbilities} /></li>
          <li><span>{tv(r.floorsPerDemon.note)}</span><Ref r={r.floorsPerDemon} /></li>
          <li><span>{t('Active Defenses precisam de um Demon', 'Active Defenses need a Demon')}</span><Ref r={r.activeNeedsDemon} /></li>
        </ul>
      </section>

      <section className={page.section}>
        <h2>{t('Defesas ', 'Defenses ')}<Ref r={r.defenses} /></h2>
        <p className={styles.muted}>{tv(r.defenseControl.value)} <Ref r={r.defenseControl} /></p>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>{t('Defesa', 'Defense')}</th><th>{t('Tipo', 'Type')}</th><th>{t('Descrição', 'Description')}</th><th>{t('Gatilho', 'Trigger')}</th><th>{t('Dados', 'Data')}</th><th>{t('Neutralizar', 'Neutralize')}</th><th>{t('Página', 'Page')}</th></tr></thead>
            <tbody>{r.defenses.value.map((d) => (
              <tr key={d.id}>
                <td>{tv(d.label)}<br /><span className={styles.source}>{tv(d.name)}</span></td><td>{GROUP_LABEL[d.group]}</td>
                <td>{tv(d.description)}{d.loadoutPicks > 0 && <><br /><span className={styles.source}>{t('Escolha ', 'Choice ')}{d.loadoutPicks}: {d.loadoutOptions.join('; ')}</span></>}</td>
                <td>{tv(d.trigger)}</td><td>{d.data}</td><td>DV{d.counterDv} ({tv(d.counterTime)})</td><td className={styles.source}>CP:R p.{d.page}</td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      </section>

      <section className={page.section}>
        <h2>{t('Preços de compra', 'Purchase prices')}</h2>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>{t('Andares', 'Floors')}</th><th>Control Nodes</th><th>{t('Portátil', 'Portable')}</th><th>{t('Por andar', 'Per floor')}</th></tr></thead>
            <tbody>{r.pricing.floors.value.map((b) => (
              <tr key={b.min}><td>{b.min} a {b.max}</td><td>{b.maxControlNodes ?? t('Sem limite', 'No limit')}</td><td>{b.portable ? t('Sim', 'Yes') : t('Não', 'No')}</td><td>{formatEbShort(b.costPerFloor)}</td></tr>
            ))}</tbody>
          </table>
        </div>
        <ul className={styles.facts}>
          <li><span>{t('Andares e limite de Control Nodes', 'Floors and Control Node limit')}</span><Ref r={r.pricing.floors} /></li>
          <li><span>{t('Password, File e Control Node: ', 'Password, File and Control Node: ')}{r.pricing.features.value.map((c) => `DV${c.dv} ${formatEbShort(c.cost)}`).join(' · ')}</span><Ref r={r.pricing.features} /></li>
          <li><span>{t('Black ICE pelo preço do catálogo; 2 no andar custam o dobro cada, 3 o triplo', 'Black ICE at the catalog price; 2 on a floor cost double each, 3 triple')}</span><Ref r={r.pricing.iceMultiplier} /></li>
          <li><span>{t('Defesas pela DV para neutralizar: ', 'Defenses by the DV to neutralize: ')}{r.pricing.defenses.value.map((c) => `DV${c.dv} ${formatEbShort(c.cost)}`).join(' · ')}</span><Ref r={r.pricing.defenses} /></li>
          <li><span>{t('Dispositivos civis sem custo extra', 'Civilian devices at no extra cost')}</span><Ref r={r.pricing.devicesFree} /></li>
          <li><span>{tv(r.pricing.availability.value)}</span><Ref r={r.pricing.availability} /></li>
        </ul>
      </section>

      <section className={page.section}>
        <h2>Stealth (Going Quiet)</h2>
        <ul className={styles.facts}>
          <li><span>{tv(r.stealth.watchers.value)}</span><Ref r={r.stealth.watchers} /></li>
          {r.stealth.checks.value.map((c) => <li key={c.label}><span><strong>{tv(c.label)}:</strong> {c.roll}</span><Ref r={r.stealth.checks} /></li>)}
          <li><span>{tv(r.stealth.placement.value)}</span><Ref r={r.stealth.placement} /></li>
          <li><span>{tv(r.stealth.enemyNetrunnerInterface.value)}</span><Ref r={r.stealth.enemyNetrunnerInterface} /></li>
        </ul>
      </section>

      <section className={page.section}>
        <h2>{t('Tipos de local e escolhas do gerador', 'Location types and generator choices')}</h2>
        <ul className={styles.facts}>
          <li><span>{r.locations.value.map((l) => tv(l.label)).join(', ')}</span><Ref r={r.locations} /></li>
          <li><span>{t('Sorteio de dificuldade: ', 'Random difficulty: ')}{Object.entries(r.generator.difficultyWeights.value).map(([k, v]) => `${k} ${v}`).join(' · ')}</span><Ref r={r.generator.difficultyWeights} /></li>
          <li><span>{t('Um Demon a cada ', 'One Demon per ')}{tv(r.generator.nodesPerDemon.value)} {t('Control Nodes, dentro do limite', 'Control Nodes, within the limit')}</span><Ref r={r.generator.nodesPerDemon} /></li>
          <li><span>{t('Tipo de Demon por dificuldade', 'Demon type by difficulty')}</span><Ref r={r.generator.demonWeights} /></li>
          <li><span>{t('Chance de Netrunner inimigo por dificuldade', 'Enemy Netrunner chance by difficulty')}</span><Ref r={r.generator.enemyNetrunnerChance} /></li>
          <li><span>{t('Control Node acima do limite de compra é rolado de novo', 'A Control Node over the purchase limit is rolled again')}</span><Ref r={r.generator.respectNodeLimit} /></li>
        </ul>
        <p className={styles.source}>{r.about}</p>
      </section>
    </>
  );
}
