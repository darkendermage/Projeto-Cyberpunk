import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import clsx from 'clsx';
import { Download, ListChecks, Minus, Pencil, Plus, Trash2 } from 'lucide-react';
import { useList, useListMutations, useLists } from '../../api/hooks';
import type { GearListDetail } from '../../api/types';
import { ApiError } from '../../api/client';
import { ItemArt } from '../../components/art/ItemArt';
import { Button, EmptyState, ErrorState, Field, IconButton, Spinner } from '../../components/ui/ui';
import { downloadJson, formatEb } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import { ItemDetailModal } from '../catalog/ItemDetailModal';
import page from '../../styles/Page.module.css';
import styles from './ListsPage.module.css';
import { t, tv } from '../../i18n';

export function ListsPage() {
  const { id } = useParams();
  const selectedId = id ? Number(id) : null;
  const lists = useLists();
  const navigate = useNavigate();
  const { create } = useListMutations();
  const toast = useToast();
  const [name, setName] = useState('');
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (!selectedId && lists.data?.length) navigate(`/lists/${lists.data[0].id}`, { replace: true });
  }, [selectedId, lists.data, navigate]);

  const onCreate = (e: React.FormEvent) => {
    e.preventDefault();
    create.mutate({ name }, {
      onSuccess: (list) => { setName(''); setError(undefined); toast.notify(t(`Lista “${list.name}” criada.`, `List “${list.name}” created.`)); navigate(`/lists/${list.id}`); },
      onError: (err) => setError(err instanceof ApiError ? err.fieldErrors.name?.[0] ?? err.message : t('Falha ao criar a lista.', 'Couldn\'t create the list.')),
    });
  };

  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch={t('Minhas Listas', 'My Lists')}>{t('Minhas Listas', 'My Lists')}</h1>
        <p className={page.lead}>{t('Monte loadouts, listas de compras ou o inventário de um personagem. O custo total é calculado automaticamente.', 'Build loadouts, shopping lists or a character\'s inventory. The total cost is worked out automatically.')}</p>
      </header>

      <div className={styles.layout}>
        <aside className={styles.sidebar}>
          <form onSubmit={onCreate} className={styles.createForm}>
            <Field label={t('Nova lista', 'New list')} error={error} htmlFor="new-list">
              <div className={styles.inline}>
                <input id="new-list" value={name} onChange={(e) => setName(e.target.value)} placeholder={t('Ex.: Loadout do Solo', 'E.g. Solo loadout')} maxLength={80} />
                <IconButton label={t('Criar lista', 'Create list')} type="submit" disabled={!name.trim() || create.isPending}><Plus size={16} /></IconButton>
              </div>
            </Field>
          </form>
          {lists.isLoading && <Spinner />}
          {lists.isError && <ErrorState error={lists.error} onRetry={() => lists.refetch()} />}
          <nav className={styles.listNav} aria-label={t('Listas', 'Lists')}>
            {lists.data?.map((l) => (
              <Link key={l.id} to={`/lists/${l.id}`} className={clsx(styles.listLink, l.id === selectedId && styles.listActive)}>
                <span className={styles.listName}>{tv(l.name)}</span>
                <span className={styles.listMeta}>{l.totalQuantity} {t('itens · ', 'items · ')}{formatEb(l.totalPrice)} eb</span>
              </Link>
            ))}
          </nav>
        </aside>

        <section className={styles.detail}>
          {selectedId ? <ListDetail id={selectedId} /> : lists.data?.length === 0 && (
            <EmptyState title={t('Nenhuma lista', 'No lists')} icon={<ListChecks size={28} />}>
              {t('Crie uma lista ao lado e adicione itens pela ficha de qualquer item do catálogo.', 'Create a list on the side and add items from any catalog item\'s page.')}
            </EmptyState>
          )}
        </section>
      </div>
    </div>
  );
}

function ListDetail({ id }: { id: number }) {
  const list = useList(id);
  const { update, remove, updateEntry, removeEntry } = useListMutations();
  const toast = useToast();
  const navigate = useNavigate();
  const [editing, setEditing] = useState(false);
  const [openItem, setOpenItem] = useState<number | null>(null);

  if (list.isLoading) return <Spinner />;
  if (list.isError) return <ErrorState error={list.error} onRetry={() => list.refetch()} />;
  const data = list.data!;

  const setQuantity = (entryId: number, quantity: number, note?: string | null) =>
    updateEntry.mutate({ listId: id, entryId, quantity, note }, { onError: (e) => toast.error(e) });

  const onDelete = () => {
    if (!window.confirm(t(`Excluir a lista “${data.name}”? Esta ação não pode ser desfeita.`, `Delete the list “${data.name}”? This can't be undone.`))) return;
    remove.mutate(id, {
      onSuccess: () => { toast.notify(t('Lista excluída.', 'List deleted.')); navigate('/lists'); },
      onError: (e) => toast.error(e),
    });
  };

  return (
    <div className={styles.detailInner}>
      {editing ? (
        <ListEditForm list={data} onDone={() => setEditing(false)}
          onSave={(name, description) => update.mutate({ id, name, description }, {
            onSuccess: () => { setEditing(false); toast.notify(t('Lista atualizada.', 'List updated.')); },
            onError: (e) => toast.error(e),
          })} />
      ) : (
        <header className={styles.detailHeader}>
          <div>
            <h2 className="hud-title">{tv(data.name)}</h2>
            {data.description && <p className={page.lead}>{tv(data.description)}</p>}
          </div>
          <div className={styles.headerActions}>
            <Button icon={<Pencil size={14} />} onClick={() => setEditing(true)}>{t('Renomear', 'Rename')}</Button>
            <Button icon={<Download size={14} />} onClick={() => downloadJson(`lista-${data.id}.json`, data)}>{t('Exportar', 'Export')}</Button>
            <Button variant="danger" icon={<Trash2 size={14} />} onClick={onDelete}>{t('Excluir', 'Delete')}</Button>
          </div>
        </header>
      )}

      <div className={styles.summary}>
        <div><span>{t('Itens', 'Items')}</span><strong>{data.entries.reduce((s, e) => s + e.quantity, 0)}</strong></div>
        <div><span>{t('Custo total', 'Total cost')}</span><strong className={styles.amber}>{formatEb(data.totalPrice)} eb</strong></div>
        {data.unpricedEntries > 0 && <div><span>{t('Sem preço', 'No price')}</span><strong>{data.unpricedEntries}</strong></div>}
      </div>

      {data.entries.length === 0 ? (
        <EmptyState title={t('Lista vazia', 'Empty list')} action={<Link to="/catalog/weapons" className={page.link}>{t('Ir para o catálogo', 'Go to the catalog')}</Link>}>
          {t('Abra a ficha de um item e use “Adicionar à lista”.', 'Open an item\'s page and use “Add to list”.')}
        </EmptyState>
      ) : (
        <ul className={styles.entries}>
          {data.entries.map((e) => (
            <li key={e.id} className={styles.entry}>
              <ItemArt item={e.item} size="row" />
              <div className={styles.entryMain}>
                <button type="button" className={styles.entryName} onClick={() => setOpenItem(e.item.id)}>{tv(e.item.name)}</button>
                <span className={styles.entryMeta}>{tv(e.item.subtype?.label)}{e.item.manufacturer ? ` · ${tv(e.item.manufacturer.name)}` : ''}</span>
                <NoteInput value={e.note ?? ''} onSave={(note) => setQuantity(e.id, e.quantity, note)} />
              </div>
              <div className={styles.qty} aria-label={t(`Quantidade de ${tv(e.item.name)}`, `Quantity of ${tv(e.item.name)}`)}>
                <IconButton label={t('Diminuir quantidade', 'Decrease quantity')} disabled={e.quantity <= 1} onClick={() => setQuantity(e.id, e.quantity - 1, e.note)}><Minus size={14} /></IconButton>
                <span className="mono">{e.quantity}</span>
                <IconButton label={t('Aumentar quantidade', 'Increase quantity')} disabled={e.quantity >= 999} onClick={() => setQuantity(e.id, e.quantity + 1, e.note)}><Plus size={14} /></IconButton>
              </div>
              <span className={styles.entryPrice}>{e.item.price != null ? `${formatEb(e.item.price * e.quantity)} eb` : '—'}</span>
              <IconButton label={t(`Remover ${tv(e.item.name)} da lista`, `Remove ${tv(e.item.name)} from the list`)}
                onClick={() => removeEntry.mutate({ listId: id, entryId: e.id }, { onError: (err) => toast.error(err) })}>
                <Trash2 size={15} />
              </IconButton>
            </li>
          ))}
        </ul>
      )}
      <ItemDetailModal itemId={openItem} onClose={() => setOpenItem(null)} />
    </div>
  );
}

function ListEditForm({ list, onSave, onDone }: { list: GearListDetail; onSave: (name: string, description?: string) => void; onDone: () => void }) {
  const [name, setName] = useState(list.name);
  const [description, setDescription] = useState(list.description ?? '');
  return (
    <form className={styles.editForm} onSubmit={(e) => { e.preventDefault(); onSave(name, description); }}>
      <Field label={t('Nome', 'Name')} htmlFor="list-name"><input id="list-name" value={name} onChange={(e) => setName(e.target.value)} maxLength={80} required /></Field>
      <Field label={t('Descrição', 'Description')} htmlFor="list-desc"><textarea id="list-desc" value={description} onChange={(e) => setDescription(e.target.value)} maxLength={500} /></Field>
      <div className={styles.headerActions}>
        <Button type="submit" variant="primary" disabled={!name.trim()}>{t('Salvar', 'Save')}</Button>
        <Button onClick={onDone}>{t('Cancelar', 'Cancel')}</Button>
      </div>
    </form>
  );
}

function NoteInput({ value, onSave }: { value: string; onSave: (note: string) => void }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  return (
    <input className={styles.note} value={text} placeholder={t('Anotação (opcional)', 'Note (optional)')} maxLength={300} aria-label={t('Anotação', 'Note')}
      onChange={(e) => setText(e.target.value)} onBlur={() => text !== value && onSave(text)}
      onKeyDown={(e) => e.key === 'Enter' && (e.target as HTMLInputElement).blur()} />
  );
}
