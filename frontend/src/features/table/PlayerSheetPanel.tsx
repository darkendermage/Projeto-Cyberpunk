import type { PlayerSheet } from '../../api/tableTypes';
import { skillName, t } from '../../i18n';
import { STAT_LABEL } from '../characters/characterLabels';
import { groupSkills } from './sheet';
import styles from './Table.module.css';

const signed = (n: number) => (n >= 0 ? `+${n}` : `${n}`);
const sp = (now: number, max: number | null) => `SP ${now}${max != null ? `/${max}` : ''}`;

/** The player's own sheet to read at the table: nothing is edited here, the GM's app keeps the sheet. */
export function PlayerSheetPanel({ sheet }: { sheet: PlayerSheet | null }) {
  if (!sheet) return <span className={styles.muted}>{t('O mestre não ligou um personagem a este convite.', "The GM didn't link a character to this invite.")}</span>;
  return (
    <div className={styles.stack}>
      <div className={styles.stack} style={{ gap: 2 }}>
        <span className={styles.heroName}>{sheet.handle ?? sheet.name}</span>
        {sheet.handle && <span className={styles.muted}>{sheet.name}</span>}
        {sheet.roles.map((r) => <span key={r} className={styles.muted}>{r}</span>)}
      </div>
      <div className={styles.row}>
        <span><span className={styles.label}>HP </span><span className={styles.stat}>{sheet.hp}/{sheet.maxHp}</span></span>
        <span><span className={styles.label}>{t('Humanidade', 'Humanity')} </span><span className={styles.stat}>{sheet.humanity}/{sheet.maxHumanity}</span></span>
      </div>
      <div className={styles.sheetStats} role="list" aria-label={t('Atributos', 'Stats')}>
        {sheet.stats.map((s) => (
          <span key={s.key} role="listitem" className={styles.sheetStat} title={STAT_LABEL[s.key] ?? s.key}>
            <span className={styles.label}>{s.key}</span>
            <span className={styles.stat}>{s.value}</span>
          </span>
        ))}
      </div>
      <div className={styles.section}>
        <span className={styles.label}>{t('Armadura', 'Armor')}</span>
        <span className={styles.row}>
          <span className={styles.muted}>{t('Cabeça', 'Head')}</span>
          <span className={styles.stat}>{sp(sheet.armor.spHead, sheet.armor.maxSpHead)}</span>
          {sheet.headArmor && <span className={styles.muted}>{sheet.headArmor}</span>}
        </span>
        <span className={styles.row}>
          <span className={styles.muted}>{t('Corpo', 'Body')}</span>
          <span className={styles.stat}>{sp(sheet.armor.spBody, sheet.armor.maxSpBody)}</span>
          {sheet.bodyArmor && <span className={styles.muted}>{sheet.bodyArmor}</span>}
        </span>
      </div>
      {sheet.weapons.length > 0 && (
        <div className={styles.section}>
          <span className={styles.label}>{t('Armas', 'Weapons')}</span>
          {sheet.weapons.map((w, i) => (
            <div key={`${w.name}-${i}`} className={styles.rowBetween}>
              <span>{w.name}{w.equipped ? '' : ` · ${t('guardada', 'stowed')}`}</span>
              <span className={styles.stat}>{[w.damage, w.rof != null ? `ROF ${w.rof}` : null, w.magazine != null ? `${w.rounds}/${w.magazine}` : null].filter(Boolean).join(' · ')}</span>
            </div>
          ))}
        </div>
      )}
      {sheet.cyberware.length > 0 && (
        <div className={styles.section}>
          <span className={styles.label}>Cyberware</span>
          <span>{sheet.cyberware.join(' · ')}</span>
        </div>
      )}
      {sheet.gear.length > 0 && (
        <div className={styles.section}>
          <span className={styles.label}>{t('Equipamento', 'Gear')}</span>
          <span>{sheet.gear.map((g) => (g.quantity > 1 ? `${g.name} ×${g.quantity}` : g.name)).join(' · ')}</span>
        </div>
      )}
      <div className={styles.section}>
        <span className={styles.label}>{t('Perícias com nível', 'Trained skills')}</span>
        {groupSkills(sheet.skills).map((g) => (
          <div key={g.key} className={styles.stack} style={{ gap: 2 }}>
            <span className={styles.source}>{g.label}</span>
            {g.skills.map((s) => (
              <span key={`${s.name}-${s.specialization ?? ''}`} className={styles.rowBetween}>
                <span>{skillName(s.name, s.namePt)}{s.specialization ? ` (${s.specialization})` : ''}</span>
                <span className={styles.stat}>{s.level} · {signed(s.total)}</span>
              </span>
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}
