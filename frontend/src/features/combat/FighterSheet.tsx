import type { ReactNode } from 'react';
import type { FighterView, NamedValue } from '../../api/encounterTypes';
import { Badge } from '../../components/ui/ui';
import { WOUND_LABEL, WOUND_TONE } from './combat';
import styles from './Combat.module.css';
import { t, tv } from '../../i18n';

/** Read-only sheet of an NPC or generic enemy: the snapshot it joined the fight with, plus its running state. */
export function FighterSheet({ f }: { f: FighterView }) {
  const stats = f.stats && f.stats.length > 0 ? f.stats : [
    { id: 'REF', label: 'REF', value: f.ref }, { id: 'DEX', label: 'DEX', value: f.dex }, { id: 'BODY', label: 'BODY', value: f.body },
    { id: 'WILL', label: 'WILL', value: f.will }, { id: 'MOVE', label: 'MOVE', value: f.move },
  ];
  return (
    <section className={styles.panel} aria-label={t(`Ficha de ${tv(f.name)}`, `Sheet of ${tv(f.name)}`)}>
      <div className={styles.rowBetween}>
        <div>
          <h2 className={styles.panelTitle}>{tv(f.name)}</h2>
          <span className={styles.muted}>{f.kind === 'Generic' ? t('Inimigo genérico', 'Generic enemy') : 'NPC'}</span>
        </div>
        <span className={styles.row}>
          <Badge tone={f.side === 'Enemy' ? 'red' : 'cyan'}>{f.side === 'Enemy' ? t('Inimigo', 'Enemy') : t('Aliado', 'Ally')}</Badge>
          <Badge tone={WOUND_TONE[f.woundState]}>{WOUND_LABEL[f.woundState]}</Badge>
        </span>
      </div>
      <dl className={styles.stats}>
        <div><dt>HP</dt><dd>{f.hp}/{f.maxHp}</dd></div>
        <div><dt>{t('SP corpo', 'Body SP')}</dt><dd>{f.spBody}/{f.maxSpBody}</dd></div>
        <div><dt>{t('SP cabeça', 'Head SP')}</dt><dd>{f.spHead}/{f.maxSpHead}</dd></div>
        <div><dt>{t('Penalidade', 'Penalty')}</dt><dd>{f.actionPenalty}</dd></div>
      </dl>
      <Values title="STATs" values={stats} />
      <Block title={t('Armas e ataques', 'Weapons and attacks')}>
        <ul className={styles.sheetList}>
          {f.weapons.map((w) => (
            <li key={w.key}>
              <strong>{tv(w.name)}</strong> {tv(w.damage)} · ROF {w.rof} {t('· ataque +', '· attack +')}{w.attackTotal}
              {w.autofireTotal != null && ` · autofire +${w.autofireTotal}`}
              {w.magazine != null && t(` · ${w.rounds ?? 0}/${w.magazine} tiros`, ` · ${w.rounds ?? 0}/${w.magazine} rounds`)}
            </li>
          ))}
        </ul>
      </Block>
      {f.skills && f.skills.length > 0 && <Values title="Skills" values={f.skills} list />}
      {f.injuries.length > 0 && (
        <Block title="Critical Injuries">
          <ul className={styles.injuries}>
            {f.injuries.map((i) => <li key={i.name}><strong>{tv(i.name)}</strong> ({i.location === 'Head' ? t('cabeça', 'head') : t('corpo', 'body')}){i.effect ? ` — ${tv(i.effect)}` : ''}</li>)}
          </ul>
        </Block>
      )}
      <span className={styles.source}>{t('Ficha registrada quando entrou no combate', 'Sheet recorded when they joined the fight')}{f.source ? ` · ${f.source}` : ''}.</span>
    </section>
  );
}

function Block({ title, children }: { title: string; children: ReactNode }) {
  return <div className={styles.block}><span className={styles.label}>{title}</span>{children}</div>;
}

function Values({ title, values, list }: { title: string; values: NamedValue[]; list?: boolean }) {
  return (
    <Block title={title}>
      <dl className={list ? styles.skillList : styles.stats}>
        {values.map((v) => <div key={v.id}><dt>{tv(v.label)}</dt><dd>{tv(v.value)}</dd></div>)}
      </dl>
    </Block>
  );
}
