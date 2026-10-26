import type { NightMarketRules, Ruled } from '../../api/nightMarketTypes';
import { Badge, Collapsible } from '../../components/ui/ui';
import { formatEb, PRICE_CATEGORY_LABEL } from '../../lib/format';
import page from '../../styles/Page.module.css';
import styles from './NightMarket.module.css';
import { t, tv } from '../../i18n';

function Ref({ r }: { r: Ruled<unknown> }) {
  return r.complementary
    ? <Badge tone="amber" title={tv(r.note) ?? undefined}>{t('complementar', 'complementary')}</Badge>
    : <span className={styles.source}>{r.source}</span>;
}

function Fact({ label, r, children }: { label: string; r: Ruled<unknown>; children?: React.ReactNode }) {
  return (
    <tr>
      <td>{label}</td>
      <td>{children ?? String(r.value)}{r.note && <div className={styles.muted}>{tv(r.note)}</div>}</td>
      <td><Ref r={r} /></td>
    </tr>
  );
}

/** Every parameter of night-market-rules.json with the page it came from, or the complementary flag. */
export function SourcesView({ rules }: { rules: NightMarketRules }) {
  const d = rules.rules;
  const g = d.generator;
  const op = d.operator;
  const c = d.condition;
  return (
    <div className={styles.dialog}>
      <Collapsible title={t('Gerador do Core (CP:R p.338–339)', 'Core generator (CP:R p.338–339)')} defaultOpen>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <tbody>
              <Fact label={t('Tipos de mercadoria', 'Types of goods')} r={g.goodsRolls}>{tv(g.goodsRolls.value)} {t('× 1d6, sem repetir', '× 1d6, no repeats')}</Fact>
              <Fact label={t('Linhas por tipo', 'Lines per type')} r={g.linesPerType} />
              <Fact label={t('Faixas da d100', 'd100 ranges')} r={g.bandSize}>{tv(g.bandSize.value)} {t('em ', 'in ')}{tv(g.bandSize.value)}</Fact>
              <Fact label={t('Todas as qualidades', 'All qualities')} r={g.allQualities}>{t('Sim', 'Yes')}</Fact>
              <Fact label={t('Peça Foundational', 'Foundational piece')} r={g.foundational}>{t('Vem junto com a Option', 'Comes with the Option')}</Fact>
              <Fact label={t('Instalação', 'Install')} r={g.cyberwareInstall} />
              <Fact label={t('Estoque', 'Stock')} r={g.notTotalInventory} />
              <Fact label={t('Sorteio das escolhas do Mestre', 'Drawing the GM\'s choices')} r={g.gmChoiceDraw}>{t('Ajuda opcional', 'Optional help')}</Fact>
              <Fact label={t('Outros livros por tipo', 'Other books by type')} r={g.relatedBooks}>{t('Opcional', 'Optional')}</Fact>
            </tbody>
          </table>
        </div>
        {g.goods.value.map((goods) => (
          <details key={goods.id} style={{ marginTop: 8 }}>
            <summary><b>{goods.roll}. {tv(goods.name)}</b> <span className={styles.muted}>— {tv(goods.description)}</span></summary>
            <ol className={styles.muted}>
              {goods.table.map((e, i) => (
                <li key={i} value={i + 1}>
                  <span className={styles.source}>{i * g.bandSize.value + 1}–{(i + 1) * g.bandSize.value}</span> {tv(e.label)}
                  {e.local && ` (${formatEb(e.local.price)}eb)`}{e.gmChoice && t(' — espaço para o Mestre', ' — room for the GM')}{e.note && <em> · {tv(e.note)}</em>}
                </li>
              ))}
            </ol>
          </details>
        ))}
      </Collapsible>

      <Collapsible title={t('Fixer: Operator, Reach e Haggle', 'Fixer: Operator, Reach and Haggle')} defaultOpen>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <tbody>
              <Fact label="Reach" r={op.reach}>
                {op.reach.value.map((entry) => `${entry.minRank === entry.maxRank ? entry.minRank : `${entry.minRank}–${entry.maxRank}`}: ${PRICE_CATEGORY_LABEL[entry.category]}`).join(' · ')}
              </Fact>
              <Fact label={t('Organizar Night Market', 'Organize a Night Market')} r={op.nightMarketRank}>Operator {tv(op.nightMarketRank.value)}+</Fact>
              <Fact label={t('Reach no próprio Night Market', 'Reach at their own Night Market')} r={op.nightMarketReach}>{PRICE_CATEGORY_LABEL[op.nightMarketReach.value]}</Fact>
              <Fact label="Midnight Market" r={op.midnightMarketRank}>Operator {tv(op.midnightMarketRank.value)}+</Fact>
              <Fact label={t('Sem Fixer', 'No Fixer')} r={d.withoutFixer}>{t('Até ', 'Up to ')}{PRICE_CATEGORY_LABEL[d.withoutFixer.value]}</Fact>
              <Fact label="Haggle" r={op.haggleFormula} />
              <Fact label={t('Empate', 'Tie')} r={op.haggleTie}>{t('Fica com o Defensor', 'Goes to the Defender')}</Fact>
              <Fact label={t('Críticos', 'Criticals')} r={op.criticals} />
              <Fact label={t('Acordos por transação', 'Deals per transaction')} r={op.dealsPerTransaction} />
              <Fact label={t('Acordos', 'Deals')} r={op.deals}>
                {op.deals.value.map((x) => <div key={x.id}>Rank {x.minRank}+: {tv(x.text)}</div>)}
              </Fact>
              <Fact label={t('Arredondamento', 'Rounding')} r={op.rounding}>{t('Para baixo', 'Down')}</Fact>
              <Fact label={t('Disponibilidade ≠ preço', 'Availability ≠ price')} r={d.fixerAvailability} />
            </tbody>
          </table>
        </div>
      </Collapsible>

      <Collapsible title={t('Itens Damaged e Destroyed (IR5)', 'Damaged and Destroyed items (IR5)')}>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <tbody>
              <Fact label={t('Categorias de preço', 'Price categories')} r={d.priceLadder}>
                {d.priceLadder.value.map((entry) => `${PRICE_CATEGORY_LABEL[entry.category]} ${formatEb(entry.price)}eb`).join(' · ')}
              </Fact>
              <Fact label="Damaged" r={c.damagedSteps}>{tv(c.damagedSteps.value)} {t('categoria abaixo', 'category lower')}</Fact>
              <Fact label="Destroyed" r={c.destroyedSteps}>{tv(c.destroyedSteps.value)} {t('categorias abaixo', 'categories lower')}</Fact>
              <Fact label={t('Fixer percebe', 'Fixer notices')} r={c.detectRank}>Operator {tv(c.detectRank.value)}+</Fact>
              <Fact label={t('Damaged barato', 'Cheap Damaged')} r={c.lowValueDamaged}>{t('Até ', 'Up to ')}{tv(c.lowValueDamaged.value)}{t('eb: poucos compram', 'eb: few buy')}</Fact>
              <Fact label="Beyond Repair" r={c.beyondRepair}>{tv(c.beyondRepair.value)}eb</Fact>
              <Fact label={t('Vender danificado', 'Selling damaged')} r={c.sellingIsGmCall} />
              <Fact label={t('Piso', 'Floor')} r={c.floor}>{PRICE_CATEGORY_LABEL[c.floor.value]}</Fact>
              <Fact label={t('Preço ao descer', 'Price when dropping')} r={c.categoryPrice}>{t('Preço de mercado da categoria', 'Market price of the category')}</Fact>
            </tbody>
          </table>
        </div>
      </Collapsible>

      <Collapsible title={t('Black Chrome, Midnight Market, Door Prize e Mystery Boxes', 'Black Chrome, Midnight Market, Door Prize and Mystery Boxes')}>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <tbody>
              <Fact label={t('Booths do CBK', 'CBK booths')} r={d.blackChrome.booths} />
              <Fact label={t('Taxa do CBK', 'CBK fee')} r={d.blackChrome.listingFee} />
              <Fact label="Buy Now!!!" r={d.blackChrome.buyNow} />
              <Fact label="Midnight Market" r={d.midnightMarket.items}>{tv(d.midnightMarket.items.value)} {t('itens raros', 'rare items')}</Fact>
              <Fact label={t('Sorteio do Midnight', 'Midnight draw')} r={d.midnightMarket.drawPool}>{PRICE_CATEGORY_LABEL[d.midnightMarket.drawPool.value.minCategory]}{t('+ ou exótica', '+ or exotic')}</Fact>
              <Fact label={t('Door Prize: chegada', 'Door Prize: arrival')} r={d.doorPrize.arrival}>{t('A cada ', 'Every ')}{tv(d.doorPrize.arrival.value)} {t('visitantes', 'visitors')}</Fact>
              <Fact label={t('Door Prize: exemplo', 'Door Prize: example')} r={d.doorPrize.example}>{tv(d.doorPrize.example.value.label)}</Fact>
              <Fact label={t('Door Prize: bônus', 'Door Prize: bonus')} r={d.doorPrize.bonus}>{d.doorPrize.bonus.value.map((b) => tv(b.label)).join(' · ')}</Fact>
              <Fact label={t('Door Prize: roleta', 'Door Prize: wheel')} r={d.doorPrize.slices}>{d.doorPrize.slices.value.map((s) => t(`${tv(s.label)} (peso ${s.weight})`, `${tv(s.label)} (weight ${s.weight})`)).join(' · ')}</Fact>
              <Fact label="Mystery Box" r={d.mysteryBoxes.redemption} />
              <Fact label={t('Padrão de preço', 'Price pattern')} r={d.mysteryBoxes.pricePattern}>{d.mysteryBoxes.pricePattern.value.join(', ')}eb</Fact>
              <Fact label="Table of Mystery" r={d.mysteryBoxes.boxes}>
                {d.mysteryBoxes.boxes.value.map((b) => <div key={b.roll}>{b.roll}. {tv(b.name)}: {b.contents.map((x) => tv(x.label)).join(' · ')}</div>)}
              </Fact>
            </tbody>
          </table>
        </div>
      </Collapsible>

      <Collapsible title={t('Mercados prontos', 'Ready-made markets')}>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>{t('Mercado', 'Market')}</th><th>{t('Organizador', 'Organizer')}</th><th>{t('Fonte', 'Source')}</th></tr></thead>
            <tbody>
              {rules.presets.map((p) => (
                <tr key={p.id}>
                  <td><b>{p.letter}</b> · {tv(p.name)}</td>
                  <td>{tv(p.organizer.name)}{p.organizer.operatorRank != null ? ` (Operator ${p.organizer.operatorRank})` : ''}</td>
                  <td className={styles.source}>{p.source}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Collapsible>
    </div>
  );
}
