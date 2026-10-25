import { useNavigate } from 'react-router';
import clsx from 'clsx';
import { AlertTriangle, BadgeCheck, ExternalLink } from 'lucide-react';
import type { FieldDescriptor, Item } from '../../api/types';
import { useCategoryKey, useItemDetail, useSchema } from '../../api/hooks';
import { ItemArt } from '../../components/art/ItemArt';
import { ErrorState, Modal, Spinner } from '../../components/ui/ui';
import { DATA_ORIGIN_LABEL, formatAttribute, formatEb, PRICE_CATEGORY_LABEL, RULE_SYSTEM_LABEL } from '../../lib/format';
import { rangeRowsFor } from '../../lib/rangeTable';
import { useRangeUnit } from '../../lib/hooks';
import { RangeUnitToggle, WeaponRange } from '../reference/RangeTables';
import { CATEGORY_CODES } from './categoryMeta';
import { AddToListMenu, CompareButton, FavoriteButton } from './ItemActions';
import styles from './ItemDetailModal.module.css';

export function ItemDetailModal({ itemId, onClose }: { itemId: number | null; onClose: () => void }) {
  const detail = useItemDetail(itemId);
  const item = detail.data?.item;
  const schema = useSchema(item?.category);
  const navigate = useNavigate();
  const keyFor = useCategoryKey();

  const openRelated = (category: string, id: number) => navigate(`/catalog/${keyFor(category)}?item=${id}`);

  return (
    <Modal open={itemId !== null} onClose={onClose} wide labelledBy="item-detail-title">
      {detail.isLoading && <div className={styles.loading}><Spinner /></div>}
      {detail.isError && <div className={styles.loading}><ErrorState error={detail.error} onRetry={() => detail.refetch()} /></div>}
      {item && (
        <article className={styles.detail}>
          <button type="button" className={styles.close} onClick={onClose} aria-label="Fechar ficha">×</button>
          <header className={styles.hero}>
            <ItemArt item={item} size="detail" />
            <div className={styles.heroInfo}>
              <span className={styles.code}>
                {CATEGORY_CODES[item.category]}-{String(item.id).padStart(4, '0')} · {schema.data?.label ?? ''}
              </span>
              <h2 id="item-detail-title" className={styles.name} data-glitch={item.name}>{item.name}</h2>
              <p className={styles.sub}>
                {item.subtype?.label}
                {item.manufacturer && <> · <span className={styles.maker}>{item.manufacturer.name}</span></>}
              </p>
              <div className={styles.price}>
                {item.price != null ? <>{formatEb(item.price)} <small>eddies</small></> : <small>Sem preço fixo</small>}
                {item.priceCategory && <span className={styles.priceCat}>{PRICE_CATEGORY_LABEL[item.priceCategory] ?? item.priceCategory}</span>}
              </div>
              {item.priceNote && <p className={styles.priceNote}>{item.priceNote}</p>}
              <div className={styles.actions}>
                <FavoriteButton item={item} variant="inline" />
                <CompareButton item={item} variant="inline" />
                <AddToListMenu item={item} />
              </div>
            </div>
          </header>

          <OriginNotice item={item} />

          <div className={styles.columns}>
            <section>
              <h3 className="hud-title">Atributos</h3>
              {schema.data ? <AttributeTable item={item} fields={schema.data.fields} /> : <Spinner />}
            </section>
            <section className={styles.textCol}>
              {item.description && (
                <div>
                  <h3 className="hud-title">Descrição</h3>
                  <p className={styles.text}>{item.description}</p>
                </div>
              )}
              {item.notes && (
                <div>
                  <h3 className="hud-title">Notas</h3>
                  <p className={styles.text}>{item.notes}</p>
                </div>
              )}
              {item.tags.length > 0 && (
                <div>
                  <h3 className="hud-title">Propriedades</h3>
                  <div className={styles.tags}>{item.tags.map((t) => <span key={t}>{t}</span>)}</div>
                </div>
              )}
              <div>
                <h3 className="hud-title">Referência</h3>
                <dl className={styles.reference}>
                  <dt>Sistema</dt><dd>{RULE_SYSTEM_LABEL[item.ruleSystem] ?? item.ruleSystem}</dd>
                  <dt>Fonte</dt><dd>{item.source?.name ?? 'Não informada'}{item.page ? `, p. ${item.page}` : ''}</dd>
                  <dt>Origem</dt><dd>{DATA_ORIGIN_LABEL[item.dataOrigin]}</dd>
                </dl>
              </div>
            </section>
          </div>

          {item.category === 'weapons' && <WeaponRangeSection item={item} />}

          {detail.data!.relations.length > 0 && (
            <section className={styles.relations}>
              {detail.data!.relations.map((group) => (
                <div key={group.key}>
                  <h3 className="hud-title">{group.label} <span className={styles.relCount}>{group.items.length}</span></h3>
                  <div className={styles.relList}>
                    {group.items.map((r) => (
                      <button key={r.id} type="button" onClick={() => openRelated(r.category, r.id)}>
                        <span>{r.name}</span>
                        {r.price != null && <small>{formatEb(r.price)} eb</small>}
                        <ExternalLink size={12} />
                      </button>
                    ))}
                  </div>
                </div>
              ))}
            </section>
          )}
        </article>
      )}
    </Modal>
  );
}

/** The weapon's row of the Core range table (p.173), in meters or 2 m tiles. */
function WeaponRangeSection({ item }: { item: Item }) {
  const [unit, setUnit] = useRangeUnit();
  const weaponClass = item.subtype?.value;
  if (!rangeRowsFor(weaponClass).single) return null;
  return (
    <section className={styles.rangeSection}>
      <div className={styles.rangeHeader}>
        <h3 className="hud-title">Alcance (DV por distância)</h3>
        <RangeUnitToggle unit={unit} onChange={setUnit} />
      </div>
      <WeaponRange weaponClass={weaponClass} autofire={item.attributes.autofire != null} unit={unit} />
      <p className={styles.rangeNote}>
        Core Rulebook p.173, pela classe da arma. Tile = quadrado de 2 m/yds.
        {item.attributes.exotic ? ' Armas exóticas podem ter regra própria na descrição.' : ''}
      </p>
    </section>
  );
}

function OriginNotice({ item }: { item: Item }) {
  if (item.dataOrigin === 'Custom') return null;
  if (item.dataOrigin === 'Verified') {
    const where = [item.source?.name, item.page ? `p. ${item.page}` : null].filter(Boolean).join(', ');
    return (
      <div className={clsx(styles.notice, styles.noticeVerified)} role="note">
        <BadgeCheck size={16} />
        {`Conferido com o PDF oficial${where ? ` (${where})` : ''}. As notas da ficha registram o que mudou em relação ao compêndio.`}
      </div>
    );
  }
  return (
    <div className={styles.notice} role="note">
      <AlertTriangle size={16} />
      {item.dataOrigin === 'Demo'
        ? 'Registro de demonstração, criado para testar o catálogo. Não é um dado oficial dos livros.'
        : 'Dados importados do compêndio fornecido, ainda não conferidos com os livros oficiais. Valores derivados (alcance máximo, munição compatível) seguem a classe do item.'}
    </div>
  );
}

function AttributeTable({ item, fields }: { item: Item; fields: FieldDescriptor[] }) {
  const subtype = item.subtype?.value;
  const rows = fields.filter((f) =>
    !f.base && f.kind !== 'longtext' &&
    (!f.appliesTo?.length || !subtype || f.appliesTo.includes(subtype)));

  const groups = new Map<string, { field: FieldDescriptor; text: string; unit?: string }[]>();
  const shown = new Set<string>();
  for (const field of rows) {
    const value = formatAttribute(field, item.attributes[field.key]);
    if (value.empty) continue;
    // "Dano 3d6" and "Dano (nº de d6) 3d6" carry the same information; keep the first.
    if (field.unit === 'd6' && shown.has(value.text)) continue;
    shown.add(value.text);
    const group = field.group ?? 'Geral';
    if (!groups.has(group)) groups.set(group, []);
    groups.get(group)!.push({ field, text: value.text, unit: value.unit });
  }
  const longTexts = fields.filter((f) => f.kind === 'longtext' && item.attributes[f.key] && item.attributes[f.key] !== item.description);

  return (
    <div className={styles.attrGroups}>
      {[...groups.entries()].map(([group, entries]) => (
        <div key={group}>
          {groups.size > 1 && <p className={styles.groupName}>{group}</p>}
          <dl className={styles.attrs}>
            {entries.map(({ field, text, unit }) => (
              <div key={field.key} title={field.description ?? undefined}>
                <dt>{field.label}</dt>
                <dd>{text}{unit && <small> {unit}</small>}</dd>
              </div>
            ))}
          </dl>
        </div>
      ))}
      {longTexts.map((f) => (
        <div key={f.key}>
          <p className={styles.groupName}>{f.label}</p>
          <p className={styles.text}>{String(item.attributes[f.key])}</p>
        </div>
      ))}
    </div>
  );
}
