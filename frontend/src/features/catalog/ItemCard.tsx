import { memo } from 'react';
import clsx from 'clsx';
import type { CategorySchema, Item } from '../../api/types';
import { ItemArt } from '../../components/art/ItemArt';
import { cardStats, DATA_ORIGIN_LABEL, formatEb, shortLabel } from '../../lib/format';
import { CATEGORY_CODES } from './categoryMeta';
import { CompareButton, FavoriteButton } from './ItemActions';
import styles from './ItemCard.module.css';

const ORIGIN_BADGE: Record<string, string> = { Demo: 'DEMO', Custom: 'CUSTOM', Verified: 'CONFERIDO' };

interface Props {
  item: Item;
  schema: CategorySchema | undefined;
  onOpen: (item: Item) => void;
}

function sourceBadge(item: Item) {
  if (item.page) return `p. ${item.page}`;
  return item.source?.name ?? null;
}

function ItemCardImpl({ item, schema, onOpen }: Props) {
  const stats = schema ? cardStats(schema, item, 4) : [];
  const badge = sourceBadge(item);
  const chips = [...new Set([item.subtype?.label, ...item.tags].filter(Boolean))].slice(0, 3) as string[];

  return (
    <article className={styles.card}>
      <ItemArt item={item} size="card">
        <span className={styles.code} aria-hidden>{CATEGORY_CODES[item.category] ?? 'ITM'}-{String(item.id).padStart(4, '0')}</span>
        {item.dataOrigin !== 'Compendium' && (
          <span className={clsx(styles.origin, item.dataOrigin === 'Demo' && styles.originDemo, item.dataOrigin === 'Verified' && styles.originVerified)}
            title={DATA_ORIGIN_LABEL[item.dataOrigin]}>
            {ORIGIN_BADGE[item.dataOrigin]}
          </span>
        )}
      </ItemArt>
      <div className={styles.overlayActions}>
        <CompareButton item={item} />
        <FavoriteButton item={item} />
      </div>

      <div className={styles.body}>
        <div className={styles.titleRow}>
          <h3 className={styles.name}>
            <button type="button" className={styles.open} onClick={() => onOpen(item)}>{item.name}</button>
          </h3>
          {badge && <span className={styles.badge} title={item.source?.name}>{badge}</span>}
        </div>

        <div className={styles.metaRow}>
          <span className={styles.subtype}>{item.subtype?.label}</span>
          <span className={styles.price}>
            {item.price != null ? <>{formatEb(item.price)} <small>eddies</small></> : <small>{item.priceNote ?? 'sem preço'}</small>}
          </span>
        </div>

        {item.description && <p className={styles.description}>{item.description}</p>}

        {stats.length > 0 && (
          <dl className={styles.stats} style={{ gridTemplateColumns: `repeat(${stats.length}, 1fr)` }}>
            {stats.map(({ field, value }) => (
              <div key={field.key} title={field.label}>
                <dt>{shortLabel(field)}</dt>
                <dd>{value.text}{value.unit && <small> {value.unit}</small>}</dd>
              </div>
            ))}
          </dl>
        )}

        <div className={styles.footer}>
          <div className={styles.chips}>
            {chips.map((c) => <span key={c} className={styles.chip}>{c}</span>)}
          </div>
        </div>
      </div>
    </article>
  );
}

export const ItemCard = memo(ItemCardImpl);
