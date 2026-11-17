import { useMemo, useState } from 'react';
import { Minus, Plus, TrendingUp } from 'lucide-react';
import type { CharacterDetail, CharacterRules } from '../../api/characterTypes';
import { Button, ComboBox, IconButton, Modal, NumberInput, Select } from '../../components/ui/ui';
import { improvementGroups, specializationChoices, specializationSources } from './skillImprovement';
import type { Direction } from './BalanceDialog';
import type { Run } from './CombatPanels';
import styles from './Characters.module.css';
import { t } from '../../i18n';

// IP (CP:R p.408-410) and multiclassing (p.142). Costs come from the report and the server refuses what
// the character can't afford.

const IP_CHIPS = [5, 10, 20, 40];
const notYet = t('A progressão começa depois da criação', 'Progression starts after creation');

/** − and + next to the IP: receiving IP (every session) and correcting a wrong entry. */
export function IpButtons({ detail, run, busy }: { detail: CharacterDetail; run: Run; busy: boolean }) {
  const [open, setOpen] = useState<Direction | null>(null);
  const finalized = detail.sheet.creation.finalized;
  return (
    <span className={styles.row} style={{ gap: 4, flexWrap: 'nowrap' }}>
      <IconButton label={finalized ? t('Tirar IP (corrigir um lançamento)', 'Take IP (fix an entry)') : notYet}
        disabled={!finalized || busy || detail.sheet.progress.ip <= 0} onClick={() => setOpen('take')}><Minus size={15} /></IconButton>
      <IconButton label={finalized ? t('Receber IP', 'Receive IP') : notYet} disabled={!finalized || busy} onClick={() => setOpen('add')}><Plus size={15} /></IconButton>
      {open && <IpDialog direction={open} detail={detail} run={run} busy={busy} onClose={() => setOpen(null)} />}
    </span>
  );
}

function IpDialog({ direction, detail, run, busy, onClose }: { direction: Direction; detail: CharacterDetail; run: Run; busy: boolean; onClose: () => void }) {
  const [amount, setAmount] = useState<number | null>(null);
  const [reason, setReason] = useState('');
  const ip = detail.sheet.progress.ip;
  const add = direction === 'add';
  const signed = (amount ?? 0) * (add ? 1 : -1);
  const ready = (amount ?? 0) > 0 && reason.trim().length > 0 && ip + signed >= 0;
  const submit = async () => {
    if (!ready) return;
    if (await run('progression/ip', { amount: signed, reason: reason.trim() }, add ? t('IP recebidos.', 'IP received.') : t('IP corrigidos.', 'IP corrected.'))) onClose();
  };
  return (
    <Modal open onClose={onClose} title={add ? t('Receber IP', 'Receive IP') : t('Tirar IP', 'Take IP')}>
      <form className={styles.panel} style={{ clipPath: 'none', border: 'none' }} onSubmit={(e) => { e.preventDefault(); void submit(); }}>
        <div className={styles.formGrid}>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>IP</span>
            <NumberInput className={styles.input} min={1} max={add ? 9999 : Math.max(1, ip)} allowEmpty value={amount} data-autofocus onChange={setAmount} />
          </label>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>{t('Motivo', 'Reason')}</span>
            <input className={styles.input} value={reason} placeholder={add ? t('Sessão, missão…', 'Session, mission…') : t('Lançado a mais na sessão 3…', 'Too much given in session 3…')}
              onChange={(e) => setReason(e.target.value)} />
          </label>
        </div>
        <div className={styles.row} role="group" aria-label={t('Valores rápidos', 'Quick amounts')}>
          {IP_CHIPS.filter((n) => add || n <= ip).map((n) => (
            <Button key={n} variant={amount === n ? 'primary' : 'ghost'} onClick={() => setAmount(n)}>{n} IP</Button>
          ))}
        </div>
        <dl className={styles.kv}>
          <dt>{t('IP agora', 'IP now')}</dt><dd>{ip}</dd>
          <dt>{t('Depois', 'After')}</dt><dd className={signed < 0 ? styles.red : styles.green}>{ip + signed}</dd>
        </dl>
        {!add && <p className={styles.source}>{t('Para desfazer um gasto em perícia ou Rank, use o Desfazer do histórico: ele devolve os IP.', 'To undo a skill or Rank spend, use Undo in the history: it gives the IP back.')}</p>}
        <div className={styles.row}>
          <Button type="submit" variant="primary" disabled={busy || !ready}>{add ? t('Receber', 'Receive') : t('Tirar', 'Take')}{amount ? ` ${amount} IP` : ''}</Button>
          <Button variant="ghost" onClick={onClose}>{t('Cancelar', 'Cancel')}</Button>
        </div>
      </form>
    </Modal>
  );
}

/** "Evoluir": spending IP on skills, Role Ranks and a new Role, in a window. */
export function ProgressionButton({ detail, rules, run, busy }: { detail: CharacterDetail; rules: CharacterRules; run: Run; busy: boolean }) {
  const [open, setOpen] = useState(false);
  const finalized = detail.sheet.creation.finalized;
  return (
    <>
      <Button variant="ghost" icon={<TrendingUp size={14} />} disabled={!finalized} title={finalized ? t('Gastar IP em perícias, Rank e Role', 'Spend IP on skills, Rank and Role') : notYet}
        onClick={() => setOpen(true)}>{t('Evoluir', 'Level up')}</Button>
      <Modal open={open} onClose={() => setOpen(false)} title={t('Evoluir', 'Level up')} wide>
        <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
          <ProgressionForm detail={detail} rules={rules} run={run} busy={busy} />
        </div>
      </Modal>
    </>
  );
}

function ProgressionForm({ detail, rules, run, busy }: { detail: CharacterDetail; rules: CharacterRules; run: Run; busy: boolean }) {
  const [skill, setSkill] = useState('');
  const [spec, setSpec] = useState('');
  const [newRole, setNewRole] = useState('');
  const ip = detail.sheet.progress.ip;
  const report = detail.report;
  const current = report.roles.find((r) => r.current);
  const groups = useMemo(() => improvementGroups(rules.skills, report.skills), [rules.skills, report.skills]);
  const picked = groups.flatMap((g) => g.options).find((o) => o.value === skill);
  const choices = picked?.isNew
    ? specializationChoices(picked.skillId, report.skills.map((s) => s.key), specializationSources(rules))
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
