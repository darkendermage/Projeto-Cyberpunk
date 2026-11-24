import { useState } from 'react';
import { useCharacterRules } from '../../api/characters';
import type { InjuryLocation } from '../../api/characterTypes';
import { Button, Select, Switch } from '../../components/ui/ui';
import { t, tv } from '../../i18n';

// A Critical Injury from the tables, by hand: Flashbangs (Damaged Eye and Ear), other effects or a GM call. The +5
// bonus damage belongs to a hit (CP:R p.187), so it starts off.
export function InjuryPicker({ busy, rowClass, onAdd }: {
  busy?: boolean;
  rowClass?: string;
  onAdd: (location: InjuryLocation, name: string, bonusDamage: boolean) => void;
}) {
  const rules = useCharacterRules().data;
  const [location, setLocation] = useState<InjuryLocation>('Body');
  const [name, setName] = useState('');
  const [bonus, setBonus] = useState(false);
  const table = (rules?.criticalInjuries ?? []).filter((i) => i.location === location);
  const picked = table.find((i) => i.name === name);
  return (
    <span className={rowClass}>
      <Select aria-label={t('Tabela de lesão', 'Injury table')} value={location} onChange={(e) => { setLocation(e.target.value as InjuryLocation); setName(''); }}>
        <option value="Body">{t('Corpo', 'Body')}</option>
        <option value="Head">{t('Cabeça', 'Head')}</option>
      </Select>
      <Select aria-label={t('Lesão crítica', 'Critical Injury')} value={name} onChange={(e) => setName(e.target.value)}>
        <option value="">{t('Lesão crítica…', 'Critical Injury…')}</option>
        {table.map((i) => <option key={i.name} value={i.name}>{i.roll} · {tv(i.name)}</option>)}
      </Select>
      <Switch checked={bonus} onChange={setBonus} label={t('+5 de dano', '+5 damage')} />
      <Button disabled={busy || !name} title={picked ? tv(picked.effect) : undefined} onClick={() => { onAdd(location, name, bonus); setName(''); }}>
        {t('Aplicar lesão', 'Apply injury')}
      </Button>
    </span>
  );
}
