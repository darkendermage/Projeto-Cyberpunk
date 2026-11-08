import { useMemo, useState } from 'react';
import { TrendingUp } from 'lucide-react';
import type { CharacterDetail, CharacterRules } from '../../api/characterTypes';
import { Button, ComboBox, Modal, Select } from '../../components/ui/ui';
import { MARTIAL_ARTS } from '../reference/martialArts';
import { languagesFor } from './characterLabels';
import { improvementGroups, specializationChoices } from './skillImprovement';
import { SheetPanel } from './CharacterUi';
import type { Run } from './CombatPanels';
import styles from './Characters.module.css';
import { t } from '../../i18n';

/**
 * Improvement Points (CP:R p.408–410): award, raise a skill or a Role Ability, and take up a new Role (CP:R p.142).
 * Costs come from the server report; the server refuses anything the character can't afford.
 */
export function ProgressionPanel({ detail, rules, run, busy }: { detail: CharacterDetail; rules: CharacterRules; run: Run; busy: boolean }) {
  const [open, setOpen] = useState(false);
  if (!detail.sheet.creation.finalized) return null;
  // Compact on the sheet (round 7): the IP at a glance; awarding and spending them in a window.
  return (
    <SheetPanel title={t('Progressão', 'Progression')} icon={<TrendingUp size={16} />}
      actions={<Button variant="primary" onClick={() => setOpen(true)}>{t('Distribuir IP', 'Spend IP')}</Button>}>
      <div className={styles.bigNumbers}>
        <strong>{detail.sheet.progress.ip}</strong><span className={styles.muted}>{t('IP disponíveis · ', 'IP available · ')}{detail.sheet.progress.ipEarned} {t('ganhos', 'earned')}</span>
      </div>
      <Modal open={open} onClose={() => setOpen(false)} title={t('Progressão', 'Progression')} wide>
        <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
          <ProgressionForm detail={detail} rules={rules} run={run} busy={busy} />
        </div>
      </Modal>
    </SheetPanel>
  );
}

function ProgressionForm({ detail, rules, run, busy }: { detail: CharacterDetail; rules: CharacterRules; run: Run; busy: boolean }) {
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [skill, setSkill] = useState('');
  const [spec, setSpec] = useState('');
  const [newRole, setNewRole] = useState('');
  const ip = detail.sheet.progress.ip;
  const report = detail.report;
  const current = report.roles.find((r) => r.current);
  const groups = useMemo(() => improvementGroups(rules.skills, report.skills), [rules.skills, report.skills]);
  const picked = groups.flatMap((g) => g.options).find((o) => o.value === skill);
  const choices = picked?.isNew
    ? specializationChoices(picked.skillId, report.skills.map((s) => s.key), {
      languages: languagesFor(rules, null), homes: rules.homes, martialArts: MARTIAL_ARTS.map((m) => m.name),
    })
    : null;
  const skillKey = picked?.isNew ? (spec.trim() ? `${picked.skillId}:${spec.trim()}` : '') : picked?.value ?? '';
  const others = rules.roles.filter((r) => !report.roles.some((x) => x.role === r.id));
  const canMulticlass = (current?.rank ?? 0) >= 4;

  return (
    <>
      <span className={styles.source}>CP:R p.408–410, p.142</span>
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
        <Select className={styles.grow} value={skill} onChange={(e) => { setSkill(e.target.value); setSpec(''); }} aria-label={t('Perícia a melhorar', 'Skill to improve')}>
          <option value="">{t('Melhorar ou aprender perícia…', 'Improve or learn a skill…')}</option>
          {groups.map((g) => (
            <optgroup key={g.category} label={g.label}>
              {g.options.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.isNew ? `${o.label} (${o.cost} IP)` : `${o.label} ${o.level} → ${o.level + 1} (${o.cost} IP)`}
                </option>
              ))}
            </optgroup>
          ))}
        </Select>
        {choices && (choices.free
          ? <ComboBox className={styles.grow} value={spec} onChange={setSpec} options={choices.options} aria-label={t('Especialização', 'Specialization')}
              placeholder={t('Escolha ou digite…', 'Pick or type…')} />
          : (
            <Select className={styles.grow} value={spec} onChange={(e) => setSpec(e.target.value)} aria-label={t('Especialização', 'Specialization')}>
              <option value="">{t('Escolha…', 'Choose…')}</option>
              {choices.options.map((o) => <option key={o} value={o}>{o}</option>)}
            </Select>
          ))}
        <Button disabled={busy || !skillKey || !picked || picked.cost > ip}
          title={picked && picked.cost > ip ? t(`Faltam ${picked.cost - ip} IP.`, `${picked.cost - ip} IP short.`) : undefined}
          onClick={async () => {
            if (await run('progression/skill', { skillKey }, picked?.isNew ? t('Perícia aprendida.', 'Skill learned.') : t('Perícia melhorada.', 'Skill improved.'))) { setSkill(''); setSpec(''); }
          }}>
          {picked?.isNew || picked?.level === 0 ? t('Aprender', 'Learn') : t('Subir', 'Raise')}{picked ? ` (${picked.cost} IP)` : ''}
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
    </>
  );
}
