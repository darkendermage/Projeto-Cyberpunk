import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
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
      onSuccess: (list) => { setName(''); setError(undefined); toast.notify(`Lista “${list.name}” criada.`); navigate(`/lists/${list.id}`); },
      onError: (err) => setError(err instanceof ApiError ? err.fieldErrors.name?.[0] ?? err.message : 'Falha ao criar a lista.'),
    });
  };

  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch="Minhas Listas">Minhas Listas</h1>
        <p className={page.lead}>Monte loadouts, listas de compras ou o inventário de um personagem. O custo total é calculado automaticamente.</p>
      </header>

      <div className={styles.layout}>
        <aside className={styles.sidebar}>
          <form onSubmit={onCreate} className={styles.createForm}>
            <Field label="Nova lista" error={error} htmlFor="new-list">
              <div className={styles.inline}>
                <input id="new-list" value={name} onChange={(e) => setName(e.target.value)} placeholder="Ex.: Loadout do Solo" maxLength={80} />
                <IconButton label="Criar lista" type="submit" disabled={!name.trim() || create.isPending}><Plus size={16} /></IconButton>
              </div>
            </Field>
          </form>
          {lists.isLoading && <Spinner />}
          {lists.isError && <ErrorState error={lists.error} onRetry={() => lists.refetch()} />}
          <nav className={styles.listNav} aria-label="Listas">
            {lists.data?.map((l) => (
              <Link key={l.id} to={`/lists/${l.id}`} className={clsx(styles.listLink, l.id === selectedId && styles.listActive)}>
                <span className={styles.listName}>{l.name}</span>
                <span className={styles.listMeta}>{l.totalQuantity} itens · {formatEb(l.totalPrice)} eb</span>
              </Link>
            ))}
          </nav>
        </aside>

        <section className={styles.detail}>
          {selectedId ? <ListDetail id={selectedId} /> : lists.data?.length === 0 && (
            <EmptyState title="Nenhuma lista" icon={<ListChecks size={28} />}>
              Crie uma lista ao lado e adicione itens pela ficha de qualquer item do catálogo.
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
    if (!window.confirm(`Excluir a lista “${data.name}”? Esta ação não pode ser desfeita.`)) return;
    remove.mutate(id, {
      onSuccess: () => { toast.notify('Lista excluída.'); navigate('/lists'); },
      onError: (e) => toast.error(e),
    });
  };

  return (
    <div className={styles.detailInner}>
      {editing ? (
        <ListEditForm list={data} onDone={() => setEditing(false)}
          onSave={(name, description) => update.mutate({ id, name, description }, {
            onSuccess: () => { setEditing(false); toast.notify('Lista atualizada.'); },
            onError: (e) => toast.error(e),
          })} />
      ) : (
        <header className={styles.detailHeader}>
          <div>
            <h2 className="hud-title">{data.name}</h2>
            {data.description && <p className={page.lead}>{data.description}</p>}
          </div>
          <div className={styles.headerActions}>
            <Button icon={<Pencil size={14} />} onClick={() => setEditing(true)}>Renomear</Button>
            <Button icon={<Download size={14} />} onClick={() => downloadJson(`lista-${data.id}.json`, data)}>Exportar</Button>
            <Button variant="danger" icon={<Trash2 size={14} />} onClick={onDelete}>Excluir</Button>
          </div>
        </header>
      )}

      <div className={styles.summary}>
        <div><span>Itens</span><strong>{data.entries.reduce((s, e) => s + e.quantity, 0)}</strong></div>
        <div><span>Custo total</span><strong className={styles.amber}>{formatEb(data.totalPrice)} eb</strong></div>
        {data.unpricedEntries > 0 && <div><span>Sem preço</span><strong>{data.unpricedEntries}</strong></div>}
      </div>

      {data.entries.length === 0 ? (
        <EmptyState title="Lista vazia" action={<Link to="/catalog/weapons" className={page.link}>Ir para o catálogo</Link>}>
          Abra a ficha de um item e use “Adicionar à lista”.
        </EmptyState>
      ) : (
        <ul className={styles.entries}>
          {data.entries.map((e) => (
            <li key={e.id} className={styles.entry}>
              <ItemArt item={e.item} size="row" />
              <div className={styles.entryMain}>
                <button type="button" className={styles.entryName} onClick={() => setOpenItem(e.item.id)}>{e.item.name}</button>
                <span className={styles.entryMeta}>{e.item.subtype?.label}{e.item.manufacturer ? ` · ${e.item.manufacturer.name}` : ''}</span>
                <NoteInput value={e.note ?? ''} onSave={(note) => setQuantity(e.id, e.quantity, note)} />
              </div>
              <div className={styles.qty} aria-label={`Quantidade de ${e.item.name}`}>
                <IconButton label="Diminuir quantidade" disabled={e.quantity <= 1} onClick={() => setQuantity(e.id, e.quantity - 1, e.note)}><Minus size={14} /></IconButton>
                <span className="mono">{e.quantity}</span>
                <IconButton label="Aumentar quantidade" disabled={e.quantity >= 999} onClick={() => setQuantity(e.id, e.quantity + 1, e.note)}><Plus size={14} /></IconButton>
              </div>
              <span className={styles.entryPrice}>{e.item.price != null ? `${formatEb(e.item.price * e.quantity)} eb` : '—'}</span>
              <IconButton label={`Remover ${e.item.name} da lista`}
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
      <Field label="Nome" htmlFor="list-name"><input id="list-name" value={name} onChange={(e) => setName(e.target.value)} maxLength={80} required /></Field>
      <Field label="Descrição" htmlFor="list-desc"><textarea id="list-desc" value={description} onChange={(e) => setDescription(e.target.value)} maxLength={500} /></Field>
      <div className={styles.headerActions}>
        <Button type="submit" variant="primary" disabled={!name.trim()}>Salvar</Button>
        <Button onClick={onDone}>Cancelar</Button>
      </div>
    </form>
  );
}

function NoteInput({ value, onSave }: { value: string; onSave: (note: string) => void }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  return (
    <input className={styles.note} value={text} placeholder="Anotação (opcional)" maxLength={300} aria-label="Anotação"
      onChange={(e) => setText(e.target.value)} onBlur={() => text !== value && onSave(text)}
      onKeyDown={(e) => e.key === 'Enter' && (e.target as HTMLInputElement).blur()} />
  );
}
