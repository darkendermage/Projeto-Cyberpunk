import { useState } from 'react';
import { Crosshair } from 'lucide-react';
import type { AttackMode, AttackResult, EncounterDetail, EncounterRules, FighterView } from '../../api/encounterTypes';
import { Badge, Button, Checkbox, Field, NumberInput, Select, Switch } from '../../components/ui/ui';
import { diceCount, isArea, MODE_LABEL, modesFor, needsRange, parseDice, THROWN } from './combat';
import type { Run } from './EncounterView';
import styles from './Combat.module.css';

/**
 * The attack form for the selected combatant: weapon (with STAT + skill already added), mode, targets, range and the
 * optional dice the players rolled at the table. Everything else is resolved by the server.
 */
export function AttackPanel({ detail, rules, attacker, busy, run }: {
  detail: EncounterDetail; rules: EncounterRules; attacker: FighterView | null; busy: boolean; run: Run;
}) {
  const [weaponKey, setWeaponKey] = useState('');
  const [mode, setMode] = useState<AttackMode>('Single');
  const [targets, setTargets] = useState<string[]>([]);
  const [range, setRange] = useState<number | null>(10);
  const [dodge, setDodge] = useState(false);
  const [modifier, setModifier] = useState<number | null>(0);
  const [attackRoll, setAttackRoll] = useState<number | null>(null);
  const [damageText, setDamageText] = useState('');
  const [thrownDamage, setThrownDamage] = useState('');
  const [ignoreCover, setIgnoreCover] = useState(false);

  if (!attacker) return null;
  const thrown = weaponKey === THROWN;
  const weapon = thrown ? null : attacker.weapons.find((w) => w.key === weaponKey) ?? attacker.weapons[0] ?? null;
  const modes = modesFor(weapon, thrown);
  const currentMode = modes.includes(mode) ? mode : 'Single';
  const area = isArea(currentMode);
  const others = detail.queue.filter((f) => f.id !== attacker.id);
  const chosen = area ? targets.filter((t) => others.some((o) => o.id === t)) : targets.slice(0, 1).filter((t) => others.some((o) => o.id === t));
  const target = chosen.length > 0 ? chosen : others.length > 0 && !area ? [others.find((o) => o.side !== attacker.side)?.id ?? others[0].id] : [];
  const expectedDice = currentMode === 'Autofire' ? 2 : currentMode === 'ShotgunShell' ? 3 : diceCount(thrown ? thrownDamage || '1d6' : weapon?.damage ?? '');
  const total = currentMode === 'Autofire' || currentMode === 'Suppressive' ? weapon?.autofireTotal : thrown ? attacker.athletics : weapon?.attackTotal;

  const submit = () => run('attack', {
    attackerId: attacker.id,
    targetIds: target,
    weapon: thrown ? THROWN : weapon?.key,
    mode: currentMode,
    range: needsRange(weapon, currentMode, thrown) ? range : null,
    dodge,
    modifier: modifier ?? 0,
    attackRoll,
    damageDice: parseDice(damageText),
    thrownDamage: thrown ? thrownDamage || null : null,
    ignoreCover,
  });

  return (
    <section className={styles.panel}>
      <div className={styles.rowBetween}>
        <h2 className={styles.panelTitle}>Ataque de {attacker.name}</h2>
        {attacker.dead && <Badge tone="red">Morto</Badge>}
      </div>
      <div className={styles.formGrid}>
        <Field label="Arma" htmlFor="atk-weapon" hint={weapon ? `Ataque ${total ?? '—'} + d10 · dano ${weapon.damage}${weapon.rounds != null ? ` · ${weapon.rounds}/${weapon.magazine} balas` : ''}` : thrown ? `DEX + Athletics ${attacker.athletics} + d10 (${rules.rules.thrown.source})` : undefined}>
          <Select id="atk-weapon" value={thrown ? THROWN : weapon?.key ?? ''} onChange={(e) => setWeaponKey(e.target.value)}>
            {attacker.weapons.map((w) => <option key={w.key} value={w.key}>{w.name} ({w.damage}{w.halvesArmor ? ', metade do SP' : ''})</option>)}
            <option value={THROWN}>Arremessar objeto ou granada</option>
          </Select>
        </Field>
        <Field label="Modo" htmlFor="atk-mode">
          <Select id="atk-mode" value={currentMode} onChange={(e) => setMode(e.target.value as AttackMode)}>
            {modes.map((m) => <option key={m} value={m}>{MODE_LABEL[m]}</option>)}
          </Select>
        </Field>
        {!area && (
          <Field label="Alvo" htmlFor="atk-target">
            <Select id="atk-target" value={target[0] ?? ''} onChange={(e) => setTargets([e.target.value])}>
              {others.map((o) => <option key={o.id} value={o.id}>{o.name}{o.side === attacker.side ? ' (aliado)' : ''}</option>)}
            </Select>
          </Field>
        )}
        {needsRange(weapon, currentMode, thrown) && (
          <Field label="Distância (m)" htmlFor="atk-range" hint={`Tabela de alcance: ${rules.rules.singleShot.source}`}>
            <NumberInput id="atk-range" min={0} max={800} allowEmpty value={range} onChange={setRange} />
          </Field>
        )}
        <Field label="Modificador" htmlFor="atk-mod" hint="Cobertura do atirador, luz, situação…">
          <NumberInput id="atk-mod" min={-20} max={20} value={modifier} onChange={setModifier} />
        </Field>
        <Field label="d10 do ataque" htmlFor="atk-roll" hint="Vazio: o app rola">
          <NumberInput id="atk-roll" min={1} max={10} allowEmpty value={attackRoll} onChange={setAttackRoll} placeholder="d10" />
        </Field>
        <Field label={`Dados de dano (${expectedDice})`} htmlFor="atk-dmg" hint="Ex.: 5 3 6. Vazio: o app rola.">
          <input id="atk-dmg" value={damageText} onChange={(e) => setDamageText(e.target.value)} placeholder="rolar" />
        </Field>
        {thrown && (
          <Field label="Dano do objeto" htmlFor="atk-thrown" hint="Granada: o dano do lançador (6d6). Faca: o da arma.">
            <input id="atk-thrown" value={thrownDamage} onChange={(e) => setThrownDamage(e.target.value)} placeholder="1d6" />
          </Field>
        )}
      </div>
      {area && (
        <div className={styles.row}>
          <span className={styles.label}>Alvos na área</span>
          {others.map((o) => (
            <Checkbox key={o.id} checked={targets.includes(o.id)} label={o.name}
              onChange={(on) => setTargets(on ? [...targets, o.id] : targets.filter((t) => t !== o.id))} />
          ))}
        </div>
      )}
      <div className={styles.row}>
        {!weapon?.melee && <Switch checked={dodge} onChange={setDodge} label={`Alvo esquiva (só REF ${rules.rules.dodgeRef.value}+)`} />}
        <Switch checked={ignoreCover} onChange={setIgnoreCover} label="Ignorar cobertura (tem linha de visão)" />
        <Button variant="primary" icon={<Crosshair size={15} />} disabled={busy || attacker.dead || target.length === 0 || !(weapon || thrown)} onClick={submit}>
          Atacar
        </Button>
      </div>
      {currentMode.startsWith('Aimed') && (
        <span className={styles.source}>{rules.rules.aimedEffects.value.find((e) => e.mode === currentMode)?.effect} ({rules.rules.aimedShot.source})</span>
      )}
      {currentMode === 'Explosive' && <span className={styles.source}>{rules.rules.explosives.value}</span>}
      {currentMode === 'Suppressive' && <span className={styles.source}>{rules.rules.suppressive.value.effect}</span>}
    </section>
  );
}

/** The last attack, step by step, like the examples of the book. */
export function AttackResultView({ result }: { result: AttackResult }) {
  return (
    <section className={styles.panel}>
      <h2 className={styles.panelTitle}>Resultado</h2>
      <p>
        <strong>{result.attacker}</strong> · {result.weapon} · {MODE_LABEL[result.mode]} · d10 {result.attackRoll} → <strong>{result.attackTotal}</strong>
      </p>
      <ul className={styles.results}>
        {result.targets.map((t) => (
          <li key={t.targetId} className={t.hit ? styles.hit : styles.miss}>
            <span className={styles.rowBetween}>
              <strong>{t.targetName}</strong>
              <Badge tone={t.hit ? 'red' : 'default'}>{result.mode === 'Suppressive' ? (t.hit ? 'Suprimido' : 'Segurou') : t.hit ? 'Acertou' : 'Errou'}</Badge>
            </span>
            <span className={styles.muted}>Defesa: {t.defense}</span>
            {t.hit && result.mode !== 'Suppressive' && (
              <span className={styles.muted}>
                Dano [{t.damageDice.join(', ')}] = {t.damage} no {t.location}
                {t.coverAbsorbed != null && ` · cobertura absorveu ${t.coverAbsorbed}`}
                {t.spBefore != null && ` · SP ${t.spBefore}`}
                {t.through != null && ` · ${t.through} no HP`}
                {t.woundState && ` · ${t.woundState}`}
              </span>
            )}
            {t.criticalInjury && <span className={styles.crit}>Critical Injury: {t.criticalInjury}</span>}
            {t.notes.map((n) => <span key={n} className={styles.source}>{n}</span>)}
          </li>
        ))}
      </ul>
      {result.notes.map((n) => <p key={n} className={styles.source}>{n}</p>)}
    </section>
  );
}
