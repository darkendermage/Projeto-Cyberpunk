import { Fragment, useState } from 'react';
import {
  Activity, Bandage, Crosshair, Dices, HeartPulse, RotateCcw, Shield, ShieldMinus, Skull, Swords, Wrench, X,
} from 'lucide-react';
import type { CharacterDetail, CharacterRules, InjuryLocation, InjuryStatus } from '../../api/characterTypes';
import { Button, IconButton, Select, Switch } from '../../components/ui/ui';
import { INJURY_STATUS_LABEL, signed, WOUND_LABEL } from './characterLabels';
import { SheetPanel, StateTag } from './CharacterUi';
import styles from './Characters.module.css';
import { t, tv } from '../../i18n';

export type Run = (path: string, body?: Record<string, unknown>, success?: string) => Promise<CharacterDetail | null>;

interface Props { detail: CharacterDetail; rules: CharacterRules; run: Run; busy: boolean; locked: boolean }

const woundTone = (w: string) => (w === 'Mortally' ? 'bad' : w === 'Seriously' ? 'warn' : 'ok') as 'ok' | 'warn' | 'bad';

/** HP, wound state, Death Save and the quick combat actions. Nothing here changes without an explicit action. */
export function HealthPanel({ detail, run, busy, locked }: Props) {
  const h = detail.report.health;
  const [amount, setAmount] = useState('');
  const [location, setLocation] = useState<InjuryLocation>('Body');
  const [bypass, setBypass] = useState(false);
  const [roll, setRoll] = useState('');
  const value = Number(amount);
  const pct = Math.max(0, Math.min(100, (h.currentHp / h.maxHp) * 100));
  const mark = (h.seriouslyWoundedThreshold / h.maxHp) * 100;
  const conditions = detail.sheet.combat.conditions;

  return (
    <SheetPanel title={t('Pontos de vida', 'Hit Points')} icon={<HeartPulse size={16} />}>
      <div className={styles.bigNumbers}>
        <strong>{h.currentHp}</strong><span className={styles.muted}>/ {h.maxHp}</span>
        <span className={styles.grow} />
        <StateTag tone={woundTone(h.woundState)}>{WOUND_LABEL[h.woundState]}</StateTag>
      </div>
      <div className={styles.meter} role="meter" aria-valuemin={0} aria-valuemax={h.maxHp} aria-valuenow={h.currentHp} aria-label={t('Pontos de vida', 'Hit Points')}>
        <div className={styles.meterFill} style={{ width: `${pct}%` }} />
        <div className={styles.meterMark} style={{ left: `${mark}%` }} title={t(`Seriously Wounded abaixo de ${h.seriouslyWoundedThreshold}`, `Seriously Wounded below ${h.seriouslyWoundedThreshold}`)} />
      </div>
      <dl className={styles.kv}>
        <dt>{t('Seriously Wounded abaixo de', 'Seriously Wounded below')}</dt><dd>{h.seriouslyWoundedThreshold}</dd>
        <dt>{t('Penalidade do ferimento', 'Wound penalty')}</dt><dd>{h.woundActionPenalty ? signed(h.woundActionPenalty) : '—'}{h.woundMovePenalty ? ` · MOVE ${h.woundMovePenalty}` : ''}</dd>
        <dt>{t('Death Save (abaixo de)', 'Death Save (below)')}</dt><dd title={h.bodySource ? t(`BODY de ${h.bodySource}`, `BODY from ${h.bodySource}`) : 'BODY'}>{h.deathSave}</dd>
        <dt>{t('Penalidade de Death Save', 'Death Save penalty')}</dt><dd>{h.deathSavePenalty}{h.baseDeathSavePenalty ? ` (base ${h.baseDeathSavePenalty})` : ''}</dd>
        {h.stabilizationDv && <><dt>{t('Estabilizar', 'Stabilize')}</dt><dd>DV{h.stabilizationDv}</dd></>}
      </dl>
      {conditions.length > 0 && (
        <div className={styles.row}>
          {conditions.map((c) => <StateTag key={c} tone={c === 'Morto' ? 'bad' : 'warn'}>{c}</StateTag>)}
          <IconButton label={t('Limpar condições', 'Clear conditions')} disabled={busy || locked} onClick={() => run('combat/conditions/clear')}><X size={14} /></IconButton>
        </div>
      )}

      {locked ? <p className={`${styles.small} ${styles.muted}`}>{t('Conclua a criação para usar o painel de combate.', 'Finish creation to use the combat panel.')}</p> : (
        <>
          <div className={styles.row}>
            <input className={`${styles.input} ${styles.inputSmall}`} type="number" min={0} value={amount} placeholder={t('dano', 'damage')}
              aria-label={t('Quantidade', 'Amount')} onChange={(e) => setAmount(e.target.value)} />
            <Select value={location} onChange={(e) => setLocation(e.target.value as InjuryLocation)} aria-label={t('Local', 'Location')}>
              <option value="Body">{t('Corpo', 'Body')}</option>
              <option value="Head">{t('Cabeça (Aimed Shot)', 'Head (Aimed Shot)')}</option>
            </Select>
            <Switch checked={bypass} onChange={setBypass} label={t('Ignora armadura', 'Ignores armor')} />
          </div>
          <div className={styles.row}>
            <Button icon={<Swords size={14} />} disabled={busy || !(value > 0)}
              title={t('Dano menos SP do local; o que passar vai ao HP e abla a armadura (CP:R p.186)', 'Damage minus the location\'s SP; what gets through goes to HP and ablates the armor (CP:R p.186)')}
              onClick={async () => { if (await run('combat/damage', { amount: value, location, bypassArmor: bypass })) setAmount(''); }}>
              {t('Aplicar dano', 'Apply damage')}
            </Button>
            <Button icon={<Bandage size={14} />} disabled={busy || !(value > 0)}
              onClick={async () => { if (await run('combat/heal', { amount: value })) setAmount(''); }}>{t('Curar', 'Heal')}</Button>
            <Button icon={<Activity size={14} />} disabled={busy || h.woundState === 'Unhurt'} onClick={() => run('combat/stabilize', {}, t('Estabilizado.', 'Stabilized.'))}>
              {t('Estabilizar', 'Stabilize')}
            </Button>
          </div>
          {h.mustRollDeathSave && (
            <div className={styles.row}>
              <Skull size={16} className={styles.red} aria-hidden />
              <span className={styles.small}>{t('Death Save no início do turno: d10 + ', 'Death Save at the start of the turn: d10 + ')}{h.deathSavePenalty} {t('abaixo de ', 'below ')}{h.deathSave}.</span>
              <input className={`${styles.input} ${styles.inputSmall}`} type="number" min={1} max={10} value={roll} placeholder="d10"
                aria-label={t('Resultado do d10 (vazio para rolar)', 'd10 result (empty to roll)')} onChange={(e) => setRoll(e.target.value)} />
              <Button icon={<Dices size={14} />} disabled={busy} onClick={async () => {
                if (await run('combat/death-save', { roll: roll ? Number(roll) : null })) setRoll('');
              }}>{roll ? t('Registrar', 'Record') : t('Rolar', 'Roll')} Death Save</Button>
            </div>
          )}
        </>
      )}
    </SheetPanel>
  );
}

/** Armor by location with current SP, manual ablation and repair. */
export function ArmorPanel({ detail, run, busy }: Props) {
  const a = detail.report.armor;
  const shields = detail.report.inventory.filter((i) => i.shieldHpMax != null);
  const row = (label: string, slot: 'Head' | 'Body', loc = slot === 'Head' ? a.head : a.body) => (
    <tr key={slot}>
      <td>{label}</td>
      <td className={styles.small}>{tv(loc.name) ?? '—'}{loc.fromCyberware && <span className={styles.tag}>{t('implante', 'implant')}</span>}</td>
      <td className={styles.total} title={t('SP atual / original', 'Current / original SP')}>{loc.sp}{loc.maxSp != null && loc.sp !== loc.maxSp ? <span className={styles.muted}>/{loc.maxSp}</span> : ''}</td>
      <td>
        <span className={styles.row} style={{ flexWrap: 'nowrap' }}>
          <IconButton label={t(`Ablar 1 SP (${label})`, `Ablate 1 SP (${label})`)} disabled={busy || loc.sp === 0} onClick={() => run('armor/ablate', { location: slot, amount: 1 })}>
            <ShieldMinus size={14} />
          </IconButton>
          {loc.instanceId && loc.maxSp != null && loc.sp < loc.maxSp && (
            <IconButton label={t(`Reparar ${tv(loc.name)}`, `Repair ${tv(loc.name)}`)} disabled={busy} onClick={() => run('armor/repair', { instanceId: loc.instanceId })}><Wrench size={14} /></IconButton>
          )}
        </span>
      </td>
    </tr>
  );
  return (
    <SheetPanel title={t('Armadura', 'Armor')} icon={<Shield size={16} />}>
      <table className={styles.table}>
        <thead><tr><th>{t('Local', 'Location')}</th><th>{t('Peça', 'Piece')}</th><th className={styles.num}>SP</th><th /></tr></thead>
        <tbody>
          {row(t('Cabeça', 'Head'), 'Head')}
          {row(t('Corpo', 'Body'), 'Body')}
          {shields.map((s) => (
            <tr key={s.instanceId}><td>{t('Escudo', 'Shield')}</td><td className={styles.small}>{tv(s.name)}</td><td className={styles.total}>{s.shieldHp}/{s.shieldHpMax} HP</td><td /></tr>
          ))}
        </tbody>
      </table>
      <p className={styles.small}>
        {t('Penalidade: ', 'Penalty: ')}{a.penalty.ref || a.penalty.dex || a.penalty.move
          ? <span className={styles.amber}>-{a.penalty.ref} {t('REF, -', 'REF, -')}{a.penalty.dex} DEX, -{a.penalty.move} MOVE ({a.penaltySource}{t('), aplicada uma vez', '), applied once')}</span>
          : t('nenhuma', 'none')}
      </p>
    </SheetPanel>
  );
}

/** Equipped and carried weapons: Skill Base, ammunition, fire modes and reload. */
export function WeaponsPanel({ detail, run, busy, locked }: Props) {
  const weapons = detail.report.weapons;
  return (
    <SheetPanel title={t('Armas', 'Weapons')} icon={<Crosshair size={16} />}>
      {weapons.length === 0 ? <p className={styles.muted}>{t('Nenhuma arma.', 'No weapons.')}</p> : (
        <table className={styles.table}>
          <thead><tr><th>{t('Arma', 'Weapon')}</th><th className={styles.num}>Base</th><th className={styles.num}>{t('Dano', 'Damage')}</th><th className={styles.num}>ROF</th><th className={styles.num}>{t('Munição', 'Ammo')}</th></tr></thead>
          <tbody>
            {weapons.map((w) => {
              const ranged = w.calibers.length > 0;
              return (
                <Fragment key={w.instanceId}>
                <tr className={w.equipped ? undefined : styles.dim}>
                  <td>
                    <div>{tv(w.name)}</div>
                    <div className={styles.source}>
                      {w.skillName}{w.autofireMultiplier ? ` · Autofire ${w.autofireMultiplier} (base ${w.autofireTotal})` : ''}
                      {w.attachmentSlots > 0 ? t(` · anexos ${w.attachments.length}/${w.attachmentSlots}`, ` · attachments ${w.attachments.length}/${w.attachmentSlots}`) : ''}
                    </div>
                  </td>
                  <td className={styles.total}>{w.skillTotal}</td>
                  <td className={styles.num}>{tv(w.damage)}</td>
                  <td className={styles.num}>{w.rof}</td>
                  <td className={styles.num} title={w.loadedAmmo ?? undefined}>{ranged ? `${w.loadedRounds}/${w.magazine ?? '—'}` : '—'}</td>
                </tr>
                {!locked && (
                  <tr>
                    <td colSpan={5}>
                      {ranged && <div className={styles.row}>
                        <Button disabled={busy || w.loadedRounds < 1} onClick={() => run('weapons/fire', { instanceId: w.instanceId, mode: 'Single' })}>{t('Disparar', 'Fire')}</Button>
                        {w.autofireMultiplier && (
                          <>
                            <Button disabled={busy || w.loadedRounds < 10} title={t('10 balas (CP:R p.173)', '10 rounds (CP:R p.173)')}
                              onClick={() => run('weapons/fire', { instanceId: w.instanceId, mode: 'Autofire' })}>Autofire</Button>
                            <Button disabled={busy || w.loadedRounds < 10} title={t('10 balas (CP:R p.174)', '10 rounds (CP:R p.174)')}
                              onClick={() => run('weapons/fire', { instanceId: w.instanceId, mode: 'Suppressive' })}>{t('Supressão', 'Suppression')}</Button>
                          </>
                        )}
                        <IconButton label={t(`Recarregar ${tv(w.name)}`, `Reload ${tv(w.name)}`)} disabled={busy || (w.magazine != null && w.loadedRounds >= w.magazine)}
                          onClick={() => run('weapons/reload', { instanceId: w.instanceId })}><RotateCcw size={14} /></IconButton>
                        {w.loadedAmmo && <span className={styles.source}>{w.loadedAmmo}</span>}
                      </div>}
                    </td>
                  </tr>
                )}
                </Fragment>
              );
            })}
          </tbody>
        </table>
      )}
    </SheetPanel>
  );
}

/**
 * Attachments of one weapon: the installed ones (removable) and the ones in the inventory that can go in. Class,
 * slots and Exotic restrictions are checked by the server (CP:R p.95).
 */
export function Attachments({ detail, weaponId, slots, run, busy }: { detail: CharacterDetail; weaponId: string; slots: number; run: Run; busy: boolean }) {
  const [pick, setPick] = useState('');
  const entry = detail.sheet.inventory.find((e) => e.instanceId === weaponId);
  const installed = entry?.weapon?.attachments ?? [];
  const taken = new Set(detail.sheet.inventory.flatMap((e) => e.weapon?.attachments ?? []));
  const lines = new Map(detail.report.inventory.map((i) => [i.instanceId, i]));
  const free = detail.report.inventory.filter((i) => i.category === 'WeaponAttachment' && (!taken.has(i.instanceId) || i.quantity > 1));
  if (installed.length === 0 && free.length === 0) return null;
  return (
    <div className={styles.row} style={{ marginTop: 4 }}>
      <span className={styles.source}>{t('Anexos', 'Attachments')}{slots > 0 ? ` (${slots} slots)` : ''}:</span>
      {installed.map((id) => (
        <span key={id} className={styles.tag} style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
          {tv(lines.get(id)?.name) ?? id}
          <IconButton label={t(`Retirar ${tv(lines.get(id)?.name)}`, `Take out ${tv(lines.get(id)?.name)}`)} disabled={busy}
            onClick={() => run('weapons/detach', { weaponInstanceId: weaponId, attachmentInstanceId: id })}><X size={12} /></IconButton>
        </span>
      ))}
      {free.length > 0 && (
        <>
          <Select value={pick} onChange={(e) => setPick(e.target.value)} aria-label={t('Acessório a instalar', 'Attachment to install')}>
            <option value="">{t('Instalar acessório…', 'Install attachment…')}</option>
            {free.map((i) => <option key={i.instanceId} value={i.instanceId}>{tv(i.name)}{i.quantity > 1 ? ` (x${i.quantity})` : ''}</option>)}
          </Select>
          <Button disabled={busy || !pick} onClick={async () => {
            if (await run('weapons/attach', { weaponInstanceId: weaponId, attachmentInstanceId: pick })) setPick('');
          }}>{t('Instalar', 'Install')}</Button>
        </>
      )}
    </div>
  );
}

/** Critical Injuries from the official tables, with Quick Fix and Treatment as the rules describe them. */
export function InjuriesPanel({ detail, rules, run, busy, locked }: Props) {
  const [location, setLocation] = useState<InjuryLocation>('Body');
  const [name, setName] = useState('');
  const [bonus, setBonus] = useState(true);
  const [showHealed, setShowHealed] = useState(false);
  const table = rules.criticalInjuries.filter((i) => i.location === location);
  const injuries = detail.sheet.injuries.filter((i) => showHealed || i.status !== 'Healed');
  const def = (n: string, l: InjuryLocation) => rules.criticalInjuries.find((i) => i.name === n && i.location === l);
  const setStatus = (id: string, status: InjuryStatus) => run(`injuries/${id}`, { status });

  return (
    <SheetPanel title="Critical Injuries" icon={<Skull size={16} />}
      actions={<Switch checked={showHealed} onChange={setShowHealed} label={t('Histórico', 'History')} />}>
      {injuries.length === 0 ? <p className={`${styles.small} ${styles.muted}`}>{t('Nenhuma lesão ', 'No injury ')}{showHealed ? 'registrada' : t('ativa', 'active')}.</p> : (
        <ul className={styles.list}>
          {injuries.map((i) => {
            const d = def(i.name, i.location);
            return (
              <li key={i.id} className={styles.listItem} data-tone={i.status === 'Active' ? 'bad' : i.status === 'QuickFixed' ? 'warn' : 'ok'}>
                <div className={styles.grow}>
                  <div><strong>{tv(i.name)}</strong> <span className={styles.muted}>({i.location === 'Head' ? t('cabeça', 'head') : t('corpo', 'body')}{i.detail ? `, ${tv(i.detail)}` : ''})</span></div>
                  <div className={styles.small}>{tv(d?.effect)}{d?.deathSave && ' Base Death Save Penalty +1.'}</div>
                  <div className={styles.source}>
                    {INJURY_STATUS_LABEL[i.status]} · Quick Fix: {d?.quickFix ?? 'N/A'} · Treatment: {d?.treatment}
                  </div>
                </div>
                {!locked && i.status !== 'Healed' && (
                  <div className={styles.column} style={{ gap: 4 }}>
                    {i.status === 'Active' && d?.quickFix && <Button disabled={busy} onClick={() => setStatus(i.id, 'QuickFixed')}>Quick Fix</Button>}
                    {i.status === 'QuickFixed' && <Button disabled={busy} title={t('O efeito voltou (fim do dia ou lesão sofrida de novo)', 'The effect came back (end of day or injury suffered again)')} onClick={() => setStatus(i.id, 'Active')}>{t('Reativar', 'Reactivate')}</Button>}
                    <Button disabled={busy} onClick={() => setStatus(i.id, 'Healed')}>{t('Tratada', 'Treated')}</Button>
                    <IconButton label={t('Remover lançamento', 'Remove entry')} disabled={busy} onClick={() => run(`injuries/${i.id}/remove`)}><X size={14} /></IconButton>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}
      {!locked && (
        <div className={styles.row}>
          <Select value={location} onChange={(e) => { setLocation(e.target.value as InjuryLocation); setName(''); }} aria-label={t('Tabela', 'Table')}>
            <option value="Body">{t('Corpo', 'Body')}</option>
            <option value="Head">{t('Cabeça', 'Head')}</option>
          </Select>
          <Select value={name} onChange={(e) => setName(e.target.value)} aria-label={t('Lesão', 'Injury')}>
            <option value="">{t('Lesão (2d6)…', 'Injury (2d6)…')}</option>
            {table.map((i) => <option key={i.name} value={i.name}>{i.roll} · {tv(i.name)}</option>)}
          </Select>
          <Switch checked={bonus} onChange={setBonus} label={t('+5 dano bônus', '+5 bonus damage')} />
          <Button disabled={busy || !name} onClick={async () => {
            if (await run('injuries', { location, name, applyBonusDamage: bonus })) setName('');
          }}>{t('Registrar', 'Record')}</Button>
        </div>
      )}
    </SheetPanel>
  );
}
