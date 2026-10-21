import { useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Copy, Download, HeartPulse, Plus, Trash2, Upload, UserRound, Users, Wallet } from 'lucide-react';
import {
  characterApi, downloadCharacter, problemMessage, readCharacterFile, useCharacterLibrary, useCharacterRules, useCharacters,
} from '../../api/characters';
import type { CharacterSummary, CreationMethod } from '../../api/characterTypes';
import { Button, ComboBox, EmptyState, ErrorState, Field, IconButton, Modal, Select, Spinner } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import page from '../../styles/Page.module.css';
import { formatDate, languagesFor, METHOD_HINT, METHOD_LABEL, STATUS_LABEL } from './characterLabels';
import { StateTag } from './CharacterUi';
import styles from './Characters.module.css';

export function CharactersPage() {
  const list = useCharacters();
  const [creating, setCreating] = useState(false);

  return (
    <div className={page.page}>
      <header className={styles.row}>
        <div className={styles.grow}>
          <h1 className="hud-title">Personagens</h1>
          <p className={page.lead}>
            Fichas independentes de Cyberpunk RED: crie pelos três métodos oficiais, acompanhe em jogo e mantenha o histórico.
          </p>
        </div>
        <div className={styles.row} style={{ flexWrap: 'nowrap' }}>
          <ImportButton />
          <Button variant="primary" icon={<Plus size={16} />} onClick={() => setCreating(true)}>Novo personagem</Button>
        </div>
      </header>

      {list.isLoading && <Spinner />}
      {list.error && <ErrorState error={list.error} onRetry={() => list.refetch()} />}
      {list.data && list.data.length === 0 && (
        <EmptyState title="Nenhum personagem ainda" icon={<Users size={28} />}
          action={<Button variant="primary" icon={<Plus size={16} />} onClick={() => setCreating(true)}>Criar o primeiro</Button>}>
          Escolha um método de criação e uma Role para começar, ou importe um personagem exportado.
        </EmptyState>
      )}
      {list.data && list.data.length > 0 && (
        <div className={styles.cards}>
          {list.data.map((c) => <CharacterCard key={c.id} character={c} />)}
        </div>
      )}

      <NewCharacterModal open={creating} onClose={() => setCreating(false)} />
    </div>
  );
}

/** Picks an exported file and creates a new character from it; nothing existing is overwritten. */
function ImportButton() {
  const toast = useToast();
  const navigate = useNavigate();
  const { importFile } = useCharacterLibrary();
  const input = useRef<HTMLInputElement>(null);

  const pick = async (file: File | undefined) => {
    if (!file) return;
    let content: unknown;
    try {
      content = await readCharacterFile(file);
    } catch (error) {
      toast.notify((error as Error).message, 'error');
      return;
    }
    importFile.mutate(content, {
      onSuccess: (d) => {
        toast.notify(`${d.sheet.identity.name} importado como personagem novo.`);
        if (d.notices.length > 0) toast.notify(d.notices.join(' '), 'info');
        navigate(`/characters/${d.id}`);
      },
      onError: (e) => toast.notify(problemMessage(e), 'error'),
    });
  };

  return (
    <>
      <input ref={input} type="file" accept=".json,application/json" hidden aria-hidden
        onChange={(e) => { void pick(e.target.files?.[0]); e.target.value = ''; }} />
      <Button icon={<Upload size={16} />} disabled={importFile.isPending} title="Criar um personagem a partir de um arquivo exportado"
        onClick={() => input.current?.click()}>Importar</Button>
    </>
  );
}

function CharacterCard({ character: c }: { character: CharacterSummary }) {
  const toast = useToast();
  const navigate = useNavigate();
  const { duplicate, remove } = useCharacterLibrary();
  const [confirming, setConfirming] = useState(false);
  const target = c.status === 'Creating' ? `/characters/${c.id}/criacao` : `/characters/${c.id}/ficha`;
  const tone = c.status === 'Complete' ? 'ok' : c.status === 'Creating' ? 'warn' : 'bad';

  return (
    <article className={styles.card}>
      {c.hasPortrait
        ? <img className={styles.avatar} src={characterApi.portraitUrl(c.id, c.updatedAt)} alt="" />
        : <div className={styles.avatar}><UserRound size={34} /></div>}
      <div className={styles.cardBody}>
        <Link to={target} className={styles.cardName}>{c.name}{c.handle && <span className={styles.muted}> “{c.handle}”</span>}</Link>
        <div className={styles.cardMeta}>
          <span>{c.role ?? 'Sem Role'}{c.roleRank ? ` · Rank ${c.roleRank}` : ''}</span>
          <span className={styles.muted}>{METHOD_LABEL[c.method]}</span>
        </div>
        <div className={styles.cardMeta}>
          <StateTag tone={tone} title={c.errorCount ? `${c.errorCount} erro(s), ${c.warningCount} aviso(s)` : undefined}>
            {STATUS_LABEL[c.status]}{c.errorCount > 0 ? ` (${c.errorCount})` : ''}
          </StateTag>
          {c.status !== 'Creating' && (
            <>
              <span title="Pontos de vida"><HeartPulse size={13} /> <span className={styles.mono}>{c.currentHp}/{c.maxHp}</span></span>
              <span title="Humanidade" className={styles.mono}>HUM {c.humanity}/{c.humanityMax}</span>
            </>
          )}
          <span title="Saldo"><Wallet size={13} /> <span className={styles.mono}>{formatEb(c.balance)}</span></span>
        </div>
        <div className={styles.cardMeta}><span className={styles.source}>Atualizado {formatDate(c.updatedAt)}</span></div>
      </div>
      <div className={styles.cardActions}>
        <Button onClick={() => navigate(target)}>{c.status === 'Creating' ? 'Continuar criação' : 'Abrir ficha'}</Button>
        <IconButton label="Duplicar como base" onClick={() => duplicate.mutate(c.id, {
          onSuccess: (d) => toast.notify(`Cópia criada: ${d.sheet.identity.name}.`),
          onError: (e) => toast.notify(problemMessage(e), 'error'),
        })}><Copy size={16} /></IconButton>
        <IconButton label="Exportar para arquivo" onClick={() => downloadCharacter(c.id)}><Download size={16} /></IconButton>
        <IconButton label="Excluir" onClick={() => setConfirming(true)}><Trash2 size={16} /></IconButton>
      </div>

      <Modal open={confirming} onClose={() => setConfirming(false)} title="Excluir personagem">
        <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
          <p>Excluir <strong>{c.name}</strong> e todo o histórico? Esta ação não pode ser desfeita.</p>
          <p className={styles.small}>
            Para guardar uma cópia, <button type="button" className={styles.linkButton} onClick={() => downloadCharacter(c.id)}>exporte o personagem</button> antes.
          </p>
          <div className={styles.row}>
            <Button variant="danger" icon={<Trash2 size={16} />} data-autofocus onClick={() => remove.mutate(c.id, {
              onSuccess: () => { setConfirming(false); toast.notify(`${c.name} foi excluído.`); },
              onError: (e) => toast.notify(problemMessage(e), 'error'),
            })}>Excluir definitivamente</Button>
            <Button variant="ghost" onClick={() => setConfirming(false)}>Cancelar</Button>
          </div>
        </div>
      </Modal>
    </article>
  );
}

/** First step of creation: method, Role and identity. The draft is created on the server right away. */
export function NewCharacterModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const rules = useCharacterRules();
  const { create } = useCharacterLibrary();
  const navigate = useNavigate();
  const toast = useToast();
  const [method, setMethod] = useState<CreationMethod>('Streetrat');
  const [role, setRole] = useState('Solo');
  const [name, setName] = useState('');
  const [handle, setHandle] = useState('');
  const [region, setRegion] = useState('');
  const [language, setLanguage] = useState('');
  const [home, setHome] = useState('Watson');
  const [preset, setPreset] = useState<number | null>(null);

  const r = rules.data;
  const languages = r ? languagesFor(r, region) : [];
  const cp = r?.methods.find((m) => m.method === 'CompletePackage');

  const submit = () => create.mutate({
    name, handle: handle || null, method, role, nativeLanguage: language || null, culturalRegion: region || null, home,
    overrides: method === 'CompletePackage' && preset && preset !== cp?.statPoints ? { statPoints: preset, note: 'Escolhido na criação' } : null,
  }, {
    onSuccess: (detail) => {
      onClose();
      toast.notify(`${detail.sheet.identity.name} criado. Siga os passos da criação.`);
      navigate(`/characters/${detail.id}/criacao`);
    },
    onError: (e) => toast.notify(problemMessage(e), 'error'),
  });

  return (
    <Modal open={open} onClose={onClose} title="Novo personagem" wide>
      <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
        {!r ? <Spinner /> : (
          <>
            <div className={styles.methods} role="radiogroup" aria-label="Método de criação">
              {r.methods.map((m) => (
                <button key={m.method} type="button" role="radio" aria-checked={method === m.method}
                  className={`${styles.choice} ${method === m.method ? styles.choiceActive : ''}`} onClick={() => setMethod(m.method)}>
                  <span className={styles.choiceTitle}>{METHOD_LABEL[m.method]}</span>
                  <span className={styles.small}>{METHOD_HINT[m.method]}</span>
                  <span className={styles.source}>{m.summary} · {m.source}</span>
                </button>
              ))}
            </div>

            <div className={styles.formGrid}>
              <Field label="Nome" htmlFor="nc-name">
                <input id="nc-name" className={styles.input} value={name} onChange={(e) => setName(e.target.value)} data-autofocus maxLength={80} />
              </Field>
              <Field label="Handle (nome de rua)" htmlFor="nc-handle">
                <input id="nc-handle" className={styles.input} value={handle} onChange={(e) => setHandle(e.target.value)} maxLength={80} />
              </Field>
              <Field label="Role" htmlFor="nc-role" hint={`Habilidade: ${r.roles.find((x) => x.id === role)?.ability} (Rank 4)`}>
                <Select id="nc-role" value={role} onChange={(e) => setRole(e.target.value)}>
                  {r.roles.map((x) => <option key={x.id} value={x.id}>{x.id}</option>)}
                </Select>
              </Field>
              <Field label="Origem cultural" htmlFor="nc-region" hint="CP:R p.45">
                <Select id="nc-region" value={region} onChange={(e) => { setRegion(e.target.value); setLanguage(''); }}>
                  <option value="">Escolha…</option>
                  {r.culturalOrigins.map((o) => <option key={o.region} value={o.region}>{o.region}</option>)}
                </Select>
              </Field>
              <Field label="Idioma de origem" htmlFor="nc-lang" hint={`Language nível ${r.originLanguageLevel}, sem custo`}>
                <ComboBox id="nc-lang" value={language} onChange={setLanguage} options={languages} placeholder="Escolha ou digite…" />
              </Field>
              <Field label="Bairro (Local Expert)" htmlFor="nc-home">
                <ComboBox id="nc-home" value={home} onChange={setHome} options={r.homes} placeholder="Escolha ou digite…" />
              </Field>
              {method === 'CompletePackage' && cp && (
                <Field label="Pontos de STAT" htmlFor="nc-preset" hint="O livro recomenda o Starting Character (CP:R p.78)">
                  <Select id="nc-preset" value={preset ?? cp.statPoints ?? 62} onChange={(e) => setPreset(Number(e.target.value))}>
                    {cp.statPresets.map((p) => <option key={p.points} value={p.points}>{p.label} ({p.points})</option>)}
                  </Select>
                </Field>
              )}
            </div>

            <div className={styles.row}>
              <Button variant="primary" icon={<Plus size={16} />} disabled={!name.trim() || create.isPending} onClick={submit}>
                Criar e continuar
              </Button>
              <Button variant="ghost" onClick={onClose}>Cancelar</Button>
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}
