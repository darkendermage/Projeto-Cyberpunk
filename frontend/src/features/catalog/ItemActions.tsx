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

export function FavoriteButton({ item, variant = 'overlay' }: { item: Pick<Item, 'id' | 'name'>; variant?: 'overlay' | 'inline' }) {
  const ids = useFavoriteIds();
  const toggle = useToggleFavorite();
  const toast = useToast();
  const active = ids.data?.includes(item.id) ?? false;

  return (
    <button type="button" className={clsx(styles.action, styles[variant], active && styles.starOn)}
      aria-pressed={active} aria-label={active ? `Remover ${item.name} dos favoritos` : `Favoritar ${item.name}`}
      title={active ? 'Remover dos favoritos' : 'Favoritar'}
      onClick={(e) => {
        e.stopPropagation();
        toggle.mutate({ id: item.id, favorite: !active }, {
          onError: (error) => toast.error(error, 'Não foi possível atualizar os favoritos.'),
        });
      }}>
      <Star size={variant === 'inline' ? 16 : 15} fill={active ? 'currentColor' : 'none'} />
      {variant === 'inline' && <span>{active ? 'Favorito' : 'Favoritar'}</span>}
    </button>
  );
}

export function CompareButton({ item, variant = 'overlay' }: { item: Pick<Item, 'id' | 'name' | 'category'>; variant?: 'overlay' | 'inline' }) {
  const compare = useCompare();
  const toast = useToast();
  const active = compare.has(item.id);

  return (
    <button type="button" className={clsx(styles.action, styles[variant], active && styles.compareOn)}
      aria-pressed={active} aria-label={active ? `Remover ${item.name} da comparação` : `Comparar ${item.name}`}
      title={active ? 'Remover da comparação' : 'Adicionar à comparação'}
      onClick={(e) => {
        e.stopPropagation();
        const result = compare.toggle(item);
        if (result === 'full') toast.notify(`A comparação aceita até ${MAX_COMPARE} itens.`, 'info');
      }}>
      <GitCompareArrows size={variant === 'inline' ? 16 : 15} />
      {variant === 'inline' && <span>{active ? 'Comparando' : 'Comparar'}</span>}
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
      onSuccess: () => { toast.notify(`${item.name} adicionado a “${listName}”.`); setOpen(false); },
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
        <span>Adicionar à lista</span>
      </button>
      {open && (
        <div className={styles.menu} role="menu">
          <p className={styles.menuTitle}>Minhas listas</p>
          {lists.isLoading && <Spinner />}
          {lists.data?.length === 0 && <p className={styles.menuEmpty}>Você ainda não tem listas.</p>}
          {lists.data?.map((l) => (
            <button key={l.id} type="button" role="menuitem" onClick={() => add(l.id, l.name)} disabled={addEntry.isPending}>
              <span>{l.name}</span>
              <span className={styles.menuCount}>{l.entryCount}</span>
            </button>
          ))}
          <form onSubmit={createAndAdd} className={styles.newList}>
            <input value={newName} onChange={(e) => setNewName(e.target.value)} placeholder="Nova lista..." maxLength={80}
              aria-label="Nome da nova lista" />
            <button type="submit" aria-label="Criar lista e adicionar" disabled={!newName.trim() || create.isPending}><Plus size={14} /></button>
          </form>
          <Link to="/lists" className={styles.menuLink}>Gerenciar listas</Link>
        </div>
      )}
    </div>
  );
}
