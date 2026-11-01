import { Fragment, useMemo, useState, type ReactNode } from 'react';
import { BookOpen, Cpu, Crosshair, HeartPulse, Package, Search, Shield, Sparkles, UserRound } from 'lucide-react';
import type { SkillRule } from '../../api/characterTypes';
import type { NpcDetail, NpcSheet, NpcWeapon } from '../../api/npcCompendium';
import { SheetPanel } from '../characters/CharacterUi';
import { STAT_LABEL } from '../characters/characterLabels';
import styles from '../characters/Characters.module.css';
import dossier from './NpcDossier.module.css';
import { t } from '../../i18n';
import { formatPrinted, groupNpcSkills, RANGE_BANDS, STAT_ORDER, weaponClassLabel } from './npcFormat';

const plain = (text: string) => text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();

/** Portrait frame of the sheet (the books' art isn't in the app) and the identity block. */
export function NpcIdentity({ npc, role }: { npc: NpcDetail; role?: ReactNode }) {
  return (
    <>
      <div className={styles.portrait} role="img" aria-label={npc.name}>
        <div className={styles.portraitEmpty}><UserRound size={72} /></div>
      </div>
      <section className={styles.panel}>
        <div className={styles.idField}>
          <span className={styles.idLabel}>{t('Nome', 'Name')}</span>
          <span className={styles.idValue}>{npc.name}</span>
        </div>
        {npc.aliases.length > 0 && (
          <div className={styles.idField}>
            <span className={styles.idLabel}>Handle</span>
            <span>{npc.aliases.join(' · ')}</span>
          </div>
        )}
        <div className={styles.idField}>
          <span className={styles.idLabel}>Role</span>
          <div className={styles.row}>
            <span className={styles.idValue}>{npc.role ?? '—'}</span>
            <span className={styles.grow} />
            {npc.roleRank != null && <span className={styles.abilityRank} title={t('Rank da Habilidade de Role', 'Role Ability Rank')}>{npc.roleRank}</span>}
          </div>
          {role}
        </div>
        <dl className={styles.kv}>
          <dt>{t('Reputação', 'Reputation')}</dt><dd>{formatPrinted(npc.rep)}</dd>
          {npc.affiliation && <><dt>{t('Afiliação', 'Affiliation')}</dt><dd>{npc.affiliation}</dd></>}
        </dl>
      </section>
    </>
  );
}

export function NpcBiography({ npc }: { npc: NpcDetail }) {
  return (
    <SheetPanel title={t('Biografia', 'Biography')} icon={<BookOpen size={16} />}>
      <p className={dossier.biography}>{npc.summary}</p>
      {npc.sheetNote && <p className={`${styles.small} ${styles.muted}`}>{npc.sheetNote}</p>}
      <p className={styles.source}>{npc.sources.map((s) => `${s.book} p.${s.page}`).join(' · ')}</p>
    </SheetPanel>
  );
}

/** STATs like the character sheet: the value in use is the big one, with the printed base under it when armor changes it. */
export function NpcStats({ sheet }: { sheet: NpcSheet }) {
  const keys = STAT_ORDER.filter((k) => k in sheet.stats);
  return (
    <div className={`${styles.column} ${styles.statColumn}`} role="list" aria-label={t('Atributos', 'STATs')}>
      {keys.map((k) => {
        const armored = sheet.armoredStats[k];
        const differs = armored != null && armored !== sheet.stats[k];
        return (
          <div key={k} className={styles.stat} role="listitem" title={STAT_LABEL[k] ?? k}>
            <span className={styles.statKey}>{k}</span>
            <span className={styles.statValue} data-changed={differs ? 'down' : undefined}>{formatPrinted(differs ? armored : sheet.stats[k])}</span>
            {differs && <span className={styles.statBase}>base {sheet.stats[k]}</span>}
          </div>
        );
      })}
    </div>
  );
}

export function NpcSkills({ sheet, rules }: { sheet: NpcSheet; rules: SkillRule[] }) {
  const [query, setQuery] = useState('');
  const groups = useMemo(() => {
    const q = plain(query.trim());
    return groupNpcSkills(sheet.skills, sheet.armoredSkills, rules)
      .map((g) => ({ ...g, lines: g.lines.filter((l) => !q || [l.name, l.label, l.stat ?? ''].some((v) => plain(v).includes(q))) }))
      .filter((g) => g.lines.length > 0);
  }, [sheet, rules, query]);

  return (
    <SheetPanel title={t('Perícias', 'Skills')}>
      <div className={styles.skillToolbar}>
        <label className={styles.search}>
          <Search size={15} aria-hidden />
          <span className="visually-hidden">{t('Buscar perícia', 'Search skill')}</span>
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t('Buscar perícia (PT ou EN)…', 'Search skill (EN or PT)…')} />
        </label>
      </div>
      {groups.length === 0 && <p className={styles.muted}>{t('Nenhuma perícia com esse filtro.', 'No skills with this filter.')}</p>}
      <div className={styles.skillGrid}>
        {groups.map((g) => (
          <div key={g.key} className={styles.skillGroup}>
            <table className={styles.table}>
              <caption>{g.label}</caption>
              <thead><tr><th>{t('Perícia', 'Skill')}</th><th className={styles.num}>BASE</th></tr></thead>
              <tbody>
                {g.lines.map((l) => (
                  <tr key={l.name}>
                    <td title={l.name}>{l.label} {l.stat && <span className={styles.muted}>({l.stat})</span>}</td>
                    <td className={styles.total} title={l.armored != null ? t(`Base ${l.base}; com a armadura, ${l.armored}`, `Base ${l.base}; ${l.armored} with armor`) : undefined}>
                      {l.armored ?? l.base}{l.armored != null && <span className={styles.muted}> ({l.base})</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
      </div>
    </SheetPanel>
  );
}

export function NpcHealth({ sheet }: { sheet: NpcSheet }) {
  const wounded = sheet.seriouslyWounded;
  return (
    <SheetPanel title={t('Pontos de vida', 'Hit Points')} icon={<HeartPulse size={16} />}>
      <div className={styles.bigNumbers}><strong>{sheet.hp}</strong><span className={styles.muted}>HP</span></div>
      <div className={styles.meter} aria-hidden>
        <div className={styles.meterFill} style={{ width: '100%' }} />
        {wounded != null && <div className={styles.meterMark} style={{ left: `${Math.min(100, (wounded / sheet.hp) * 100)}%` }} />}
      </div>
      <dl className={styles.kv}>
        <dt>{t('Seriously Wounded abaixo de', 'Seriously Wounded below')}</dt><dd>{formatPrinted(wounded)}</dd>
        <dt>{t('Death Save (abaixo de)', 'Death Save (below)')}</dt><dd>{formatPrinted(sheet.deathSave)}</dd>
      </dl>
    </SheetPanel>
  );
}

const LOCATION_LABEL: Record<string, string> = { Head: t('Cabeça', 'Head'), Body: t('Corpo', 'Body') };

export function NpcArmor({ armor }: { armor: NpcSheet['armor'] }) {
  return (
    <SheetPanel title={t('Armadura', 'Armor')} icon={<Shield size={16} />}>
      {armor.length === 0 ? <p className={styles.muted}>{t('Nenhuma armadura.', 'No armor.')}</p> : (
        <table className={styles.table}>
          <thead><tr><th>{t('Local', 'Location')}</th><th>{t('Peça', 'Piece')}</th><th className={styles.num}>SP</th></tr></thead>
          <tbody>
            {armor.map((a, index) => (
              <tr key={`${a.location}-${a.name}-${index}`}>
                <td>{LOCATION_LABEL[a.location] ?? a.location}</td>
                <td className={styles.small}>
                  {a.name}
                  {a.spVsMelee != null && <div className={styles.source}>{t(`SP ${a.spVsMelee} vs. corpo a corpo`, `SP ${a.spVsMelee} vs. melee`)}</div>}
                </td>
                <td className={styles.total}>{a.sp}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </SheetPanel>
  );
}

function RangeTable({ weapon }: { weapon: NpcWeapon }) {
  return (
    <div className={dossier.scroll}>
      <table className={styles.table}>
        <caption>{t('DV por distância (m)', 'DV by range (m)')}</caption>
        <thead><tr>{RANGE_BANDS.map((b) => <th key={b} className={styles.num}>{b}</th>)}</tr></thead>
        <tbody><tr>{RANGE_BANDS.map((b, i) => <td key={b} className={styles.num}>{formatPrinted(weapon.rangeDv?.[i])}</td>)}</tr></tbody>
      </table>
    </div>
  );
}

export function NpcWeapons({ weapons }: { weapons: NpcWeapon[] }) {
  return (
    <SheetPanel title={t('Armas', 'Weapons')} icon={<Crosshair size={16} />}>
      {weapons.length === 0 ? <p className={styles.muted}>{t('Nenhuma arma.', 'No weapons.')}</p> : (
        <table className={styles.table}>
          <thead>
            <tr>
              <th>{t('Arma', 'Weapon')}</th><th className={styles.num}>Base</th><th className={styles.num}>{t('Dano', 'Damage')}</th>
              <th className={styles.num}>ROF</th><th className={styles.num}>{t('Pente', 'Mag')}</th>
            </tr>
          </thead>
          <tbody>
            {weapons.map((w, index) => (
              <Fragment key={`${w.name}-${index}`}>
                <tr>
                  <td>
                    <div>{w.name}</div>
                    <div className={styles.source}>
                      {w.skill}
                      {w.class && ` · ${weaponClassLabel(w.class)}`}
                      {w.autofireMultiplier != null && ` · Autofire x${w.autofireMultiplier}`}
                    </div>
                  </td>
                  <td className={styles.total}>{w.attackBase}</td>
                  <td className={styles.num}>{w.damage}</td>
                  <td className={styles.num}>{w.rof}</td>
                  <td className={styles.num}>{formatPrinted(w.magazine)}</td>
                </tr>
                {(w.rangeDv || w.notes) && (
                  <tr className={dossier.weaponDetail}>
                    <td colSpan={5}>
                      {w.rangeDv && <RangeTable weapon={w} />}
                      {w.notes && <div className={styles.source}>{w.notes}</div>}
                    </td>
                  </tr>
                )}
              </Fragment>
            ))}
          </tbody>
        </table>
      )}
    </SheetPanel>
  );
}

function TextList({ title, icon, items }: { title: string; icon: ReactNode; items: string[] }) {
  if (items.length === 0) return null;
  return (
    <SheetPanel title={title} icon={icon}>
      <ul className={styles.list}>
        {items.map((item, index) => <li key={`${item}-${index}`} className={styles.listItem}><span className={styles.small}>{item}</span></li>)}
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
        {specials.map((s, index) => (
          <li key={`${s.name}-${index}`} className={styles.listItem}>
            <span className={styles.small}><strong>{s.name}</strong>: {s.text}</span>
          </li>
        ))}
      </ul>
    </SheetPanel>
  );
}
