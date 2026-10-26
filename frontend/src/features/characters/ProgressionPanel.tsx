import { useState } from 'react';
import { TrendingUp } from 'lucide-react';
import type { CharacterDetail, CharacterRules } from '../../api/characterTypes';
import { Button, Select } from '../../components/ui/ui';
import { skillLabel } from './characterLabels';
import { SheetPanel } from './CharacterUi';
import type { Run } from './CombatPanels';
import styles from './Characters.module.css';
import { t } from '../../i18n';

/**
 * Improvement Points (CP:R p.408–410): award, raise a skill or a Role Ability, and take up a new Role (CP:R p.142).
 * Costs come from the server report; the server refuses anything the character can't afford.
 */
export function ProgressionPanel({ detail, rules, run, busy }: { detail: CharacterDetail; rules: CharacterRules; run: Run; busy: boolean }) {
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [skill, setSkill] = useState('');
  const [newSkill, setNewSkill] = useState('');
  const [newRole, setNewRole] = useState('');
  const ip = detail.sheet.progress.ip;
  const report = detail.report;
  const current = report.roles.find((r) => r.current);
  const skills = report.skills.filter((s) => s.level > 0 && s.ipToRaise != null);
  const picked = report.skills.find((s) => s.key === skill);
  const others = rules.roles.filter((r) => !report.roles.some((x) => x.role === r.id));
  const canMulticlass = (current?.rank ?? 0) >= 4;

  if (!detail.sheet.creation.finalized) return null;

  return (
    <SheetPanel title={t('Progressão', 'Progression')} icon={<TrendingUp size={16} />} actions={<span className={styles.source}>CP:R p.408–410, p.142</span>}>
      <div className={styles.bigNumbers}>
        <strong>{ip}</strong><span className={styles.muted}>{t('IP disponíveis · ', 'IP available · ')}{detail.sheet.progress.ipEarned} {t('ganhos', 'earned')}</span>
      </div>

      <div className={styles.row}>
        <input className={`${styles.input} ${styles.inputSmall}`} style={{ width: 80 }} type="number" value={amount} placeholder="IP"
          aria-label={t('IP recebidos', 'IP received')} onChange={(e) => setAmount(e.target.value)} />
        <input className={`${styles.input} ${styles.grow}`} value={reason} placeholder={t('Sessão, missão…', 'Session, mission…')} aria-label={t('Motivo dos IP', 'Reason for the IP')}
          onChange={(e) => setReason(e.target.value)} />
        <Button disabled={busy || !Number(amount) || !reason.trim()} onClick={async () => {
          if (await run('progression/ip', { amount: Number(amount), reason }, t('IP registrados.', 'IP recorded.'))) { setAmount(''); setReason(''); }
        }}>{t('Receber', 'Receive')}</Button>
      </div>

      <div className={styles.row}>
        <Select value={skill} onChange={(e) => setSkill(e.target.value)} aria-label={t('Perícia a melhorar', 'Skill to improve')}>
          <option value="">{t('Melhorar perícia…', 'Improve skill…')}</option>
          {skills.map((s) => <option key={s.key} value={s.key}>{skillLabel(s)} {s.level} → {s.level + 1} ({s.ipToRaise} IP)</option>)}
        </Select>
        <Button disabled={busy || !picked || (picked.ipToRaise ?? Infinity) > ip}
          onClick={async () => { if (await run('progression/skill', { skillKey: skill }, t('Perícia melhorada.', 'Skill improved.'))) setSkill(''); }}>
          {t('Subir', 'Raise')}{picked ? ` (${picked.ipToRaise} IP)` : ''}
        </Button>
      </div>
      <div className={styles.row}>
        <input className={styles.input} style={{ maxWidth: 260 }} value={newSkill} placeholder={t('Nova perícia (ex.: language:Japanese)', 'New skill (e.g. language:Japanese)')}
          aria-label={t('Nova perícia', 'New skill')} onChange={(e) => setNewSkill(e.target.value)} />
        <Button variant="ghost" disabled={busy || !newSkill.trim() || ip < 20}
          onClick={async () => { if (await run('progression/skill', { skillKey: newSkill.trim() }, t('Perícia aprendida.', 'Skill learned.'))) setNewSkill(''); }}>
          {t('Aprender no nível 1', 'Learn at level 1')}
        </Button>
      </div>

      {report.roles.map((r) => (
        <div key={r.role} className={styles.row}>
          <span className={styles.grow}>{r.ability} ({r.role}) — Rank {r.rank}</span>
          <Button disabled={busy || r.ipToRaise == null || r.ipToRaise > ip}
            onClick={() => run('progression/role', { role: r.role }, t(`${r.ability} no Rank ${r.rank + 1}.`, `${r.ability} at Rank ${r.rank + 1}.`))}>
            Rank {r.rank + 1}{r.ipToRaise != null ? ` (${r.ipToRaise} IP)` : ''}
          </Button>
        </div>
      ))}

      <div className={styles.row}>
        <Select value={newRole} onChange={(e) => setNewRole(e.target.value)} aria-label={t('Nova Role', 'New Role')} disabled={!canMulticlass}>
          <option value="">{canMulticlass ? t('Nova Role (multiclasse)…', 'New Role (multiclass)…') : t(`Multiclasse exige ${current?.role ?? 'a Role atual'} no Rank 4`, `Multiclassing requires ${current?.role ?? 'the current Role'} at Rank 4`)}</option>
          {others.map((r) => <option key={r.id} value={r.id}>{r.id} — {r.ability}</option>)}
        </Select>
        <Button disabled={busy || !newRole || ip < 60}
          onClick={async () => { if (await run('progression/multiclass', { role: newRole }, t(`Agora também ${newRole}.`, `Now also ${newRole}.`))) setNewRole(''); }}>
          {t('Assumir (Rank 1)', 'Take it (Rank 1)')}
        </Button>
      </div>
    </SheetPanel>
  );
}
