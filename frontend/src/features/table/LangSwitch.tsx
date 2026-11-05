import { Segmented } from '../../components/ui/ui';
import { lang, setLang, type Lang } from '../../i18n';

/** PT/EN for the player's screens, which live outside the app shell and its settings panel. */
export function LangSwitch() {
  return (
    <Segmented<Lang> size="sm" ariaLabel="Language / Idioma" value={lang} onChange={setLang}
      options={[{ value: 'pt', label: 'PT' }, { value: 'en', label: 'EN' }]} />
  );
}
