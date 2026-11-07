import { Crosshair, RefreshCw, X } from 'lucide-react';
import type { AttackMode, WeaponView } from '../../api/encounterTypes';
import { Button, Select } from '../../components/ui/ui';
import { t, tv } from '../../i18n';
import { MODE_LABEL, WOUND_LABEL } from '../combat/combat';
import { cannotFire, canReload, quickModes, weaponLabel, type Aim } from './aim';
import { ArmorControl } from './ArmorControl';
import type { StripEntry } from './strip';
import { initials } from './tokens';
import styles from './Table.module.css';

/** The weapons a card offers to whoever looks at it, and why they can't fire now (not their Turn). */
export interface CardWeapons { weapons: WeaponView[]; blocked: string | null }

/**
 * "On the scene": a card per character and fighter under the map — picture, name, HP and the SP of head and body
 * (NPCs show only their Wound State to the players). A click selects the token, or picks the target while aiming. The
 * cards of whoever may shoot (the player's own character, any fighter for the GM) list their weapons: a click aims.
 */
export function SceneStrip({ entries, selected, onPick, weaponsOf, aim, onWeapon, onReload, onUnjam }: {
  entries: StripEntry[];
  selected: readonly string[];
  onPick: (entry: StripEntry) => void;
  weaponsOf?: (entry: StripEntry) => CardWeapons | null;
  aim?: Aim | null;
  onWeapon?: (entry: StripEntry, weapon: WeaponView) => void;
  /** Reloads a weapon with a magazine (an Action in a fight). */
  onReload?: (entry: StripEntry, weapon: WeaponView) => void;
  /** Clears a jammed weapon (an Action). */
  onUnjam?: (entry: StripEntry, weapon: WeaponView) => void;
}) {
  if (entries.length === 0) return null;
  return (
    <div className={styles.strip} role="list" aria-label={t('Na cena', 'On the scene')}>
      {entries.map((e) => {
        const ratio = e.hp != null && e.maxHp ? Math.max(0, Math.min(1, e.hp / e.maxHp)) : null;
        const armed = e.offScene ? null : weaponsOf?.(e) ?? null;
        const shooter = aim != null && e.combatantId === aim.attackerId;
        const targeted = aim != null && e.combatantId != null && e.combatantId === aim.targetId;
        return (
          <div key={e.tokenId} role="listitem" style={{ borderColor: e.color }}
            className={`${styles.stripCard} ${selected.includes(e.tokenId) ? styles.stripCardOn : ''} ${targeted ? styles.stripTarget : ''} ${shooter ? styles.stripShooter : ''} ${e.offScene ? styles.stripCardOff : ''}`}>
            <button type="button" className={styles.stripPick} onClick={() => { if (!e.offScene) onPick(e); }} aria-disabled={e.offScene || undefined}
              title={e.offScene ? t('Fora da cena: sem token neste mapa', 'Off scene: no token on this map') : undefined}
              aria-label={e.offScene ? t(`${e.name} (fora da cena)`, `${e.name} (off scene)`) : aim && !shooter ? t(`Mirar em ${e.name}`, `Aim at ${e.name}`) : e.name}>
              {e.imageUrl
                ? <img className={styles.stripAvatar} src={e.imageUrl} alt="" />
                : <span className={styles.stripAvatar} style={{ background: e.color }}>{initials(e.name)}</span>}
              <span className={styles.stripBody}>
                <span className={styles.stripName}>
                  {e.online != null && <span className={`${styles.dot} ${e.online ? styles.dotOn : styles.dotOff}`} title={e.online ? t('Conectado', 'Connected') : t('Desconectado', 'Offline')} />}
                  {e.name}
                  {e.offScene && <span className={styles.stripTag}>{t('fora da cena', 'off scene')}</span>}
                </span>
                {ratio != null ? (
                  <span className={styles.stripHp}>
                    <span className={styles.meter}><span className={styles.meterFill} style={{ width: `${ratio * 100}%` }} /></span>
                    <span className={styles.stat}>{e.hp}/{e.maxHp}</span>
                  </span>
                ) : (
                  <span className={styles.muted}>{e.dead ? t('Morto', 'Dead') : e.woundState ? WOUND_LABEL[e.woundState] : ''}</span>
                )}
                {(e.spHead != null || e.spBody != null) && <ArmorControl compact armor={e} />}
              </span>
            </button>
            {armed && armed.weapons.length > 0 && (
              <span className={styles.stripWeapons} role="group" aria-label={t(`Armas de ${e.name}`, `${e.name}'s weapons`)}>
                {armed.weapons.map((w) => {
                  const why = armed.blocked ?? cannotFire(w) ?? (e.dead ? t('Morto não ataca.', 'The dead don\'t attack.') : null);
                  const on = shooter && aim?.weapon.key === w.key;
                  const reload = canReload(w);
                  const reloadWhy = armed.blocked ?? reload ?? (e.dead ? t('Morto não recarrega.', "The dead don't reload.") : null);
                  return (
                    <span key={w.key} className={styles.weaponChipGroup}>
                      <button type="button" className={`${styles.weaponChip} ${on ? styles.weaponChipOn : ''}`} disabled={!!why}
                        title={why ?? t('Atirar: escolha o alvo no mapa ou num cartão', 'Shoot: pick the target on the map or on a card')}
                        onClick={() => onWeapon?.(e, w)}>
                        <Crosshair size={11} aria-hidden /> {tv(weaponLabel(w))}
                      </button>
                      {onUnjam && w.jammed && (
                        <button type="button" className={styles.weaponChip} disabled={!!armed.blocked}
                          title={armed.blocked ?? t('Destravar (uma Ação)', 'Unjam (an Action)')} onClick={() => onUnjam(e, w)}>
                          {t('Destravar', 'Unjam')}
                        </button>
                      )}
                      {onReload && w.magazine != null && !w.melee && (
                        <button type="button" className={styles.weaponChip} disabled={!!reloadWhy}
                          aria-label={t(`Recarregar ${tv(w.name)}`, `Reload ${tv(w.name)}`)}
                          title={reloadWhy ?? t(`Recarregar (${w.rounds ?? 0}/${w.magazine}) — uma Ação em combate`, `Reload (${w.rounds ?? 0}/${w.magazine}) — an Action in a fight`)}
                          onClick={() => onReload(e, w)}>
                          <RefreshCw size={11} aria-hidden />
                        </button>
                      )}
                    </span>
                  );
                })}
              </span>
            )}
          </div>
        );
      })}
    </div>
  );
}

/** The aiming bar over the cards: the weapon, its mode, the target picked and the shot. */
export function AimBar({ aim, targetName, busy, onMode, onFire, onCancel }: {
  aim: Aim;
  targetName: string | null;
  busy: boolean;
  onMode: (mode: AttackMode) => void;
  onFire: () => void;
  onCancel: () => void;
}) {
  const modes = quickModes(aim.weapon);
  return (
    <div className={styles.aimBar} role="region" aria-label={t('Mira', 'Aim')}>
      <Crosshair size={16} className={styles.aimIcon} aria-hidden />
      <span className={styles.aimText}>
        <strong>{aim.attackerName}</strong> · {tv(aim.weapon.name)}
        {' → '}
        {targetName ? <strong>{targetName}</strong> : <span className={styles.muted}>{t('clique no alvo no mapa ou num cartão', 'click the target on the map or on a card')}</span>}
      </span>
      {modes.length > 1 && (
        <Select aria-label={t('Modo de ataque', 'Attack mode')} value={aim.mode} onChange={(e) => onMode(e.target.value as AttackMode)}>
          {modes.map((m) => <option key={m} value={m}>{MODE_LABEL[m]}</option>)}
        </Select>
      )}
      <Button variant="primary" icon={<Crosshair size={14} />} disabled={!aim.targetId || busy} onClick={onFire}>{t('Disparar', 'Fire')}</Button>
      <Button variant="ghost" icon={<X size={14} />} onClick={onCancel}>{t('Cancelar (Esc)', 'Cancel (Esc)')}</Button>
    </div>
  );
}
