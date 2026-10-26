import { useState } from 'react';
import { Crosshair } from 'lucide-react';
import type { AttackMode, AttackResult, EncounterDetail, EncounterRules, FighterView } from '../../api/encounterTypes';
import { Badge, Button, Checkbox, Field, NumberInput, Select, Switch } from '../../components/ui/ui';
import { diceCount, isArea, MODE_LABEL, modesFor, needsRange, parseDice, THROWN } from './combat';
import type { Run } from './EncounterView';
import styles from './Combat.module.css';
import { t, tv } from '../../i18n';

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
  const chosen = area ? targets.filter((entry) => others.some((o) => o.id === entry)) : targets.slice(0, 1).filter((entry) => others.some((o) => o.id === entry));
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
        <h2 className={styles.panelTitle}>{t('Ataque de ', 'Attack by ')}{tv(attacker.name)}</h2>
        {attacker.dead && <Badge tone="red">{t('Morto', 'Dead')}</Badge>}
      </div>
      <div className={styles.formGrid}>
        <Field label={t('Arma', 'Weapon')} htmlFor="atk-weapon" hint={weapon ? t(`Ataque ${total ?? '—'} + d10 · dano ${tv(weapon.damage)}${weapon.rounds != null ? ` · ${weapon.rounds}/${weapon.magazine} balas` : ''}`, `Attack ${total ?? '—'} + d10 · damage ${tv(weapon.damage)}${weapon.rounds != null ? ` · ${weapon.rounds}/${weapon.magazine} rounds` : ''}`) : thrown ? `DEX + Athletics ${attacker.athletics} + d10 (${rules.rules.thrown.source})` : undefined}>
          <Select id="atk-weapon" value={thrown ? THROWN : weapon?.key ?? ''} onChange={(e) => setWeaponKey(e.target.value)}>
            {attacker.weapons.map((w) => <option key={w.key} value={w.key}>{tv(w.name)} ({tv(w.damage)}{w.halvesArmor ? t(', metade do SP', ', half SP') : ''})</option>)}
            <option value={THROWN}>{t('Arremessar objeto ou granada', 'Throw an object or grenade')}</option>
          </Select>
        </Field>
        <Field label={t('Modo', 'Mode')} htmlFor="atk-mode">
          <Select id="atk-mode" value={currentMode} onChange={(e) => setMode(e.target.value as AttackMode)}>
            {modes.map((m) => <option key={m} value={m}>{MODE_LABEL[m]}</option>)}
          </Select>
        </Field>
        {!area && (
          <Field label={t('Alvo', 'Target')} htmlFor="atk-target">
            <Select id="atk-target" value={target[0] ?? ''} onChange={(e) => setTargets([e.target.value])}>
              {others.map((o) => <option key={o.id} value={o.id}>{tv(o.name)}{o.side === attacker.side ? t(' (aliado)', ' (ally)') : ''}</option>)}
            </Select>
          </Field>
        )}
        {needsRange(weapon, currentMode, thrown) && (
          <Field label={t('Distância (m)', 'Distance (m)')} htmlFor="atk-range" hint={t(`Tabela de alcance: ${rules.rules.singleShot.source}`, `Range table: ${rules.rules.singleShot.source}`)}>
            <NumberInput id="atk-range" min={0} max={800} allowEmpty value={range} onChange={setRange} />
          </Field>
        )}
        <Field label={t('Modificador', 'Modifier')} htmlFor="atk-mod" hint={t('Cobertura do atirador, luz, situação…', 'Shooter\'s cover, lighting, situation…')}>
          <NumberInput id="atk-mod" min={-20} max={20} value={modifier} onChange={setModifier} />
        </Field>
        <Field label={t('d10 do ataque', 'Attack d10')} htmlFor="atk-roll" hint={t('Vazio: o app rola', 'Empty: the app rolls')}>
          <NumberInput id="atk-roll" min={1} max={10} allowEmpty value={attackRoll} onChange={setAttackRoll} placeholder="d10" />
        </Field>
        <Field label={t(`Dados de dano (${expectedDice})`, `Damage dice (${expectedDice})`)} htmlFor="atk-dmg" hint={t('Ex.: 5 3 6. Vazio: o app rola.', 'E.g. 5 3 6. Empty: the app rolls.')}>
          <input id="atk-dmg" value={damageText} onChange={(e) => setDamageText(e.target.value)} placeholder={t('rolar', 'roll')} />
        </Field>
        {thrown && (
          <Field label={t('Dano do objeto', 'Object damage')} htmlFor="atk-thrown" hint={t('Granada: o dano do lançador (6d6). Faca: o da arma.', 'Grenade: the launcher\'s damage (6d6). Knife: the weapon\'s.')}>
            <input id="atk-thrown" value={thrownDamage} onChange={(e) => setThrownDamage(e.target.value)} placeholder="1d6" />
          </Field>
        )}
      </div>
      {area && (
        <div className={styles.row}>
          <span className={styles.label}>{t('Alvos na área', 'Targets in the area')}</span>
          {others.map((o) => (
            <Checkbox key={o.id} checked={targets.includes(o.id)} label={tv(o.name)}
              onChange={(on) => setTargets(on ? [...targets, o.id] : targets.filter((entry) => entry !== o.id))} />
          ))}
        </div>
      )}
      <div className={styles.row}>
        {!weapon?.melee && <Switch checked={dodge} onChange={setDodge} label={t(`Alvo esquiva (só REF ${tv(rules.rules.dodgeRef.value)}+)`, `Target dodges (REF ${tv(rules.rules.dodgeRef.value)}+ only)`)} />}
        <Switch checked={ignoreCover} onChange={setIgnoreCover} label={t('Ignorar cobertura (tem linha de visão)', 'Ignore cover (has line of sight)')} />
        <Button variant="primary" icon={<Crosshair size={15} />} disabled={busy || attacker.dead || target.length === 0 || !(weapon || thrown)} onClick={submit}>
          {t('Atacar', 'Attack')}
        </Button>
      </div>
      {currentMode.startsWith('Aimed') && (
        <span className={styles.source}>{tv(rules.rules.aimedEffects.value.find((e) => e.mode === currentMode)?.effect)} ({rules.rules.aimedShot.source})</span>
      )}
      {currentMode === 'Explosive' && <span className={styles.source}>{tv(rules.rules.explosives.value)}</span>}
      {currentMode === 'Suppressive' && <span className={styles.source}>{tv(rules.rules.suppressive.value.effect)}</span>}
    </section>
  );
}

/** The last attack, step by step, like the examples of the book. */
export function AttackResultView({ result }: { result: AttackResult }) {
  return (
    <section className={styles.panel}>
      <h2 className={styles.panelTitle}>{t('Resultado', 'Result')}</h2>
      <p>
        <strong>{result.attacker}</strong> · {result.weapon} · {MODE_LABEL[result.mode]} · d10 {result.attackRoll} → <strong>{result.attackTotal}</strong>
      </p>
      <ul className={styles.results}>
        {result.targets.map((entry) => (
          <li key={entry.targetId} className={entry.hit ? styles.hit : styles.miss}>
            <span className={styles.rowBetween}>
              <strong>{entry.targetName}</strong>
              <Badge tone={entry.hit ? 'red' : 'default'}>{result.mode === 'Suppressive' ? (entry.hit ? t('Suprimido', 'Suppressed') : t('Segurou', 'Held')) : entry.hit ? t('Acertou', 'Hit') : t('Errou', 'Missed')}</Badge>
            </span>
            <span className={styles.muted}>{t('Defesa: ', 'Defense: ')}{entry.defense}</span>
            {entry.hit && result.mode !== 'Suppressive' && (
              <span className={styles.muted}>
                {t('Dano [', 'Damage [')}{entry.damageDice.join(', ')}] = {tv(entry.damage)} {t('no ', 'to the ')}{tv(entry.location)}
                {entry.coverAbsorbed != null && t(` · cobertura absorveu ${entry.coverAbsorbed}`, ` · cover absorbed ${entry.coverAbsorbed}`)}
                {entry.spBefore != null && ` · SP ${entry.spBefore}`}
                {entry.through != null && t(` · ${entry.through} no HP`, ` · ${entry.through} to HP`)}
                {entry.woundState && ` · ${entry.woundState}`}
              </span>
            )}
            {entry.criticalInjury && <span className={styles.crit}>Critical Injury: {entry.criticalInjury}</span>}
            {entry.notes.map((n) => <span key={n} className={styles.source}>{n}</span>)}
          </li>
        ))}
      </ul>
      {result.notes.map((n) => <p key={n} className={styles.source}>{n}</p>)}
    </section>
  );
}
