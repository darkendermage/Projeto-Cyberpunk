import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { CornerDownLeft, Search } from 'lucide-react';
import { useGlobalSearch } from '../../api/hooks';
import { useDebouncedValue } from '../../lib/hooks';
import { formatEb } from '../../lib/format';
import { Modal, Spinner } from '../../components/ui/ui';
import { CategoryIcon } from '../../components/art/CategoryIcon';
import { CATEGORY_ICONS } from '../catalog/categoryMeta';
import styles from './CommandPalette.module.css';
import { t, tv } from '../../i18n';

/** Ctrl+K search across the whole catalog; Enter opens the item inside its category. */
export function CommandPalette({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [query, setQuery] = useState('');
  const [cursor, setCursor] = useState(0);
  const debounced = useDebouncedValue(query, 250);
  const search = useGlobalSearch(debounced);
  const navigate = useNavigate();

  const flat = useMemo(() => (search.data ?? []).flatMap((g) => g.items), [search.data]);

  useEffect(() => setCursor(0), [debounced]);
  useEffect(() => { if (!open) setQuery(''); }, [open]);

  const go = (category: string, id?: number) => {
    onClose();
    navigate(id ? `/catalog/${category}?item=${id}` : `/catalog/${category}?q=${encodeURIComponent(debounced)}`);
  };

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setCursor((c) => Math.min(c + 1, flat.length - 1)); }
    if (e.key === 'ArrowUp') { e.preventDefault(); setCursor((c) => Math.max(c - 1, 0)); }
    if (e.key === 'Enter' && flat[cursor]) { e.preventDefault(); go(flat[cursor].category, flat[cursor].id); }
  };

  let index = -1;
  return (
    <Modal open={open} onClose={onClose} variant="top">
      <div className={styles.inputRow}>
        <Search size={18} />
        <input data-autofocus autoFocus value={query} onChange={(e) => setQuery(e.target.value)} onKeyDown={onKeyDown}
          placeholder={t('Buscar em todo o catálogo: nome, fabricante, efeito...', 'Search the whole catalog: name, manufacturer, effect...')} aria-label={t('Buscar em todo o catálogo', 'Search the whole catalog')}
          role="combobox" aria-expanded={flat.length > 0} aria-controls="palette-results" />
        {search.isFetching && <Spinner />}
        <kbd>Esc</kbd>
      </div>

      <div id="palette-results" className={styles.results} role="listbox">
        {debounced.trim().length < 2 && <p className={styles.hint}>{t('Digite pelo menos 2 caracteres. Ex.: “militech”, “smartgun”, “SP 11”.', 'Type at least 2 characters. E.g. “militech”, “smartgun”, “SP 11”.')}</p>}
        {debounced.trim().length >= 2 && search.data?.length === 0 && !search.isFetching && (
          <p className={styles.hint}>{t('Nenhum registro encontrado para “', 'Nothing found for “')}{debounced}”.</p>
        )}
        {search.isError && <p className={styles.hint}>{t('Falha na busca: ', 'Search failed: ')}{tv((search.error as Error).message)}</p>}
        {search.data?.map((group) => (
          <section key={group.category} className={styles.group}>
            <header>
              <CategoryIcon icon={CATEGORY_ICONS[group.category] ?? 'gear'} size={16} />
              <span>{tv(group.label)}</span>
              <button type="button" onClick={() => go(group.category)}>{t('ver ', 'see ')}{group.total} {t('resultado', 'result')}{group.total > 1 ? 's' : ''}</button>
            </header>
            {group.items.map((item) => {
              index += 1;
              const active = index === cursor;
              const i = index;
              return (
                <button key={item.id} type="button" role="option" aria-selected={active}
                  className={active ? styles.active : undefined}
                  onMouseEnter={() => setCursor(i)} onClick={() => go(item.category, item.id)}>
                  <span className={styles.name}>{tv(item.name)}</span>
                  <span className={styles.meta}>{tv(item.subtype?.label)}{item.manufacturer ? ` · ${tv(item.manufacturer.name)}` : ''}</span>
                  <span className={styles.price}>{item.price != null ? `${formatEb(item.price)} eb` : ''}</span>
                  {active && <CornerDownLeft size={14} className={styles.enter} />}
                </button>
              );
            })}
          </section>
        ))}
      </div>
    </Modal>
  );
}
