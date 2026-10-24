import { useEffect } from 'react';
import { Link, Navigate, NavLink, useNavigate, useParams } from 'react-router';
import { ArrowLeft, Copy, Download } from 'lucide-react';
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

function CharacterWorkspace({ detail, rules, tab }: { detail: CharacterDetail; rules: CharacterRules; tab: CharacterTab }) {
  const editor = useSheetEditor(detail);
  const navigate = useNavigate();
  const toast = useToast();
  const list = useCharacters();
  const { duplicate } = useCharacterLibrary();
  const creating = !detail.sheet.creation.finalized;
  const tone = detail.status === 'Complete' ? 'ok' : detail.status === 'Creating' ? 'warn' : 'bad';
  const identity = editor.draft.identity;

  // Alt+1..4 jump between Personagem, Caminho de Vida, Equipamento and Cyberware; Ctrl+S saves (see useSheetEditor).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (!e.altKey || e.ctrlKey || e.metaKey) return;
      const target = TABS[Number(e.key) - 1];
      if (!target) return;
      e.preventDefault();
      void editor.flush().then(() => navigate(`/characters/${detail.id}/${target.tab}`));
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [detail.id, editor, navigate]);

  const switchTo = async (other: number) => {
    await editor.flush();
    navigate(`/characters/${other}/${tab === 'criacao' ? 'ficha' : tab}`);
  };

  return (
    <div className={page.page}>
      <div className={styles.topbar}>
        <Link to="/characters" className={styles.tab} aria-label="Voltar aos personagens"><ArrowLeft size={16} /></Link>
        <div className={styles.topbarName}>
          <strong>{identity.name || 'Sem nome'}{identity.handle && <span className={styles.muted}> “{identity.handle}”</span>}</strong>
          <span className={styles.source}>{detail.sheet.roles[0]?.role} · {METHOD_LABEL[detail.sheet.creation.method]}</span>
        </div>
        <StateTag tone={tone}>{STATUS_LABEL[detail.status]}</StateTag>

        <nav className={styles.tabs} aria-label="Seções da ficha (Alt+1 a Alt+4)">
          {creating && (
            <NavLink to={`/characters/${detail.id}/criacao`} className={({ isActive }) => `${styles.tab} ${isActive ? styles.tabActive : ''}`}>
              Criação
            </NavLink>
          )}
          {TABS.map((t) => t.ready
            ? (
              <NavLink key={t.tab} to={`/characters/${detail.id}/${t.tab}`} className={({ isActive }) => `${styles.tab} ${isActive ? styles.tabActive : ''}`}>
                {t.label}
              </NavLink>
            )
            : <span key={t.tab} className={`${styles.tab} ${styles.tabDisabled}`} title="Chega nas próximas fases">{t.label}</span>)}
        </nav>

        <div className={styles.topbarRight}>
          <label className="visually-hidden" htmlFor="character-switch">Trocar de personagem</label>
          <Select id="character-switch" value={detail.id} onChange={(e) => void switchTo(Number(e.target.value))}>
            {(list.data ?? [{ id: detail.id, name: identity.name }]).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
          <IconButton label="Duplicar como base" onClick={() => duplicate.mutate(detail.id, {
            onSuccess: (d) => { toast.notify(`Cópia criada: ${d.sheet.identity.name}.`); navigate(`/characters/${d.id}`); },
            onError: (e) => toast.notify(problemMessage(e), 'error'),
          })}><Copy size={16} /></IconButton>
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
