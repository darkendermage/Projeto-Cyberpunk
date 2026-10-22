import type { NightMarketRules, Ruled } from '../../api/nightMarketTypes';
import { Badge, Collapsible } from '../../components/ui/ui';
import { formatEb, PRICE_CATEGORY_LABEL } from '../../lib/format';
import page from '../../styles/Page.module.css';
import styles from './NightMarket.module.css';

function Ref({ r }: { r: Ruled<unknown> }) {
  return r.complementary
    ? <Badge tone="amber" title={r.note ?? undefined}>complementar</Badge>
    : <span className={styles.source}>{r.source}</span>;
}

function Fact({ label, r, children }: { label: string; r: Ruled<unknown>; children?: React.ReactNode }) {
  return (
    <tr>
      <td>{label}</td>
      <td>{children ?? String(r.value)}{r.note && <div className={styles.muted}>{r.note}</div>}</td>
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
      <Collapsible title="Gerador do Core (CP:R p.338–339)" defaultOpen>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <tbody>
              <Fact label="Tipos de mercadoria" r={g.goodsRolls}>{g.goodsRolls.value} × 1d6, sem repetir</Fact>
              <Fact label="Linhas por tipo" r={g.linesPerType} />
              <Fact label="Faixas da d100" r={g.bandSize}>{g.bandSize.value} em {g.bandSize.value}</Fact>
              <Fact label="Todas as qualidades" r={g.allQualities}>Sim</Fact>
              <Fact label="Peça Foundational" r={g.foundational}>Vem junto com a Option</Fact>
              <Fact label="Instalação" r={g.cyberwareInstall} />
              <Fact label="Estoque" r={g.notTotalInventory} />
              <Fact label="Sorteio das escolhas do Mestre" r={g.gmChoiceDraw}>Ajuda opcional</Fact>
              <Fact label="Outros livros por tipo" r={g.relatedBooks}>Opcional</Fact>
            </tbody>
          </table>
        </div>
        {g.goods.value.map((goods) => (
          <details key={goods.id} style={{ marginTop: 8 }}>
            <summary><b>{goods.roll}. {goods.name}</b> <span className={styles.muted}>— {goods.description}</span></summary>
            <ol className={styles.muted}>
              {goods.table.map((e, i) => (
                <li key={i} value={i + 1}>
                  <span className={styles.source}>{i * g.bandSize.value + 1}–{(i + 1) * g.bandSize.value}</span> {e.label}
                  {e.local && ` (${formatEb(e.local.price)}eb)`}{e.gmChoice && ' — espaço para o Mestre'}{e.note && <em> · {e.note}</em>}
                </li>
              ))}
            </ol>
          </details>
        ))}
      </Collapsible>

      <Collapsible title="Fixer: Operator, Reach e Haggle" defaultOpen>
        <div className={page.tableWrap}>
          <table className={page.table}>
            <tbody>
              <Fact label="Reach" r={op.reach}>
                {op.reach.value.map((t) => `${t.minRank === t.maxRank ? t.minRank : `${t.minRank}–${t.maxRank}`}: ${PRICE_CATEGORY_LABEL[t.category]}`).join(' · ')}
              </Fact>
              <Fact label="Organizar Night Market" r={op.nightMarketRank}>Operator {op.nightMarketRank.value}+</Fact>
              <Fact label="Reach no próprio Night Market" r={op.nightMarketReach}>{PRICE_CATEGORY_LABEL[op.nightMarketReach.value]}</Fact>
              <Fact label="Midnight Market" r={op.midnightMarketRank}>Operator {op.midnightMarketRank.value}+</Fact>
              <Fact label="Sem Fixer" r={d.withoutFixer}>Até {PRICE_CATEGORY_LABEL[d.withoutFixer.value]}</Fact>
              <Fact label="Haggle" r={op.haggleFormula} />
              <Fact label="Empate" r={op.haggleTie}>Fica com o Defensor</Fact>
              <Fact label="Críticos" r={op.criticals} />
              <Fact label="Acordos por transação" r={op.dealsPerTransaction} />
              <Fact label="Acordos" r={op.deals}>
                {op.deals.value.map((x) => <div key={x.id}>Rank {x.minRank}+: {x.text}</div>)}
              </Fact>
              <Fact label="Arredondamento" r={op.rounding}>Para baixo</Fact>
              <Fact label="Disponibilidade ≠ preço" r={d.fixerAvailability} />
            </tbody>
          </table>
        </div>
      </Collapsible>

      <Collapsible title="Itens Damaged e Destroyed (IR5)">
        <div className={page.tableWrap}>
          <table className={page.table}>
            <tbody>
              <Fact label="Categorias de preço" r={d.priceLadder}>
                {d.priceLadder.value.map((t) => `${PRICE_CATEGORY_LABEL[t.category]} ${formatEb(t.price)}eb`).join(' · ')}
              </Fact>
              <Fact label="Damaged" r={c.damagedSteps}>{c.damagedSteps.value} categoria abaixo</Fact>
              <Fact label="Destroyed" r={c.destroyedSteps}>{c.destroyedSteps.value} categorias abaixo</Fact>
              <Fact label="Fixer percebe" r={c.detectRank}>Operator {c.detectRank.value}+</Fact>
              <Fact label="Damaged barato" r={c.lowValueDamaged}>Até {c.lowValueDamaged.value}eb: poucos compram</Fact>
              <Fact label="Beyond Repair" r={c.beyondRepair}>{c.beyondRepair.value}eb</Fact>
              <Fact label="Vender danificado" r={c.sellingIsGmCall} />
              <Fact label="Piso" r={c.floor}>{PRICE_CATEGORY_LABEL[c.floor.value]}</Fact>
              <Fact label="Preço ao descer" r={c.categoryPrice}>Preço de mercado da categoria</Fact>
            </tbody>
          </table>
        </div>
      </Collapsible>

      <Collapsible title="Black Chrome, Midnight Market, Door Prize e Mystery Boxes">
        <div className={page.tableWrap}>
          <table className={page.table}>
            <tbody>
              <Fact label="Booths do CBK" r={d.blackChrome.booths} />
              <Fact label="Taxa do CBK" r={d.blackChrome.listingFee} />
              <Fact label="Buy Now!!!" r={d.blackChrome.buyNow} />
              <Fact label="Midnight Market" r={d.midnightMarket.items}>{d.midnightMarket.items.value} itens raros</Fact>
              <Fact label="Sorteio do Midnight" r={d.midnightMarket.drawPool}>{PRICE_CATEGORY_LABEL[d.midnightMarket.drawPool.value.minCategory]}+ ou exótica</Fact>
              <Fact label="Door Prize: chegada" r={d.doorPrize.arrival}>A cada {d.doorPrize.arrival.value} visitantes</Fact>
              <Fact label="Door Prize: exemplo" r={d.doorPrize.example}>{d.doorPrize.example.value.label}</Fact>
              <Fact label="Door Prize: bônus" r={d.doorPrize.bonus}>{d.doorPrize.bonus.value.map((b) => b.label).join(' · ')}</Fact>
              <Fact label="Door Prize: roleta" r={d.doorPrize.slices}>{d.doorPrize.slices.value.map((s) => `${s.label} (peso ${s.weight})`).join(' · ')}</Fact>
              <Fact label="Mystery Box" r={d.mysteryBoxes.redemption} />
              <Fact label="Padrão de preço" r={d.mysteryBoxes.pricePattern}>{d.mysteryBoxes.pricePattern.value.join(', ')}eb</Fact>
              <Fact label="Table of Mystery" r={d.mysteryBoxes.boxes}>
                {d.mysteryBoxes.boxes.value.map((b) => <div key={b.roll}>{b.roll}. {b.name}: {b.contents.map((x) => x.label).join(' · ')}</div>)}
              </Fact>
            </tbody>
          </table>
        </div>
      </Collapsible>

      <Collapsible title="Mercados prontos">
        <div className={page.tableWrap}>
          <table className={page.table}>
            <thead><tr><th>Mercado</th><th>Organizador</th><th>Fonte</th></tr></thead>
            <tbody>
              {rules.presets.map((p) => (
                <tr key={p.id}>
                  <td><b>{p.letter}</b> · {p.name}</td>
                  <td>{p.organizer.name}{p.organizer.operatorRank != null ? ` (Operator ${p.organizer.operatorRank})` : ''}</td>
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
