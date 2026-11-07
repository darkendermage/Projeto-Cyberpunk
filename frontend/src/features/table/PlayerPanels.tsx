import { useState } from 'react';
import { Crosshair, Dices, HeartPulse, RotateCcw, RotateCw, SkipForward } from 'lucide-react';
import type { ArmorLocation, RollKind, TableVisibility, TokenView, PlayerCharacter, PlayerEncounter } from '../../api/tableTypes';
import { Badge, Button, IconButton, Segmented } from '../../components/ui/ui';
import { skillName, t } from '../../i18n';
import { WOUND_LABEL, WOUND_TONE } from '../combat/combat';
import { ArmorControl } from './ArmorControl';
import { NumField } from './NumField';
import { damageFormula } from './tokens';
import styles from './Table.module.css';

type Roll = (formula: string, kind: RollKind, label: string, visibility: TableVisibility, luck: number) => void;

const signed = (n: number) => (n >= 0 ? `+${n}` : `${n}`);

/**
 * The player's quick panel: their character's numbers, weapons and skills, each a click away from a server roll,
 * and their token's facing. In a fight the numbers come from the combat table; otherwise from the sheet.
 */
export function CharacterPanel({ character, encounter, token, onRoll, onTurn, onArmor, busy }: {
  character: PlayerCharacter | null;
  encounter: PlayerEncounter | null;
  token: TokenView | undefined;
  onRoll: Roll;
  onTurn: (delta: number) => void;
  /** The player's own armor up or down (ablation, repair). */
  onArmor?: (location: ArmorLocation, delta: number) => void;
  busy?: boolean;
}) {
  const [visibility, setVisibility] = useState<TableVisibility>('Public');
  const [luck, setLuck] = useState(0);
  const luckLeft = character?.luckAvailable ?? 0;
  const spend = Math.min(luck, luckLeft);
  // LUCK goes on Checks only (CP:R p.130) and is spent by the roll; Damage and Death Saves take none.
  const check = (formula: string, label: string) => { onRoll(formula, 'Check', label, visibility, spend); setLuck(0); };
  const [filter, setFilter] = useState('');
  const fighter = encounter?.queue.find((f) => f.own);
  if (!character && !fighter) return <span className={styles.muted}>{t('O mestre não ligou um personagem a este convite.', 'The GM didn\'t link a character to this invite.')}</span>;

  const hp = fighter?.hp ?? character?.hp ?? 0;
  // In a fight the combat table's numbers; otherwise the sheet's.
  const armor = fighter?.spBody != null
    ? { spHead: fighter.spHead ?? null, maxSpHead: fighter.maxSpHead ?? null, spBody: fighter.spBody, maxSpBody: fighter.maxSpBody ?? null }
    : character ? character.armor : null;
  const weapons = fighter?.weapons ?? character?.weapons ?? [];
  const maxHp = fighter?.maxHp ?? character?.maxHp ?? 0;
  const skills = (character?.skills ?? [])
    .filter((s) => !filter || skillName(s.name, s.namePt).toLowerCase().includes(filter.toLowerCase()))
    .sort((a, b) => skillName(a.name, a.namePt).localeCompare(skillName(b.name, b.namePt)));

  return (
    <div className={styles.stack}>
      <div className={styles.rowBetween}>
        <span className={styles.heroName}>{character?.handle ?? character?.name ?? fighter?.name}</span>
        {fighter && <Badge tone={WOUND_TONE[fighter.woundState]}>{fighter.dead ? t('Morto', 'Dead') : WOUND_LABEL[fighter.woundState]}</Badge>}
      </div>
      <div className={styles.stack} style={{ gap: 4 }}>
        <span className={styles.rowBetween}><span className={styles.label}>HP</span><span><span className={styles.statBig}>{hp}</span><span className={styles.stat}> / {maxHp}</span></span></span>
        <div className={styles.meter}><div className={styles.meterFill} style={{ width: `${maxHp > 0 ? Math.max(0, Math.min(100, (hp / maxHp) * 100)) : 0}%` }} /></div>
      </div>
      <div className={styles.row}>
        {character && <span><span className={styles.label}>{t('Sorte', 'Luck')} </span><span className={styles.stat}>{character.luckAvailable}/{character.luck}</span></span>}
      </div>
      <div className={styles.row}>
        {armor && <ArmorControl armor={armor} busy={busy} onAdjust={character ? onArmor : undefined} />}
      </div>
      {fighter && (fighter.conditions.length > 0 || fighter.injuries.length > 0) && (
        <div className={styles.row}>
          {fighter.conditions.map((c) => <Badge key={c} tone="amber">{c.split(':')[0]}</Badge>)}
          {fighter.injuries.map((i) => <Badge key={i} tone="red">{i}</Badge>)}
        </div>
      )}

      {character && (
        <div className={styles.row}>
          <span className={styles.label}>{t('Sorte no próximo teste', 'LUCK on the next Check')}</span>
          <NumField label={t('Pontos de Sorte a gastar', 'LUCK points to spend')} min={0} max={luckLeft} value={spend} onChange={(n) => setLuck(Math.round(n))} />
          <span className={styles.muted}>{t('+1 por ponto (CP:R p.130)', '+1 a point (CP:R p.130)')}</span>
        </div>
      )}
      <Segmented size="sm" ariaLabel={t('Quem vê as rolagens', 'Who sees the rolls')} value={visibility} onChange={setVisibility}
        options={[{ value: 'Public', label: t('Pública', 'Public') }, { value: 'Private', label: t('Privada', 'Private') }, { value: 'Secret', label: t('Secreta', 'Secret') }]} />

      {fighter?.mustDeathSave && (
        <Button variant="danger" icon={<HeartPulse size={15} />}
          onClick={() => onRoll('1d10', 'Plain', t(`Death Save (precisa de menos que ${fighter.deathSaveTarget})`, `Death Save (needs under ${fighter.deathSaveTarget})`), visibility, 0)}>
          {t('Rolar Death Save', 'Roll Death Save')}
        </Button>
      )}

      {token && (
        <div className={styles.row}>
          <span className={styles.label}>{t('Token', 'Token')}</span>
          <IconButton label={t('Girar −45°', 'Turn −45°')} onClick={() => onTurn(-45)}><RotateCcw size={14} /></IconButton>
          <IconButton label={t('Girar +45°', 'Turn +45°')} onClick={() => onTurn(45)}><RotateCw size={14} /></IconButton>
        </div>
      )}

      {weapons.length > 0 && (
        <div className={styles.section}>
          <span className={styles.label}>{t('Armas', 'Weapons')}</span>
          {weapons.map((w) => {
            const damage = damageFormula(w.damage ?? '');
            return (
              <div key={w.key} className={styles.weapon}>
                <div className={styles.rowBetween}>
                  <strong>{w.name}</strong>
                  {w.magazine != null && <span className={styles.stat}>{w.rounds ?? 0}/{w.magazine}</span>}
                </div>
                <div className={styles.row}>
                  <Button icon={<Crosshair size={14} />} onClick={() => check(`1d10${signed(w.attackTotal)}`, t(`Ataque: ${w.name}`, `Attack: ${w.name}`))}>
                    {t('Ataque', 'Attack')} {signed(w.attackTotal)}
                  </Button>
                  {w.autofireTotal != null && (
                    <Button variant="ghost" onClick={() => check(`1d10${signed(w.autofireTotal!)}`, t(`Autofire: ${w.name}`, `Autofire: ${w.name}`))}>
                      Autofire {signed(w.autofireTotal)}
                    </Button>
                  )}
                  {damage && (
                    <Button variant="ghost" icon={<Dices size={14} />} onClick={() => onRoll(damage, 'Damage', t(`Dano: ${w.name}`, `Damage: ${w.name}`), visibility, 0)}>
                      {t('Dano', 'Damage')} {w.damage}
                    </Button>
                  )}
                </div>
              </div>
            );
          })}
          <span className={styles.muted}>{t('Defesa, alcance e a munição gasta são resolvidos pelo mestre na mesa de combate.', 'Defense, range and spent ammo are resolved by the GM on the combat table.')}</span>
        </div>
      )}

      {character && character.skills.length > 0 && (
        <div className={styles.section}>
          <div className={styles.rowBetween}>
            <span className={styles.label}>{t('Perícias (1d10 + total)', 'Skills (1d10 + total)')}</span>
          </div>
          <input aria-label={t('Filtrar perícias', 'Filter skills')} value={filter} placeholder={t('Filtrar…', 'Filter…')} onChange={(e) => setFilter(e.target.value)} />
          <div className={styles.skillList}>
            {skills.map((s) => {
              const name = skillName(s.name, s.namePt) + (s.specialization ? ` (${s.specialization})` : '');
              return (
                <button key={s.key} type="button" className={styles.skillButton} onClick={() => check(`1d10${signed(s.total)}`, name)}>
                  <span>{name}</span>
                  <span className={styles.skillTotal}>{signed(s.total)}</span>
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}

/** The Initiative order of the fight on screen, with the fighter whose turn it is on top of the attention. */
export function InitiativePanel({ encounter, onEndTurn, busy }: { encounter: PlayerEncounter | null; onEndTurn?: () => void; busy?: boolean }) {
  if (!encounter) return <span className={styles.muted}>{t('Nenhum combate na cena agora.', 'No fight on the scene right now.')}</span>;
  const active = encounter.queue.find((f) => f.id === encounter.activeId);
  return (
    <div className={styles.stack}>
      <div className={styles.rowBetween}>
        <span className={styles.title}>{encounter.name}</span>
        <Badge tone="red">{t(`Rodada ${encounter.round}`, `Round ${encounter.round}`)}</Badge>
      </div>
      {active?.own && (
        <div className={styles.row}>
          <Badge tone="amber">{t('Sua vez! 1 movimento + 1 ação (CP:R p.168)', 'Your turn! 1 move + 1 action (CP:R p.168)')}</Badge>
          {onEndTurn && <Button icon={<SkipForward size={14} />} disabled={busy} onClick={onEndTurn}>{t('Passar o turno', 'End turn')}</Button>}
        </div>
      )}
      <div className={styles.queue}>
        {encounter.queue.map((f) => (
          <div key={f.id} className={`${styles.queueRow} ${f.side === 'Ally' ? styles.queueAlly : styles.queueEnemy} ${f.id === encounter.activeId ? styles.queueActive : ''}`}>
            <span className={styles.queueInit}>{f.initiative ?? '–'}</span>
            <span className={styles.queueName}>{f.name}{f.own ? ` · ${t('você', 'you')}` : ''}</span>
            <Badge tone={WOUND_TONE[f.woundState]}>{f.dead ? t('Morto', 'Dead') : WOUND_LABEL[f.woundState]}</Badge>
          </div>
        ))}
      </div>
    </div>
  );
}
