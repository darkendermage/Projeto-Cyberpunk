import { useCharacterRules } from '../../api/characters';
import type { SpecialMove } from '../../api/characterTypes';
import { t, tv } from '../../i18n';
import { moveFormula } from '../reference/martialArts';
import styles from './Table.module.css';

const signed = (n: number) => (n >= 0 ? `+${n}` : `${n}`);

// The Special Moves of the forms the fighter knows. Checks roll the Resolution in the chat (against the move's DV);
// strikes are attack modes of the Martial Arts weapon in a fight (pick them in the aim bar), and Cut the Bullet shows
// up on the dodge card. Out of a fight every move just rolls its Resolution.
export function MartialMoves({ forms, inFight, onRoll }: {
  forms: { form: string; total: number }[];
  inFight: boolean;
  onRoll: (formula: string, label: string) => void;
}) {
  const rules = useCharacterRules();
  const known = forms
    .map((f) => ({ ...f, rule: rules.data?.martialArts.find((r) => r.name.toLowerCase() === f.form.toLowerCase()) }))
    .filter((f) => f.rule);
  if (known.length === 0) return null;
  const shared = rules.data?.sharedMoves ?? [];

  const chip = (form: string, total: number, m: SpecialMove) => {
    const atTable = inFight && m.kind !== 'check';
    const hint = m.kind === 'attack'
      ? t('No combate: escolha este golpe no modo da arma ao mirar com Martial Arts.', 'In a fight: pick this strike as the weapon mode when aiming with Martial Arts.')
      : m.kind === 'defense' ? t('No combate: aparece na carta de esquiva quando dá para usar.', 'In a fight: it shows up on the dodge card when it can be used.') : null;
    return (
      <button key={m.id} type="button" className={styles.weaponChip} disabled={atTable}
        title={[`${t('Requisito', 'Requirement')}: ${tv(m.requirement)}`, tv(m.effect), hint].filter(Boolean).join('\n')}
        onClick={() => onRoll(moveFormula(total), `${form}: ${m.name}${m.dv ? ` (DV${m.dv})` : ''}`)}>
        {m.name}{m.dv ? ` · DV${m.dv}` : ''}{m.kind === 'attack' ? ' ⚔' : m.kind === 'defense' ? ' ⛨' : ''}
      </button>
    );
  };

  return (
    <div className={styles.section}>
      <span className={styles.label}>{t('Golpes de Martial Arts', 'Martial Arts moves')}</span>
      {known.map(({ form, total, rule }) => (
        <div key={form} className={styles.stack} style={{ gap: 4 }}>
          <span className={styles.source}>{form} · {t('Resolução', 'Resolution')} 1d10{signed(total)} · {rule!.source} p.{rule!.page}</span>
          <div className={styles.row}>{[...shared, ...rule!.moves].map((m) => chip(form, total, m))}</div>
        </div>
      ))}
      {inFight && <span className={styles.muted}>{t('⚔ golpe: modo da arma de Martial Arts na mira · ⛨ defesa: na carta de esquiva.', '⚔ strike: Martial Arts weapon mode in the aim bar · ⛨ defense: on the dodge card.')}</span>}
    </div>
  );
}
