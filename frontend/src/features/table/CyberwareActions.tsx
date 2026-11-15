import type { ActiveCyberState, CyberTrait } from '../../api/encounterTypes';
import { Badge } from '../../components/ui/ui';
import { t, tv } from '../../i18n';
import styles from './Table.module.css';

const KIND_LABEL: Record<string, (x: CyberTrait) => string> = {
  initiative: (x) => t(`+${x.add} Iniciativa`, `+${x.add} Initiative`),
  aimed: (x) => t(`+${x.add} tiro mirado`, `+${x.add} aimed shot`),
  rangedFar: (x) => t(`+${x.add} a ${x.value} m+`, `+${x.add} at ${x.value} m+`),
  dodgeAnyRef: () => t('esquiva tiros', 'dodges bullets'),
  ignoreSeriously: () => t('ignora Seriously Wounded', 'ignores Seriously Wounded'),
  noHeadDouble: () => t('cabeça sem dano dobrado', 'no double head damage'),
  immune: (x) => t(`imune: ${x.injuries.join(', ')}`, `immune: ${x.injuries.join(', ')}`),
  statMin: (x) => `${x.stats.join('/')} ≥ ${x.value}`,
  wieldBody: (x) => t(`empunha como BODY ${x.value}`, `wields as BODY ${x.value}`),
  shield: (x) => t(`escudo ${x.value} HP`, `shield ${x.value} HP`),
};

// The fighter's implants at the table: what applies by itself (shown, already in the rolls) and what is switched on
// with a button (Sandevistan, Berserk, Flashbulb...). Switched-on pieces show until when they last.
export function CyberwareActions({ traits, active, blocked, onActivate }: {
  traits: CyberTrait[] | null | undefined;
  active?: Record<string, ActiveCyberState> | null;
  /** why the buttons are off right now (not your Turn...) */
  blocked?: string | null;
  onActivate?: (slug: string) => void;
}) {
  const list = traits ?? [];
  if (list.length === 0) return null;
  const passive = list.filter((x) => x.kind);
  const switches = list.filter((x) => x.activate);
  return (
    <div className={styles.section}>
      <span className={styles.label}>Cyberware</span>
      {passive.length > 0 && (
        <span className={styles.row}>
          {passive.map((x) => (
            <span key={`${x.slug}-${x.kind}`} title={`${tv(x.note)} (${x.source})`}><Badge tone="cyan">{tv(x.label)}: {KIND_LABEL[x.kind!]?.(x) ?? x.kind}</Badge></span>
          ))}
        </span>
      )}
      {switches.length > 0 && (
        <span className={styles.row}>
          {switches.map((x) => {
            const on = active?.[x.slug];
            return on
              ? <span key={x.slug} title={tv(x.note)}><Badge tone="amber">⚡ {tv(x.label)} {t(`ligado até a rodada ${on.untilRound}`, `on until Round ${on.untilRound}`)}</Badge></span>
              : (
                <button key={x.slug} type="button" className={styles.weaponChip} disabled={!onActivate || !!blocked}
                  title={[tv(x.note), x.source, blocked].filter(Boolean).join('\n')} onClick={() => onActivate?.(x.slug)}>
                  ⚡ {t(`Ativar ${tv(x.label)}`, `Switch on ${tv(x.label)}`)}
                </button>
              );
          })}
        </span>
      )}
    </div>
  );
}
