import { useRef, useState } from 'react';
import { useSearchParams } from 'react-router';
import { FileJson, FileSpreadsheet, Plus, Upload } from 'lucide-react';
import { api } from '../../api/client';
import { useCategories, useInvalidateCatalog } from '../../api/hooks';
import type { ImportResult } from '../../api/types';
import { Button, Field, Panel, Select, Spinner } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import page from '../../styles/Page.module.css';
import { ItemEditor } from './ItemEditor';
import styles from './AdminPage.module.css';

export function AdminPage() {
  const [params, setParams] = useSearchParams();
  const editParam = params.get('edit');
  const editId = editParam && editParam !== 'new' ? Number(editParam) : null;
  const editorOpen = editParam !== null;

  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch="Administração">Administração</h1>
        <p className={page.lead}>
          Cadastre, edite e remova itens, ou importe/exporte o catálogo. Os formulários são gerados a partir do mesmo schema
          que alimenta os filtros, e a API valida cada atributo (por exemplo, roupas não aceitam SP).
        </p>
      </header>

      {editorOpen ? (
        <ItemEditor key={editParam} editId={editId} onDone={() => setParams({})} />
      ) : (
        <div className={styles.cards}>
          <Panel className={styles.card}>
            <h2 className="hud-title">Cadastro</h2>
            <p className={styles.small}>Crie um item em qualquer categoria. Para editar, abra a ficha de um item e use “Editar”.</p>
            <Button variant="primary" icon={<Plus size={15} />} onClick={() => setParams({ edit: 'new' })}>Novo item</Button>
          </Panel>
          <ImportExportPanel />
        </div>
      )}
    </div>
  );
}

function ImportExportPanel() {
  const categories = useCategories();
  const [category, setCategory] = useState('');
  const [result, setResult] = useState<ImportResult | null>(null);
  const [importing, setImporting] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);
  const toast = useToast();
  const invalidate = useInvalidateCatalog();

  const onImport = async (file: File) => {
    setImporting(true);
    setResult(null);
    try {
      if (file.size > 10 * 1024 * 1024) throw new Error('Arquivo maior que 10 MB.');
      let rows: unknown;
      try {
        rows = JSON.parse(await file.text());
      } catch {
        throw new Error('O arquivo não é um JSON válido.');
      }
      const res = await api.importItems(rows);
      setResult(res);
      invalidate();
      toast.notify(`Importação: ${res.created} criados, ${res.updated} atualizados, ${res.failed} com erro.`, res.failed ? 'info' : 'success');
    } catch (err) {
      toast.error(err);
    } finally {
      setImporting(false);
      if (fileRef.current) fileRef.current.value = '';
    }
  };

  return (
    <Panel className={styles.card}>
      <h2 className="hud-title">Importar / Exportar</h2>
      <p className={styles.small}>
        O JSON exportado é o mesmo formato aceito na importação (itens são atualizados pelo identificador/slug).
        O CSV é por categoria, com valores legíveis, para planilhas.
      </p>
      <Field label="Categoria" htmlFor="io-category">
        <Select id="io-category" value={category} onChange={(e) => setCategory(e.target.value)}>
          <option value="">Catálogo inteiro</option>
          {categories.data?.map((c) => <option key={c.key} value={c.key}>{c.label} ({c.itemCount})</option>)}
        </Select>
      </Field>
      <div className={styles.row}>
        <a className={styles.download} href={api.exportUrl(category || null, 'json')} download>
          <FileJson size={15} /> Exportar JSON
        </a>
        <a className={styles.download} href={category ? api.exportUrl(category, 'csv') : undefined} download
          aria-disabled={!category} onClick={(e) => { if (!category) { e.preventDefault(); toast.notify('Escolha uma categoria para exportar CSV.', 'info'); } }}>
          <FileSpreadsheet size={15} /> Exportar CSV
        </a>
      </div>
      <div className={styles.row}>
        <input ref={fileRef} type="file" accept="application/json,.json" className="visually-hidden" id="import-file"
          onChange={(e) => e.target.files?.[0] && onImport(e.target.files[0])} />
        <Button icon={importing ? <Spinner /> : <Upload size={15} />} disabled={importing} onClick={() => fileRef.current?.click()}>
          Importar JSON
        </Button>
      </div>
      {result && (
        <div className={styles.importResult} role="status">
          <p><strong>{result.created}</strong> criados · <strong>{result.updated}</strong> atualizados · <strong>{result.failed}</strong> com erro</p>
          {result.errors.length > 0 && (
            <ul>
              {result.errors.slice(0, 50).map((e) => (
                <li key={e.index}>
                  <span className="mono">#{e.index}</span> {e.name ?? e.slug}: {Object.entries(e.errors).map(([k, v]) => `${k}: ${v.join(', ')}`).join(' | ')}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Panel>
  );
}
