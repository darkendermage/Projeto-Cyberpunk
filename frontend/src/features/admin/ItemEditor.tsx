import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { Save, Trash2 } from 'lucide-react';
import { ApiError, api } from '../../api/client';
import { useCategories, useInvalidateCatalog, useItemDetail, useSchema } from '../../api/hooks';
import type { AttributeValue, FieldDescriptor, Item, ItemUpsert } from '../../api/types';
import { Button, Checkbox, ErrorState, Field, Select, Spinner } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import styles from './AdminPage.module.css';

const PRICE_CATEGORIES = ['Cheap', 'Everyday', 'Costly', 'Premium', 'Expensive', 'VeryExpensive', 'Luxury', 'SuperLuxury'];

interface FormState {
  category: string;
  name: string;
  slug: string;
  price: string;
  priceNote: string;
  priceCategory: string;
  manufacturer: string;
  source: string;
  page: string;
  imageUrl: string;
  ruleSystem: string;
  dataOrigin: string;
  tags: string;
  description: string;
  notes: string;
  attributes: Record<string, AttributeValue>;
  requiresItem: string;
}

const empty = (category: string): FormState => ({
  category, name: '', slug: '', price: '', priceNote: '', priceCategory: '', manufacturer: '', source: '', page: '',
  imageUrl: '', ruleSystem: 'CyberpunkRed', dataOrigin: 'Custom', tags: '', description: '', notes: '', attributes: {}, requiresItem: '',
});

function fromItem(item: Item): FormState {
  return {
    category: item.category,
    name: item.name,
    slug: item.slug,
    price: item.price != null ? String(item.price) : '',
    priceNote: item.priceNote ?? '',
    priceCategory: item.priceCategory ?? '',
    manufacturer: item.manufacturer?.name ?? '',
    source: item.source?.name ?? '',
    page: item.page != null ? String(item.page) : '',
    imageUrl: item.imageUrl ?? '',
    ruleSystem: item.ruleSystem,
    dataOrigin: item.dataOrigin,
    tags: item.tags.join(', '),
    description: item.description ?? '',
    notes: item.notes ?? '',
    attributes: { ...item.attributes },
    requiresItem: typeof item.attributes.requiresItem === 'string' ? item.attributes.requiresItem : '',
  };
}

/** Form generated from the category schema; the API validates again and returns per-field errors. */
export function ItemEditor({ editId, onDone }: { editId: number | null; onDone: () => void }) {
  const categories = useCategories();
  const detail = useItemDetail(editId);
  const [form, setForm] = useState<FormState>(() => empty('weapons'));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const schema = useSchema(form.category);
  const toast = useToast();
  const invalidate = useInvalidateCatalog();
  const navigate = useNavigate();

  useEffect(() => {
    if (detail.data) setForm(fromItem(detail.data.item));
    else if (editId === null) setForm(empty('weapons'));
    setErrors({});
  }, [detail.data, editId]);

  const subtypeKey = schema.data?.subtypeField ?? '';
  const subtype = form.attributes[subtypeKey] as string | undefined;
  const attributeFields = useMemo(() => (schema.data?.fields ?? []).filter((f) =>
    !f.base && f.editable && (!f.appliesTo?.length || !subtype || f.appliesTo.includes(subtype))), [schema.data, subtype]);

  const set = <K extends keyof FormState>(key: K, value: FormState[K]) => setForm((f) => ({ ...f, [key]: value }));
  const setAttr = (key: string, value: AttributeValue) => setForm((f) => ({ ...f, attributes: { ...f.attributes, [key]: value } }));

  if (editId !== null && detail.isLoading) return <Spinner />;
  if (editId !== null && detail.isError) return <ErrorState error={detail.error} onRetry={() => detail.refetch()} />;

  const toPayload = (): ItemUpsert => {
    const attributes: Record<string, AttributeValue> = {};
    // Attributes hidden for the current subtype are cleared, so switching e.g. armor -> clothing drops SP.
    for (const f of (schema.data?.fields ?? []).filter((x) => !x.base && x.editable && !attributeFields.includes(x))) {
      attributes[f.key] = f.kind === 'boolean' ? false : f.kind === 'set' ? [] : null;
    }
    for (const f of attributeFields) {
      const v = form.attributes[f.key];
      if (f.kind === 'number') attributes[f.key] = v === '' || v === null || v === undefined ? null : Number(v);
      else if (f.kind === 'boolean') attributes[f.key] = Boolean(v);
      else if (f.kind === 'set') attributes[f.key] = Array.isArray(v) ? v : [];
      else attributes[f.key] = v === '' || v === undefined ? null : v;
    }
    if (form.category === 'cyberware') attributes.requiresItem = form.requiresItem.trim() || null;
    const num = (s: string) => (s.trim() === '' ? null : Number(s));
    return {
      category: form.category,
      name: form.name,
      slug: form.slug.trim() || null,
      price: num(form.price),
      priceNote: form.priceNote || null,
      priceCategory: form.priceCategory || null,
      manufacturer: form.manufacturer || null,
      source: form.source || null,
      page: num(form.page),
      imageUrl: form.imageUrl || null,
      ruleSystem: form.ruleSystem,
      dataOrigin: form.dataOrigin,
      tags: form.tags.split(',').map((t) => t.trim()).filter(Boolean),
      description: form.description || null,
      notes: form.notes || null,
      attributes,
    };
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setErrors({});
    try {
      const saved = editId ? await api.updateItem(editId, toPayload()) : await api.createItem(toPayload());
      invalidate();
      toast.notify(editId ? `“${saved.name}” atualizado.` : `“${saved.name}” cadastrado.`);
      navigate(`/catalog/${saved.category}?item=${saved.id}`);
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length) {
        setErrors(Object.fromEntries(Object.entries(err.fieldErrors).map(([k, v]) => [k.replace(/^attributes\./, 'attr:'), v[0]])));
        toast.notify('Revise os campos destacados.', 'error');
      } else toast.error(err);
    } finally {
      setSaving(false);
    }
  };

  const onDelete = async () => {
    if (!editId || !window.confirm(`Excluir “${form.name}” do catálogo? Também sai de favoritos e listas.`)) return;
    try {
      await api.deleteItem(editId);
      invalidate();
      toast.notify('Item excluído.');
      onDone();
    } catch (err) {
      toast.error(err);
    }
  };

  const err = (key: string) => errors[key];

  return (
    <form className={styles.editor} onSubmit={onSubmit} noValidate>
      <div className={styles.editorHeader}>
        <h2 className="hud-title">{editId ? `Editar: ${detail.data?.item.name ?? ''}` : 'Novo item'}</h2>
        {editId && <Link to={`/catalog/${form.category}?item=${editId}`} className={styles.small}>ver no catálogo</Link>}
      </div>

      <fieldset className={styles.grid}>
        <legend>Dados gerais</legend>
        <Field label="Categoria" htmlFor="f-category">
          <Select id="f-category" value={form.category} disabled={editId !== null}
            onChange={(e) => setForm({ ...empty(e.target.value), name: form.name, description: form.description })}>
            {categories.data?.map((c) => <option key={c.key} value={c.key}>{c.label}</option>)}
          </Select>
        </Field>
        <Field label="Nome *" error={err('name')} htmlFor="f-name">
          <input id="f-name" value={form.name} onChange={(e) => set('name', e.target.value)} maxLength={150} required />
        </Field>
        <Field label="Identificador (slug)" error={err('slug')} hint="Gerado a partir do nome se vazio." htmlFor="f-slug">
          <input id="f-slug" value={form.slug} onChange={(e) => set('slug', e.target.value)} maxLength={140} />
        </Field>
        <Field label="Fabricante" htmlFor="f-maker">
          <input id="f-maker" value={form.manufacturer} onChange={(e) => set('manufacturer', e.target.value)} maxLength={120} />
        </Field>
        <Field label="Preço (eb)" error={err('price')} htmlFor="f-price">
          <input id="f-price" type="number" min={0} value={form.price} onChange={(e) => set('price', e.target.value)} />
        </Field>
        <Field label="Observação de preço" htmlFor="f-pricenote">
          <input id="f-pricenote" value={form.priceNote} onChange={(e) => set('priceNote', e.target.value)} placeholder="Ex.: 20eb/50eb" maxLength={120} />
        </Field>
        <Field label="Categoria de preço" error={err('priceCategory')} htmlFor="f-pricecat">
          <Select id="f-pricecat" value={form.priceCategory} onChange={(e) => set('priceCategory', e.target.value)}>
            <option value="">—</option>
            {PRICE_CATEGORIES.map((p) => <option key={p} value={p}>{p}</option>)}
          </Select>
        </Field>
        <Field label="Fonte / livro" htmlFor="f-source">
          <input id="f-source" value={form.source} onChange={(e) => set('source', e.target.value)} maxLength={120} />
        </Field>
        <Field label="Página" error={err('page')} htmlFor="f-page">
          <input id="f-page" type="number" min={1} value={form.page} onChange={(e) => set('page', e.target.value)} />
        </Field>
        <Field label="Sistema" htmlFor="f-system">
          <Select id="f-system" value={form.ruleSystem} onChange={(e) => set('ruleSystem', e.target.value)}>
            <option value="CyberpunkRed">Cyberpunk RED</option>
            <option value="Cyberpunk2020">Cyberpunk 2020</option>
          </Select>
        </Field>
        <Field label="Origem dos dados" htmlFor="f-origin" hint="Marque como demonstração tudo que não vem de um livro.">
          <Select id="f-origin" value={form.dataOrigin} onChange={(e) => set('dataOrigin', e.target.value)}>
            <option value="Custom">Cadastro manual</option>
            <option value="Demo">Dados de demonstração</option>
            <option value="Compendium">Compêndio importado</option>
            <option value="Verified">Conferido com PDF oficial</option>
          </Select>
        </Field>
        <Field label="Imagem (URL)" error={err('imageUrl')} htmlFor="f-image">
          <input id="f-image" value={form.imageUrl} onChange={(e) => set('imageUrl', e.target.value)} placeholder="https://..." maxLength={500} />
        </Field>
        <Field label="Propriedades (separadas por vírgula)" error={err('tags')} htmlFor="f-tags">
          <input id="f-tags" value={form.tags} onChange={(e) => set('tags', e.target.value)} placeholder="Smartgun, Qualidade Excelente" />
        </Field>
        <div className={styles.full}>
          <Field label="Descrição" error={err('description')} htmlFor="f-desc">
            <textarea id="f-desc" value={form.description} onChange={(e) => set('description', e.target.value)} maxLength={4000} />
          </Field>
        </div>
        <div className={styles.full}>
          <Field label="Notas" error={err('notes')} htmlFor="f-notes">
            <textarea id="f-notes" value={form.notes} onChange={(e) => set('notes', e.target.value)} maxLength={4000} />
          </Field>
        </div>
      </fieldset>

      {schema.isLoading && <Spinner />}
      {schema.data && (
        <fieldset className={styles.grid}>
          <legend>Atributos de {schema.data.label}</legend>
          {attributeFields.map((f) => (
            <AttributeInput key={f.key} field={f} value={form.attributes[f.key]} error={err(`attr:${f.key}`)} onChange={(v) => setAttr(f.key, v)} />
          ))}
          {form.category === 'cyberware' && (
            <Field label="Requer (peça base)" error={err('attr:requiresItem')} hint="Slug ou nome do cyberware base, ex.: cybereye." htmlFor="f-requires">
              <input id="f-requires" value={form.requiresItem} onChange={(e) => set('requiresItem', e.target.value)} />
            </Field>
          )}
          {Object.entries(errors).filter(([k]) => k.startsWith('attr:') && !attributeFields.some((f) => `attr:${f.key}` === k) && k !== 'attr:requiresItem')
            .map(([k, v]) => <p key={k} className={styles.formError}>{k.slice(5)}: {v}</p>)}
        </fieldset>
      )}

      <div className={styles.formActions}>
        <Button type="submit" variant="primary" icon={saving ? <Spinner /> : <Save size={15} />} disabled={saving || !form.name.trim()}>
          {editId ? 'Salvar alterações' : 'Cadastrar item'}
        </Button>
        <Button onClick={onDone}>Cancelar</Button>
        {editId && <Button variant="danger" icon={<Trash2 size={15} />} onClick={onDelete}>Excluir item</Button>}
      </div>
    </form>
  );
}

function AttributeInput({ field, value, error, onChange }: {
  field: FieldDescriptor; value: AttributeValue | undefined; error?: string; onChange: (value: AttributeValue) => void;
}) {
  const id = `attr-${field.key}`;
  const label = `${field.label}${field.required ? ' *' : ''}${field.unit && field.unit !== 'penalty' ? ` (${field.unit})` : ''}`;

  if (field.kind === 'boolean') {
    return (
      <div className={styles.checkField}>
        <Checkbox checked={Boolean(value)} onChange={onChange} label={field.label} />
        {error && <span className={styles.formError}>{error}</span>}
      </div>
    );
  }
  if (field.kind === 'set') {
    const selected = Array.isArray(value) ? value : [];
    return (
      <div className={styles.full}>
        <Field label={label} error={error}>
          <div className={styles.setGrid}>
            {field.options?.map((o) => (
              <Checkbox key={o.value} label={o.label} checked={selected.includes(o.value)}
                onChange={(checked) => onChange(checked ? [...selected, o.value] : selected.filter((v) => v !== o.value))} />
            ))}
          </div>
        </Field>
      </div>
    );
  }
  if (field.kind === 'enum' && field.options) {
    return (
      <Field label={label} error={error} htmlFor={id}>
        <Select id={id} value={(value as string) ?? ''} onChange={(e) => onChange(e.target.value || null)}>
          {!field.required && <option value="">—</option>}
          {field.required && !value && <option value="">Selecione...</option>}
          {field.options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
        </Select>
      </Field>
    );
  }
  if (field.kind === 'longtext') {
    return (
      <div className={styles.full}>
        <Field label={label} error={error} htmlFor={id}>
          <textarea id={id} value={(value as string) ?? ''} onChange={(e) => onChange(e.target.value)} maxLength={1000} />
        </Field>
      </div>
    );
  }
  return (
    <Field label={label} error={error} hint={field.description ?? undefined} htmlFor={id}>
      <input id={id} type={field.kind === 'number' ? 'number' : 'text'} value={value === null || value === undefined ? '' : String(value)}
        onChange={(e) => onChange(e.target.value)} />
    </Field>
  );
}
