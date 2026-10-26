import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router';
import clsx from 'clsx';
import { GitCompareArrows, ListPlus, Plus, Star } from 'lucide-react';
import type { Item } from '../../api/types';
import { useFavoriteIds, useListMutations, useLists, useToggleFavorite } from '../../api/hooks';
import { MAX_COMPARE, useCompare } from '../../state/CompareContext';
import { useToast } from '../../state/ToastContext';
import { Spinner } from '../../components/ui/ui';
import styles from './ItemActions.module.css';
import { t, tv } from '../../i18n';

export function FavoriteButton({ item, variant = 'overlay' }: { item: Pick<Item, 'id' | 'name'>; variant?: 'overlay' | 'inline' }) {
  const ids = useFavoriteIds();
  const toggle = useToggleFavorite();
  const toast = useToast();
  const active = ids.data?.includes(item.id) ?? false;

  return (
    <button type="button" className={clsx(styles.action, styles[variant], active && styles.starOn)}
      aria-pressed={active} aria-label={active ? t(`Remover ${tv(item.name)} dos favoritos`, `Remove ${tv(item.name)} from favorites`) : t(`Favoritar ${tv(item.name)}`, `Favorite ${tv(item.name)}`)}
      title={active ? t('Remover dos favoritos', 'Remove from favorites') : t('Favoritar', 'Favorite')}
      onClick={(e) => {
        e.stopPropagation();
        toggle.mutate({ id: item.id, favorite: !active }, {
          onError: (error) => toast.error(error, t('Não foi possível atualizar os favoritos.', 'Couldn\'t update the favorites.')),
        });
      }}>
      <Star size={variant === 'inline' ? 16 : 15} fill={active ? 'currentColor' : 'none'} />
      {variant === 'inline' && <span>{active ? t('Favorito', 'Favorite') : t('Favoritar', 'Favorite')}</span>}
    </button>
  );
}

export function CompareButton({ item, variant = 'overlay' }: { item: Pick<Item, 'id' | 'name' | 'category'>; variant?: 'overlay' | 'inline' }) {
  const compare = useCompare();
  const toast = useToast();
  const active = compare.has(item.id);

  return (
    <button type="button" className={clsx(styles.action, styles[variant], active && styles.compareOn)}
      aria-pressed={active} aria-label={active ? t(`Remover ${tv(item.name)} da comparação`, `Remove ${tv(item.name)} from the comparison`) : t(`Comparar ${tv(item.name)}`, `Compare ${tv(item.name)}`)}
      title={active ? t('Remover da comparação', 'Remove from comparison') : t('Adicionar à comparação', 'Add to comparison')}
      onClick={(e) => {
        e.stopPropagation();
        const result = compare.toggle(item);
        if (result === 'full') toast.notify(t(`A comparação aceita até ${MAX_COMPARE} itens.`, `The comparison takes up to ${MAX_COMPARE} items.`), 'info');
      }}>
      <GitCompareArrows size={variant === 'inline' ? 16 : 15} />
      {variant === 'inline' && <span>{active ? t('Comparando', 'Comparing') : t('Comparar', 'Compare')}</span>}
    </button>
  );
}

/** Dropdown to add an item to one of the user's lists (or create a new list on the fly). */
export function AddToListMenu({ item }: { item: Pick<Item, 'id' | 'name'> }) {
  const [open, setOpen] = useState(false);
  const [newName, setNewName] = useState('');
  const lists = useLists();
  const { addEntry, create } = useListMutations();
  const toast = useToast();
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => { if (!ref.current?.contains(e.target as Node)) setOpen(false); };
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') { e.stopPropagation(); setOpen(false); } };
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey, true);
    return () => { document.removeEventListener('mousedown', onDown); document.removeEventListener('keydown', onKey, true); };
  }, [open]);

  const add = (listId: number, listName: string) =>
    addEntry.mutate({ listId, itemId: item.id }, {
      onSuccess: () => { toast.notify(t(`${item.name} adicionado a “${listName}”.`, `${item.name} added to “${listName}”.`)); setOpen(false); },
      onError: (error) => toast.error(error),
    });

  const createAndAdd = (e: React.FormEvent) => {
    e.preventDefault();
    const name = newName.trim();
    if (!name) return;
    create.mutate({ name }, {
      onSuccess: (list) => { setNewName(''); add(list.id, list.name); },
      onError: (error) => toast.error(error),
    });
  };

  return (
    <div className={styles.menuWrap} ref={ref}>
      <button type="button" className={clsx(styles.action, styles.inline)} aria-expanded={open} aria-haspopup="menu"
        onClick={(e) => { e.stopPropagation(); setOpen((o) => !o); }}>
        <ListPlus size={16} />
        <span>{t('Adicionar à lista', 'Add to list')}</span>
      </button>
      {open && (
        <div className={styles.menu} role="menu">
          <p className={styles.menuTitle}>{t('Minhas listas', 'My lists')}</p>
          {lists.isLoading && <Spinner />}
          {lists.data?.length === 0 && <p className={styles.menuEmpty}>{t('Você ainda não tem listas.', 'You don\'t have any lists yet.')}</p>}
          {lists.data?.map((l) => (
            <button key={l.id} type="button" role="menuitem" onClick={() => add(l.id, l.name)} disabled={addEntry.isPending}>
              <span>{tv(l.name)}</span>
              <span className={styles.menuCount}>{l.entryCount}</span>
            </button>
          ))}
          <form onSubmit={createAndAdd} className={styles.newList}>
            <input value={newName} onChange={(e) => setNewName(e.target.value)} placeholder={t('Nova lista...', 'New list...')} maxLength={80}
              aria-label={t('Nome da nova lista', 'Name of the new list')} />
            <button type="submit" aria-label={t('Criar lista e adicionar', 'Create list and add')} disabled={!newName.trim() || create.isPending}><Plus size={14} /></button>
          </form>
          <Link to="/lists" className={styles.menuLink}>{t('Gerenciar listas', 'Manage lists')}</Link>
        </div>
      )}
    </div>
  );
}
