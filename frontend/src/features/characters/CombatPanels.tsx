import { Fragment, useState } from 'react';
import {
  Activity, Bandage, Crosshair, Dices, HeartPulse, RotateCcw, Shield, ShieldMinus, Skull, Swords, Wrench, X,
} from 'lucide-react';
import type { CharacterDetail, CharacterRules, InjuryLocation, InjuryStatus } from '../../api/characterTypes';
import { Button, IconButton, Select, Switch } from '../../components/ui/ui';
import { INJURY_STATUS_LABEL, signed, WOUND_LABEL } from './characterLabels';
import { SheetPanel, StateTag } from './CharacterUi';
import styles from './Characters.module.css';

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
    <SheetPanel title="Pontos de vida" icon={<HeartPulse size={16} />}>
      <div className={styles.bigNumbers}>
        <strong>{h.currentHp}</strong><span className={styles.muted}>/ {h.maxHp}</span>
        <span className={styles.grow} />
        <StateTag tone={woundTone(h.woundState)}>{WOUND_LABEL[h.woundState]}</StateTag>
      </div>
      <div className={styles.meter} role="meter" aria-valuemin={0} aria-valuemax={h.maxHp} aria-valuenow={h.currentHp} aria-label="Pontos de vida">
        <div className={styles.meterFill} style={{ width: `${pct}%` }} />
        <div className={styles.meterMark} style={{ left: `${mark}%` }} title={`Seriously Wounded abaixo de ${h.seriouslyWoundedThreshold}`} />
      </div>
      <dl className={styles.kv}>
        <dt>Seriously Wounded abaixo de</dt><dd>{h.seriouslyWoundedThreshold}</dd>
        <dt>Penalidade do ferimento</dt><dd>{h.woundActionPenalty ? signed(h.woundActionPenalty) : '—'}{h.woundMovePenalty ? ` · MOVE ${h.woundMovePenalty}` : ''}</dd>
        <dt>Death Save (abaixo de)</dt><dd title={h.bodySource ? `BODY de ${h.bodySource}` : 'BODY'}>{h.deathSave}</dd>
        <dt>Penalidade de Death Save</dt><dd>{h.deathSavePenalty}{h.baseDeathSavePenalty ? ` (base ${h.baseDeathSavePenalty})` : ''}</dd>
        {h.stabilizationDv && <><dt>Estabilizar</dt><dd>DV{h.stabilizationDv}</dd></>}
      </dl>
      {conditions.length > 0 && (
        <div className={styles.row}>
          {conditions.map((c) => <StateTag key={c} tone={c === 'Morto' ? 'bad' : 'warn'}>{c}</StateTag>)}
          <IconButton label="Limpar condições" disabled={busy || locked} onClick={() => run('combat/conditions/clear')}><X size={14} /></IconButton>
        </div>
      )}

      {locked ? <p className={`${styles.small} ${styles.muted}`}>Conclua a criação para usar o painel de combate.</p> : (
        <>
          <div className={styles.row}>
            <input className={`${styles.input} ${styles.inputSmall}`} type="number" min={0} value={amount} placeholder="dano"
              aria-label="Quantidade" onChange={(e) => setAmount(e.target.value)} />
            <Select value={location} onChange={(e) => setLocation(e.target.value as InjuryLocation)} aria-label="Local">
              <option value="Body">Corpo</option>
              <option value="Head">Cabeça (Aimed Shot)</option>
            </Select>
            <Switch checked={bypass} onChange={setBypass} label="Ignora armadura" />
          </div>
          <div className={styles.row}>
            <Button icon={<Swords size={14} />} disabled={busy || !(value > 0)}
              title="Dano menos SP do local; o que passar vai ao HP e abla a armadura (CP:R p.186)"
              onClick={async () => { if (await run('combat/damage', { amount: value, location, bypassArmor: bypass })) setAmount(''); }}>
              Aplicar dano
            </Button>
            <Button icon={<Bandage size={14} />} disabled={busy || !(value > 0)}
              onClick={async () => { if (await run('combat/heal', { amount: value })) setAmount(''); }}>Curar</Button>
            <Button icon={<Activity size={14} />} disabled={busy || h.woundState === 'Unhurt'} onClick={() => run('combat/stabilize', {}, 'Estabilizado.')}>
              Estabilizar
            </Button>
          </div>
          {h.mustRollDeathSave && (
            <div className={styles.row}>
              <Skull size={16} className={styles.red} aria-hidden />
              <span className={styles.small}>Death Save no início do turno: d10 + {h.deathSavePenalty} abaixo de {h.deathSave}.</span>
              <input className={`${styles.input} ${styles.inputSmall}`} type="number" min={1} max={10} value={roll} placeholder="d10"
                aria-label="Resultado do d10 (vazio para rolar)" onChange={(e) => setRoll(e.target.value)} />
              <Button icon={<Dices size={14} />} disabled={busy} onClick={async () => {
                if (await run('combat/death-save', { roll: roll ? Number(roll) : null })) setRoll('');
              }}>{roll ? 'Registrar' : 'Rolar'} Death Save</Button>
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
      <td className={styles.small}>{loc.name ?? '—'}{loc.fromCyberware && <span className={styles.tag}>implante</span>}</td>
      <td className={styles.total} title="SP atual / original">{loc.sp}{loc.maxSp != null && loc.sp !== loc.maxSp ? <span className={styles.muted}>/{loc.maxSp}</span> : ''}</td>
      <td>
        <span className={styles.row} style={{ flexWrap: 'nowrap' }}>
          <IconButton label={`Ablar 1 SP (${label})`} disabled={busy || loc.sp === 0} onClick={() => run('armor/ablate', { location: slot, amount: 1 })}>
            <ShieldMinus size={14} />
          </IconButton>
          {loc.instanceId && loc.maxSp != null && loc.sp < loc.maxSp && (
            <IconButton label={`Reparar ${loc.name}`} disabled={busy} onClick={() => run('armor/repair', { instanceId: loc.instanceId })}><Wrench size={14} /></IconButton>
          )}
        </span>
      </td>
    </tr>
  );
  return (
    <SheetPanel title="Armadura" icon={<Shield size={16} />}>
      <table className={styles.table}>
        <thead><tr><th>Local</th><th>Peça</th><th className={styles.num}>SP</th><th /></tr></thead>
        <tbody>
          {row('Cabeça', 'Head')}
          {row('Corpo', 'Body')}
          {shields.map((s) => (
            <tr key={s.instanceId}><td>Escudo</td><td className={styles.small}>{s.name}</td><td className={styles.total}>{s.shieldHp}/{s.shieldHpMax} HP</td><td /></tr>
          ))}
        </tbody>
      </table>
      <p className={styles.small}>
        Penalidade: {a.penalty.ref || a.penalty.dex || a.penalty.move
          ? <span className={styles.amber}>-{a.penalty.ref} REF, -{a.penalty.dex} DEX, -{a.penalty.move} MOVE ({a.penaltySource}), aplicada uma vez</span>
          : 'nenhuma'}
      </p>
    </SheetPanel>
  );
}

/** Equipped and carried weapons: Skill Base, ammunition, fire modes and reload. */
export function WeaponsPanel({ detail, run, busy, locked }: Props) {
  const weapons = detail.report.weapons;
  return (
    <SheetPanel title="Armas" icon={<Crosshair size={16} />}>
      {weapons.length === 0 ? <p className={styles.muted}>Nenhuma arma.</p> : (
        <table className={styles.table}>
          <thead><tr><th>Arma</th><th className={styles.num}>Base</th><th className={styles.num}>Dano</th><th className={styles.num}>ROF</th><th className={styles.num}>Munição</th></tr></thead>
          <tbody>
            {weapons.map((w) => {
              const ranged = w.calibers.length > 0;
              return (
                <Fragment key={w.instanceId}>
                <tr className={w.equipped ? undefined : styles.dim}>
                  <td>
                    <div>{w.name}</div>
                    <div className={styles.source}>
                      {w.skillName}{w.autofireMultiplier ? ` · Autofire ${w.autofireMultiplier} (base ${w.autofireTotal})` : ''}
                      {w.attachmentSlots > 0 ? ` · anexos ${w.attachments.length}/${w.attachmentSlots}` : ''}
                    </div>
                  </td>
                  <td className={styles.total}>{w.skillTotal}</td>
                  <td className={styles.num}>{w.damage}</td>
                  <td className={styles.num}>{w.rof}</td>
                  <td className={styles.num} title={w.loadedAmmo ?? undefined}>{ranged ? `${w.loadedRounds}/${w.magazine ?? '—'}` : '—'}</td>
                </tr>
                {!locked && (
                  <tr>
                    <td colSpan={5}>
                      {ranged && <div className={styles.row}>
                        <Button disabled={busy || w.loadedRounds < 1} onClick={() => run('weapons/fire', { instanceId: w.instanceId, mode: 'Single' })}>Disparar</Button>
                        {w.autofireMultiplier && (
                          <>
                            <Button disabled={busy || w.loadedRounds < 10} title="10 balas (CP:R p.173)"
                              onClick={() => run('weapons/fire', { instanceId: w.instanceId, mode: 'Autofire' })}>Autofire</Button>
                            <Button disabled={busy || w.loadedRounds < 10} title="10 balas (CP:R p.174)"
                              onClick={() => run('weapons/fire', { instanceId: w.instanceId, mode: 'Suppressive' })}>Supressão</Button>
                          </>
                        )}
                        <IconButton label={`Recarregar ${w.name}`} disabled={busy || (w.magazine != null && w.loadedRounds >= w.magazine)}
                          onClick={() => run('weapons/reload', { instanceId: w.instanceId })}><RotateCcw size={14} /></IconButton>
                        {w.loadedAmmo && <span className={styles.source}>{w.loadedAmmo}</span>}
                      </div>}
                      <Attachments detail={detail} weaponId={w.instanceId} slots={w.attachmentSlots} run={run} busy={busy} />
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
function Attachments({ detail, weaponId, slots, run, busy }: { detail: CharacterDetail; weaponId: string; slots: number; run: Run; busy: boolean }) {
  const [pick, setPick] = useState('');
  const entry = detail.sheet.inventory.find((e) => e.instanceId === weaponId);
  const installed = entry?.weapon?.attachments ?? [];
  const taken = new Set(detail.sheet.inventory.flatMap((e) => e.weapon?.attachments ?? []));
  const lines = new Map(detail.report.inventory.map((i) => [i.instanceId, i]));
  const free = detail.report.inventory.filter((i) => i.category === 'WeaponAttachment' && (!taken.has(i.instanceId) || i.quantity > 1));
  if (installed.length === 0 && free.length === 0) return null;
  return (
    <div className={styles.row} style={{ marginTop: 4 }}>
      <span className={styles.source}>Anexos{slots > 0 ? ` (${slots} slots)` : ''}:</span>
      {installed.map((id) => (
        <span key={id} className={styles.tag} style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
          {lines.get(id)?.name ?? id}
          <IconButton label={`Retirar ${lines.get(id)?.name}`} disabled={busy}
            onClick={() => run('weapons/detach', { weaponInstanceId: weaponId, attachmentInstanceId: id })}><X size={12} /></IconButton>
        </span>
      ))}
      {free.length > 0 && (
        <>
          <Select value={pick} onChange={(e) => setPick(e.target.value)} aria-label="Acessório a instalar">
            <option value="">Instalar acessório…</option>
            {free.map((i) => <option key={i.instanceId} value={i.instanceId}>{i.name}{i.quantity > 1 ? ` (x${i.quantity})` : ''}</option>)}
          </Select>
          <Button disabled={busy || !pick} onClick={async () => {
            if (await run('weapons/attach', { weaponInstanceId: weaponId, attachmentInstanceId: pick })) setPick('');
          }}>Instalar</Button>
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
      actions={<Switch checked={showHealed} onChange={setShowHealed} label="Histórico" />}>
      {injuries.length === 0 ? <p className={`${styles.small} ${styles.muted}`}>Nenhuma lesão {showHealed ? 'registrada' : 'ativa'}.</p> : (
        <ul className={styles.list}>
          {injuries.map((i) => {
            const d = def(i.name, i.location);
            return (
              <li key={i.id} className={styles.listItem} data-tone={i.status === 'Active' ? 'bad' : i.status === 'QuickFixed' ? 'warn' : 'ok'}>
                <div className={styles.grow}>
                  <div><strong>{i.name}</strong> <span className={styles.muted}>({i.location === 'Head' ? 'cabeça' : 'corpo'}{i.detail ? `, ${i.detail}` : ''})</span></div>
                  <div className={styles.small}>{d?.effect}{d?.deathSave && ' Base Death Save Penalty +1.'}</div>
                  <div className={styles.source}>
                    {INJURY_STATUS_LABEL[i.status]} · Quick Fix: {d?.quickFix ?? 'N/A'} · Treatment: {d?.treatment}
                  </div>
                </div>
                {!locked && i.status !== 'Healed' && (
                  <div className={styles.column} style={{ gap: 4 }}>
                    {i.status === 'Active' && d?.quickFix && <Button disabled={busy} onClick={() => setStatus(i.id, 'QuickFixed')}>Quick Fix</Button>}
                    {i.status === 'QuickFixed' && <Button disabled={busy} title="O efeito voltou (fim do dia ou lesão sofrida de novo)" onClick={() => setStatus(i.id, 'Active')}>Reativar</Button>}
                    <Button disabled={busy} onClick={() => setStatus(i.id, 'Healed')}>Tratada</Button>
                    <IconButton label="Remover lançamento" disabled={busy} onClick={() => run(`injuries/${i.id}/remove`)}><X size={14} /></IconButton>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}
      {!locked && (
        <div className={styles.row}>
          <Select value={location} onChange={(e) => { setLocation(e.target.value as InjuryLocation); setName(''); }} aria-label="Tabela">
            <option value="Body">Corpo</option>
            <option value="Head">Cabeça</option>
          </Select>
          <Select value={name} onChange={(e) => setName(e.target.value)} aria-label="Lesão">
            <option value="">Lesão (2d6)…</option>
            {table.map((i) => <option key={i.name} value={i.name}>{i.roll} · {i.name}</option>)}
          </Select>
          <Switch checked={bonus} onChange={setBonus} label="+5 dano bônus" />
          <Button disabled={busy || !name} onClick={async () => {
            if (await run('injuries', { location, name, applyBonusDamage: bonus })) setName('');
          }}>Registrar</Button>
        </div>
      )}
    </SheetPanel>
  );
}
