import { useEffect } from 'react';
import { Link, Navigate, NavLink, useNavigate, useParams } from 'react-router';
import { ArrowLeft, Copy, Download, ExternalLink } from 'lucide-react';
import { downloadCharacter, problemMessage, useCharacter, useCharacterLibrary, useCharacterRules, useCharacters } from '../../api/characters';
import type { CharacterDetail, CharacterRules } from '../../api/characterTypes';
import { ErrorState, IconButton, Select, Spinner } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import page from '../../styles/Page.module.css';
import { METHOD_LABEL, STATUS_LABEL } from './characterLabels';
import { CreationView } from './CreationView';
import { CyberwareView } from './CyberwareView';
import { EquipmentView } from './EquipmentView';
import { LifepathView } from './LifepathView';
import { SaveIndicator, StateTag } from './CharacterUi';
import { SheetView } from './SheetView';
import { useSheetEditor } from './useSheetEditor';
import styles from './Characters.module.css';

export type CharacterTab = 'criacao' | 'ficha' | 'caminho' | 'equipamento' | 'cyberware';

const TABS: { tab: CharacterTab; label: string; ready: boolean }[] = [
  { tab: 'ficha', label: 'Personagem', ready: true },
  { tab: 'caminho', label: 'Caminho de Vida', ready: true },
  { tab: 'equipamento', label: 'Equipamento', ready: true },
  { tab: 'cyberware', label: 'Cyberware', ready: true },
];

export function CharacterPage() {
  const { id: rawId, tab } = useParams();
  const id = Number(rawId);
  const character = useCharacter(id);
  const rules = useCharacterRules();

  if (!Number.isInteger(id) || id <= 0) return <Navigate to="/characters" replace />;
  if (character.isLoading || rules.isLoading) return <div className={page.page}><Spinner /></div>;
  if (character.error || !character.data) return <div className={page.page}><ErrorState error={character.error} onRetry={() => character.refetch()} /></div>;
  if (rules.error || !rules.data) return <div className={page.page}><ErrorState error={rules.error} onRetry={() => rules.refetch()} /></div>;

  const creating = !character.data.sheet.creation.finalized;
  if (!tab || (tab === 'criacao' && !creating)) return <Navigate to={`/characters/${id}/${creating ? 'criacao' : 'ficha'}`} replace />;
  // A different character keeps its own editor: the key resets local state on switch, so nothing is mixed.
  return <CharacterWorkspace key={id} detail={character.data} rules={rules.data} tab={tab as CharacterTab} />;
}

/**
 * The full character sheet inside another page (a tab of the combat table). The section tabs switch through onTab
 * instead of the URL, so the host page stays where it is. onVersion reports each new sheet version to the host.
 */
export function EmbeddedCharacter({ id, tab, onTab, onVersion }: {
  id: number; tab: CharacterTab; onTab: (tab: CharacterTab) => void; onVersion?: (version: number) => void;
}) {
  const character = useCharacter(id);
  const rules = useCharacterRules();
  const version = character.data?.version;
  useEffect(() => { if (version != null) onVersion?.(version); }, [version, onVersion]);

  if (character.isLoading || rules.isLoading) return <Spinner />;
  if (character.error || !character.data) return <ErrorState error={character.error} onRetry={() => character.refetch()} />;
  if (rules.error || !rules.data) return <ErrorState error={rules.error} onRetry={() => rules.refetch()} />;
  const creating = !character.data.sheet.creation.finalized;
  const shown = tab === 'criacao' && !creating ? 'ficha' : tab;
  return <CharacterWorkspace key={id} detail={character.data} rules={rules.data} tab={shown} onTab={onTab} />;
}

function CharacterWorkspace({ detail, rules, tab, onTab }: {
  detail: CharacterDetail; rules: CharacterRules; tab: CharacterTab; onTab?: (tab: CharacterTab) => void;
}) {
  const editor = useSheetEditor(detail);
  const navigate = useNavigate();
  const toast = useToast();
  const list = useCharacters();
  const { duplicate } = useCharacterLibrary();
  const creating = !detail.sheet.creation.finalized;
  const tone = detail.status === 'Complete' ? 'ok' : detail.status === 'Creating' ? 'warn' : 'bad';
  const identity = editor.draft.identity;
  const embedded = onTab != null;

  // Alt+1..4 jump between Personagem, Caminho de Vida, Equipamento and Cyberware; Ctrl+S saves (see useSheetEditor).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (!e.altKey || e.ctrlKey || e.metaKey) return;
      const target = TABS[Number(e.key) - 1];
      if (!target) return;
      e.preventDefault();
      void editor.flush().then(() => (onTab ? onTab(target.tab) : navigate(`/characters/${detail.id}/${target.tab}`)));
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [detail.id, editor, navigate, onTab]);

  const switchTo = async (other: number) => {
    await editor.flush();
    navigate(`/characters/${other}/${tab === 'criacao' ? 'ficha' : tab}`);
  };

  const tabLink = (to: CharacterTab, label: string) => embedded
    ? (
      <button key={to} type="button" className={`${styles.tab} ${tab === to ? styles.tabActive : ''}`} aria-current={tab === to ? 'page' : undefined}
        onClick={() => void editor.flush().then(() => onTab(to))}>
        {label}
      </button>
    )
    : (
      <NavLink key={to} to={`/characters/${detail.id}/${to}`} className={({ isActive }) => `${styles.tab} ${isActive ? styles.tabActive : ''}`}>
        {label}
      </NavLink>
    );

  return (
    <div className={embedded ? styles.embedded : page.page}>
      <div className={styles.topbar}>
        {!embedded && <Link to="/characters" className={styles.tab} aria-label="Voltar aos personagens"><ArrowLeft size={16} /></Link>}
        <div className={styles.topbarName}>
          <strong>{identity.name || 'Sem nome'}{identity.handle && <span className={styles.muted}> “{identity.handle}”</span>}</strong>
          <span className={styles.source}>{detail.sheet.roles[0]?.role} · {METHOD_LABEL[detail.sheet.creation.method]}</span>
        </div>
        <StateTag tone={tone}>{STATUS_LABEL[detail.status]}</StateTag>

        <nav className={styles.tabs} aria-label="Seções da ficha (Alt+1 a Alt+4)">
          {creating && tabLink('criacao', 'Criação')}
          {TABS.map((t) => t.ready
            ? tabLink(t.tab, t.label)
            : <span key={t.tab} className={`${styles.tab} ${styles.tabDisabled}`} title="Chega nas próximas fases">{t.label}</span>)}
        </nav>

        <div className={styles.topbarRight}>
          {embedded ? (
            <Link to={`/characters/${detail.id}/${tab}`} className={styles.tab} title="Abrir a ficha fora da mesa">
              <ExternalLink size={14} /> Página da ficha
            </Link>
          ) : (
            <>
              <label className="visually-hidden" htmlFor="character-switch">Trocar de personagem</label>
              <Select id="character-switch" value={detail.id} onChange={(e) => void switchTo(Number(e.target.value))}>
                {(list.data ?? [{ id: detail.id, name: identity.name }]).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </Select>
              <IconButton label="Duplicar como base" onClick={() => duplicate.mutate(detail.id, {
                onSuccess: (d) => { toast.notify(`Cópia criada: ${d.sheet.identity.name}.`); navigate(`/characters/${d.id}`); },
                onError: (e) => toast.notify(problemMessage(e), 'error'),
              })}><Copy size={16} /></IconButton>
            </>
          )}
          <IconButton label="Exportar para arquivo" onClick={() => void editor.flush().then(() => downloadCharacter(detail.id))}>
            <Download size={16} />
          </IconButton>
          <SaveIndicator status={editor.status} />
        </div>
      </div>

      {tab === 'criacao' && creating && <CreationView detail={detail} rules={rules} editor={editor} />}
      {tab === 'ficha' && <SheetView detail={detail} rules={rules} editor={editor} />}
      {tab === 'caminho' && <LifepathView detail={detail} rules={rules} editor={editor} />}
      {tab === 'equipamento' && <EquipmentView detail={detail} rules={rules} editor={editor} />}
      {tab === 'cyberware' && <CyberwareView detail={detail} rules={rules} editor={editor} />}
    </div>
  );
}
