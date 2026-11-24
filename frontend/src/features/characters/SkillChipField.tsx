import { useState } from 'react';
import { useCharacterRules } from '../../api/characters';
import { Select } from '../../components/ui/ui';
import { skillName, t } from '../../i18n';
import styles from './Characters.module.css';

// The skill a Skill Chip is made for: any skill; specialized ones (Language, Play Instrument...) take a specialization.
export function SkillChipField({ value, onChange }: { value: string | null; onChange: (key: string | null) => void }) {
  const rules = useCharacterRules().data;
  const [id, setId] = useState(value?.split(':')[0] ?? '');
  const [spec, setSpec] = useState(value?.split(':').slice(1).join(':') ?? '');
  const skills = [...(rules?.skills ?? [])].sort((a, b) => skillName(a.name, a.namePt).localeCompare(skillName(b.name, b.namePt)));
  const chosen = skills.find((s) => s.id === id);
  const emit = (nextId: string, nextSpec: string) => {
    const skill = skills.find((s) => s.id === nextId);
    onChange(!skill ? null : skill.specialized ? (nextSpec.trim() ? `${nextId}:${nextSpec.trim()}` : null) : nextId);
  };
  return (
    <span className={styles.row}>
      <Select value={id} aria-label={t('Perícia do Skill Chip', 'Skill Chip skill')} onChange={(e) => { setId(e.target.value); emit(e.target.value, spec); }}>
        <option value="">{t('Perícia do chip…', 'Chip skill…')}</option>
        {skills.map((s) => <option key={s.id} value={s.id}>{skillName(s.name, s.namePt)}{s.doubleCost ? ' (x2)' : ''}</option>)}
      </Select>
      {chosen?.specialized && (
        <input className={styles.input} value={spec} placeholder={t('Especialização', 'Specialization')} aria-label={t('Especialização', 'Specialization')}
          onChange={(e) => { setSpec(e.target.value); emit(id, e.target.value); }} />
      )}
    </span>
  );
}
