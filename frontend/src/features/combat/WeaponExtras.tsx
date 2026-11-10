import { RefreshCw, Repeat } from 'lucide-react';
import type { WeaponView } from '../../api/encounterTypes';
import { t, tv } from '../../i18n';
import { extraMagazines, SWITCH_COST, switchedModes } from './combat';

// The buttons a weapon with its own rules adds next to its chip: switch to another mode that stays on the weapon,
// reload an extra magazine (Tommyknocker shells, Crowd Buster battery...) and the destroyed tag. Used on the combat
// queue, the table cards and the player's panel, each with its own chip style.
export function WeaponExtras({ weapon, chipClass, blocked, onSwitch, onReload }: {
  weapon: WeaponView;
  chipClass: string;
  /** Why nothing can be done now (not their Turn...); null when it can. */
  blocked?: string | null;
  onSwitch?: (mode: string) => void;
  onReload?: (magazine: string) => void;
}) {
  if (weapon.destroyed) {
    return <span className={chipClass} aria-disabled title={t('Destruída: não dá mais para usar.', "Destroyed: it can't be used anymore.")}>{t('Destruída', 'Destroyed')}</span>;
  }
  const others = switchedModes(weapon).filter((m) => !m.modeActive);
  return (
    <>
      {onSwitch && others.map((m) => (
        <button key={m.modeId} type="button" className={chipClass} disabled={!!blocked}
          aria-label={t(`Trocar ${tv(weapon.name)} para ${m.modeLabel}`, `Switch ${tv(weapon.name)} to ${m.modeLabel}`)}
          title={blocked ?? t(`Trocar para ${m.modeLabel} (${SWITCH_COST[m.modeSwitch ?? 'action']})`, `Switch to ${m.modeLabel} (${SWITCH_COST[m.modeSwitch ?? 'action']})`)}
          onClick={() => onSwitch(m.modeId!)}>
          <Repeat size={11} aria-hidden /> {m.modeLabel}
        </button>
      ))}
      {onReload && extraMagazines(weapon).map((mag) => {
        const why = blocked ?? (mag.rounds >= mag.capacity ? t('Cheio.', 'Full.') : null);
        return (
          <button key={mag.id} type="button" className={chipClass} disabled={!!why}
            aria-label={t(`Recarregar ${tv(weapon.name)} (${mag.label})`, `Reload ${tv(weapon.name)} (${mag.label})`)}
            title={why ?? t(`Recarregar ${mag.label} — uma Ação em combate`, `Reload ${mag.label} — an Action in a fight`)}
            onClick={() => onReload(mag.id!)}>
            <RefreshCw size={11} aria-hidden /> {mag.label} {mag.rounds}/{mag.capacity}
          </button>
        );
      })}
    </>
  );
}
