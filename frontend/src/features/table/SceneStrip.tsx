import { t } from '../../i18n';
import { WOUND_LABEL } from '../combat/combat';
import { ArmorControl } from './ArmorControl';
import type { StripEntry } from './strip';
import { initials } from './tokens';
import styles from './Table.module.css';

/**
 * "On the scene": a card per character and fighter under the map — picture, name, HP and the SP of head and body
 * (NPCs show only their Wound State to the players). A click selects the token.
 */
export function SceneStrip({ entries, selected, onPick }: { entries: StripEntry[]; selected: readonly string[]; onPick: (tokenId: string) => void }) {
  if (entries.length === 0) return null;
  return (
    <div className={styles.strip} role="list" aria-label={t('Na cena', 'On the scene')}>
      {entries.map((e) => {
        const ratio = e.hp != null && e.maxHp ? Math.max(0, Math.min(1, e.hp / e.maxHp)) : null;
        return (
          <button key={e.tokenId} type="button" role="listitem" className={`${styles.stripCard} ${selected.includes(e.tokenId) ? styles.stripCardOn : ''}`}
            onClick={() => onPick(e.tokenId)} style={{ borderColor: e.color }}>
            {e.imageUrl
              ? <img className={styles.stripAvatar} src={e.imageUrl} alt="" />
              : <span className={styles.stripAvatar} style={{ background: e.color }}>{initials(e.name)}</span>}
            <span className={styles.stripBody}>
              <span className={styles.stripName}>{e.name}</span>
              {ratio != null ? (
                <span className={styles.stripHp}>
                  <span className={styles.meter}><span className={styles.meterFill} style={{ width: `${ratio * 100}%` }} /></span>
                  <span className={styles.stat}>{e.hp}/{e.maxHp}</span>
                </span>
              ) : (
                <span className={styles.muted}>{e.dead ? t('Morto', 'Dead') : e.woundState ? WOUND_LABEL[e.woundState] : ''}</span>
              )}
              {(e.spHead != null || e.spBody != null) && <ArmorControl compact armor={e} />}
            </span>
          </button>
        );
      })}
    </div>
  );
}
