import { useState } from 'react';
import { X } from 'lucide-react';
import { IconButton } from '../../components/ui/ui';
import { t } from '../../i18n';
import { matchSources, SOURCE_LABEL, sourceOf, type TokenSource } from './sources';
import styles from './Table.module.css';

// Search over PCs, saved NPCs and the compendium; the pick's image shows on the token.
export function SourcePicker({ value, sources, onChange }: {
  value: { characterId?: number | null; npcId?: number | null; compendiumNpcId?: string | null };
  sources: TokenSource[];
  onChange: (source: TokenSource | null) => void;
}) {
  const [query, setQuery] = useState('');
  const current = sourceOf(value, sources);
  const linked = value.characterId != null || value.npcId != null || !!value.compendiumNpcId;
  const matches = query.trim() ? matchSources(sources, query, 8) : [];

  if (linked) {
    return (
      <div className={styles.row}>
        <span className={styles.sourceTag}>
          <small>{current ? SOURCE_LABEL[current.kind] : t('Origem', 'Source')}</small>
          {current?.name ?? t('(não encontrado)', '(not found)')}
        </span>
        <IconButton label={t('Desvincular', 'Unlink')} onClick={() => onChange(null)}><X size={14} /></IconButton>
      </div>
    );
  }
  return (
    <div className={styles.sourcePicker}>
      <input aria-label={t('Personagem ou NPC', 'Character or NPC')} value={query} placeholder={t('Buscar PC, NPC ou compêndio…', 'Search PC, NPC or compendium…')}
        onChange={(e) => setQuery(e.target.value)} />
      {matches.length > 0 && (
        <ul className={styles.sourceList} role="listbox" aria-label={t('Resultados', 'Results')}>
          {matches.map((s) => (
            <li key={`${s.kind}:${s.id}`}>
              <button type="button" role="option" aria-selected={false} onClick={() => { setQuery(''); onChange(s); }}>
                <span>{s.name}</span><small>{SOURCE_LABEL[s.kind]}</small>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
