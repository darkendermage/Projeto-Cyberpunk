import type { ReactNode } from 'react';
import { Flag, Globe2, Target, UserRound } from 'lucide-react';
import type { CharacterDetail, CharacterRules } from '../../api/characterTypes';
import { Portrait } from './CharacterUi';
import { heroChips, type HeroChip } from './lifepath';
import lp from './Lifepath.module.css';
import { t, tv } from '../../i18n';

const CHIP_ICON: Record<HeroChip['key'], typeof Flag> = { identity: Globe2, personality: UserRound, motivation: Target, goal: Flag };

/** Header of the Lifepath tab: portrait, main Role with its ability, and the four answers that define the character. */
export function LifepathHero({ detail, rules, sheet, actions }: {
  detail: CharacterDetail; rules: CharacterRules; sheet: CharacterDetail['sheet']; actions?: ReactNode;
}) {
  const role = rules.roles.find((r) => r.id === sheet.roles[0]?.role);
  return (
    <section className={lp.hero}>
      <div className={lp.heroPortrait}><Portrait detail={detail} compact /></div>
      <div className={lp.heroMain}>
        <div className={lp.heroTitleRow}>
          <h2 className={lp.heroTitle}>
            {t('Caminho de vida', 'Lifepath')}{role && <>: <span className={lp.heroRole}>{role.id}</span></>}
          </h2>
          {role && <span className={lp.seal}>{role.lifepathSource}</span>}
        </div>
        {role && (
          <p className={lp.heroLine} title={role.abilityInfo.source}>
            <strong>{role.ability}:</strong> {tv(role.abilityInfo.summary)}
          </p>
        )}
        {actions && <div className={lp.heroActions}>{actions}</div>}
      </div>
      <dl className={lp.heroChips}>
        {heroChips(sheet).map((chip) => {
          const Icon = CHIP_ICON[chip.key];
          return (
            <div key={chip.key} className={lp.heroChip}>
              <Icon size={22} className={lp.heroChipIcon} aria-hidden />
              <div>
                <dt>{chip.label}</dt>
                <dd className={chip.value ? undefined : lp.empty}>{chip.value ? tv(chip.value) : '—'}</dd>
              </div>
            </div>
          );
        })}
      </dl>
    </section>
  );
}
