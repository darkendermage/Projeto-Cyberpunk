import type { ReactNode } from 'react';
import { Cpu, Crosshair, Heart, Package, Shield, Sparkles, Swords } from 'lucide-react';
import type { NpcSheet, NpcWeapon } from '../../api/npcCompendium';
import { SheetPanel } from '../characters/CharacterUi';
import styles from '../characters/Characters.module.css';
import dossier from './NpcDossier.module.css';
import { t } from '../../i18n';
import { formatPrinted, RANGE_BANDS, STAT_ORDER } from './npcFormat';

/** STATs in book order, only the keys the sheet prints. A differing armored value shows in parentheses, like the book. */
export function NpcStats({ sheet }: { sheet: NpcSheet }) {
  const keys = STAT_ORDER.filter((k) => k in sheet.stats);
  return (
    <div className={`${styles.column} ${styles.statColumn}`} role="list" aria-label={t('Atributos', 'STATs')}>
      {keys.map((k) => {
        const armored = sheet.armoredStats[k];
        const differs = armored != null && armored !== sheet.stats[k];
        return (
          <div key={k} className={styles.stat} role="listitem">
            <span className={styles.statKey}>{k}</span>
            <span className={styles.statValue} data-changed={differs ? 'down' : undefined}>
              {formatPrinted(sheet.stats[k])}{differs && <span className={styles.statBase}> ({armored})</span>}
            </span>
          </div>
        );
      })}
    </div>
  );
}

export function NpcHealth({ sheet }: { sheet: NpcSheet }) {
  return (
    <SheetPanel title={t('Saúde', 'Health')} icon={<Heart size={16} />}>
      <dl className={styles.kv}>
        <dt>HP</dt><dd>{sheet.hp}</dd>
        <dt>Seriously Wounded</dt><dd>{formatPrinted(sheet.seriouslyWounded)}</dd>
        <dt>Death Save</dt><dd>{formatPrinted(sheet.deathSave)}</dd>
      </dl>
    </SheetPanel>
  );
}

export function NpcWeapons({ weapons }: { weapons: NpcWeapon[] }) {
  if (weapons.length === 0) return null;
  return (
    <SheetPanel title={t('Armas', 'Weapons')} icon={<Swords size={16} />}>
      <ul className={styles.list}>
        {weapons.map((w) => (
          <li key={w.name} className={styles.listItem}>
            <div className={styles.grow}>
              <div className={styles.row}>
                <strong>{w.name}</strong>
                {w.class && <span className={styles.tag}>{w.class}</span>}
                {w.melee && <span className={styles.tag}>{t('corpo a corpo', 'melee')}</span>}
              </div>
              <div className={styles.small}>
                {w.skill} {w.attackBase} · {w.damage} · ROF {w.rof}
                {w.magazine != null && <> · {t('Pente', 'Mag')} {w.magazine}</>}
                {w.autofireMultiplier != null && <> · Autofire x{w.autofireMultiplier}</>}
              </div>
              {w.rangeDv && (
                <div className={dossier.scroll}>
                <table className={styles.table}>
                  <caption>{t('DV por distância (m)', 'DV by range (m)')}</caption>
                  <thead><tr>{RANGE_BANDS.map((b) => <th key={b} className={styles.num}>{b}</th>)}</tr></thead>
                  <tbody><tr>{RANGE_BANDS.map((b, i) => <td key={b} className={styles.num}>{formatPrinted(w.rangeDv?.[i])}</td>)}</tr></tbody>
                </table>
                </div>
              )}
              {w.notes && <div className={`${styles.small} ${styles.muted}`}>{w.notes}</div>}
            </div>
          </li>
        ))}
      </ul>
    </SheetPanel>
  );
}

export function NpcArmor({ armor }: { armor: NpcSheet['armor'] }) {
  if (armor.length === 0) return null;
  return (
    <SheetPanel title={t('Armadura', 'Armor')} icon={<Shield size={16} />}>
      <ul className={styles.list}>
        {armor.map((a) => (
          <li key={`${a.location}-${a.name}`} className={styles.listItem}>
            <span className={styles.small}>
              <strong>{a.location}</strong>: {a.name} · SP {a.sp}
              {a.spVsMelee != null && <> ({t('SP vs. corpo a corpo', 'SP vs. melee')} {a.spVsMelee})</>}
            </span>
          </li>
        ))}
      </ul>
    </SheetPanel>
  );
}

export function NpcSkills({ skills }: { skills: Record<string, number> }) {
  const entries = Object.entries(skills).sort(([a], [b]) => a.localeCompare(b));
  if (entries.length === 0) return null;
  return (
    <SheetPanel title={t('Perícias', 'Skills')} icon={<Crosshair size={16} />}>
      <ul className={styles.list}>
        {entries.map(([name, base]) => (
          <li key={name} className={styles.listItem}><span className={styles.small}>{name} {base}</span></li>
        ))}
      </ul>
    </SheetPanel>
  );
}

function TextList({ title, icon, items }: { title: string; icon: ReactNode; items: string[] }) {
  if (items.length === 0) return null;
  return (
    <SheetPanel title={title} icon={icon}>
      <ul className={styles.list}>
        {items.map((i) => <li key={i} className={styles.listItem}><span className={styles.small}>{i}</span></li>)}
      </ul>
    </SheetPanel>
  );
}

export const NpcGear = ({ gear }: { gear: string[] }) => <TextList title={t('Equipamento', 'Gear')} icon={<Package size={16} />} items={gear} />;
export const NpcCyberware = ({ cyberware }: { cyberware: string[] }) => <TextList title="Cyberware" icon={<Cpu size={16} />} items={cyberware} />;

export function NpcSpecials({ specials }: { specials: NpcSheet['specials'] }) {
  if (specials.length === 0) return null;
  return (
    <SheetPanel title={t('Regras especiais', 'Special rules')} icon={<Sparkles size={16} />}>
      <ul className={styles.list}>
        {specials.map((s) => (
          <li key={s.name} className={styles.listItem}>
            <span className={styles.small}><strong>{s.name}</strong>: {s.text}</span>
          </li>
        ))}
      </ul>
    </SheetPanel>
  );
}
