import { useRef, useState } from 'react';
import { Link } from 'react-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowDown, ArrowUp, Copy, Download, ExternalLink, Plus, Power, Radio, RotateCcw, RotateCw, SkipForward, Swords, Trash2, Upload,
} from 'lucide-react';
import { problemMessage, useCharacters } from '../../api/characters';
import { useEncounterCommand, useEncounterRules, useEncounters } from '../../api/encounters';
import type { EncounterDetail } from '../../api/encounterTypes';
import { tableApi, tableKeys } from '../../api/table';
import type {
  SceneGrid, SceneSummary, SceneTemplate, SceneView, Seat, TemplateShapeName, TokenInput, TokenView, TokenVisibility, TunnelStatus,
} from '../../api/tableTypes';
import { Badge, Button, Checkbox, Field, IconButton, Segmented, Select, Spinner, Switch } from '../../components/ui/ui';
import { MAP_LIMITS, prepareImage } from '../../lib/portrait';
import { useToast } from '../../state/ToastContext';
import { t, tv } from '../../i18n';
import { WOUND_LABEL, WOUND_TONE } from '../combat/combat';
import { AddCombatantsDialog } from '../combat/AddCombatantsDialog';
import type { Run } from '../combat/EncounterView';
import { NewFight } from './NewFight';
import { BarrierControls, BarrierStock } from './BarrierStock';
import { NumField } from './NumField';
import { ArmorControl } from './ArmorControl';
import { damageFormula } from './tokens';
import { SourcePicker } from './SourcePicker';
import { useTokenSources } from './useTokenSources';
import { sourceFields, tokenInputOf, type TokenSource } from './sources';
import { DEFAULT_GRID, pxToCell } from './grid';
import { SHAPE_LABEL } from './templates';
import styles from './Table.module.css';
import { AwarenessBlock } from './AwarenessBlock';

export type Act = (fn: () => Promise<SceneView | void>, done?: string) => Promise<void>;

const VISIBILITY_LABEL: Record<TokenVisibility, string> = {
  All: t('Todos veem', 'Everyone sees it'),
  Gm: t('Só o mestre', 'GM only'),
  Seats: t('Jogadores escolhidos', 'Chosen players'),
};


/** The scene's settings as the update request wants them, with some fields replaced. */
function sceneUpdate(scene: SceneView, change: Partial<{ name: string; grid: SceneGrid; backgroundMediaId: string | null; backgroundWidth: number; backgroundHeight: number; encounterId: number | null }>) {
  return {
    version: scene.version, name: scene.name, grid: scene.grid, backgroundMediaId: scene.background.mediaId,
    backgroundWidth: scene.background.width, backgroundHeight: scene.background.height, encounterId: scene.encounterId, ...change,
  };
}

/** A button that opens the file picker for an image. */
function ImagePicker({ label, busy, onPick }: { label: string; busy?: boolean; onPick: (file: File | undefined) => void }) {
  const input = useRef<HTMLInputElement>(null);
  return (
    <>
      <input ref={input} type="file" accept="image/png,image/jpeg,image/webp,image/gif" hidden
        onChange={(e) => { onPick(e.target.files?.[0]); e.target.value = ''; }} />
      <Button icon={busy ? <Spinner /> : <Upload size={15} />} disabled={busy} onClick={() => input.current?.click()}>{label}</Button>
    </>
  );
}

/** Uploads an image picked in a file input, shrunk for the table; returns null when the GM cancels. */
async function pickAndUpload(file: File | undefined) {
  if (!file) return null;
  const prepared = await prepareImage(file, MAP_LIMITS);
  const media = await tableApi.upload(prepared.file);
  return { ...media, width: prepared.width, height: prepared.height };
}

// ------------------------------------------------------------------ scenes

export function ScenesColumn({ scenes, currentId, onSelect, act, className }: {
  scenes: SceneSummary[]; currentId: number | null; onSelect: (id: number) => void; act: Act; className?: string;
}) {
  const [name, setName] = useState('');
  const move = (index: number, delta: number) => {
    const ids = scenes.map((s) => s.id);
    const [id] = ids.splice(index, 1);
    ids.splice(index + delta, 0, id!);
    return act(async () => { await tableApi.reorderScenes(ids); });
  };
  return (
    <aside className={`${styles.column} ${className ?? ''}`}>
      <h2 className={styles.title}>{t('Cenas', 'Scenes')}</h2>
      <form className={styles.row} onSubmit={(e) => {
        e.preventDefault();
        if (!name.trim()) return;
        void act(async () => { const s = await tableApi.createScene(name.trim()); setName(''); onSelect(s.id); return s; });
      }}>
        <input className={styles.grow} aria-label={t('Nome da cena', 'Scene name')} value={name} maxLength={80}
          placeholder={t('Ex.: Afterlife', 'E.g. Afterlife')} onChange={(e) => setName(e.target.value)} />
        <IconButton label={t('Criar cena', 'Create scene')} type="submit"><Plus size={16} /></IconButton>
      </form>
      {scenes.length === 0 && <span className={styles.muted}>{t('Crie a primeira cena: um mapa de combate, um bar, uma rua.', 'Create the first scene: a combat map, a bar, a street.')}</span>}
      <div className={styles.sceneList}>
        {scenes.map((s, i) => (
          <div key={s.id} role="button" tabIndex={0} onClick={() => onSelect(s.id)} onKeyDown={(e) => e.key === 'Enter' && onSelect(s.id)}
            className={`${styles.sceneItem} ${s.id === currentId ? styles.sceneSelected : ''} ${s.active ? styles.sceneLive : ''}`}>
            <span className={styles.sceneName}>{s.name}</span>
            <span className={styles.rowBetween}>
              <span className={styles.row}>
                {s.active ? <Badge tone="red">{t('Ao vivo', 'Live')}</Badge> : <span className={styles.source}>{s.tokens} tokens</span>}
              </span>
              <span className={styles.row} onClick={(e) => e.stopPropagation()}>
                {!s.active && <IconButton label={t('Mostrar aos jogadores', 'Show to the players')} onClick={() => act(() => tableApi.activateScene(s.id), t('Cena ao vivo.', 'Scene is live.'))}><Radio size={14} /></IconButton>}
                <IconButton label={t('Subir', 'Move up')} disabled={i === 0} onClick={() => move(i, -1)}><ArrowUp size={14} /></IconButton>
                <IconButton label={t('Descer', 'Move down')} disabled={i === scenes.length - 1} onClick={() => move(i, 1)}><ArrowDown size={14} /></IconButton>
                <IconButton label={t('Duplicar', 'Duplicate')} onClick={() => act(() => tableApi.duplicateScene(s.id))}><Copy size={14} /></IconButton>
                <IconButton label={t('Apagar', 'Delete')} onClick={() => {
                  if (window.confirm(t(`Apagar a cena "${s.name}"?`, `Delete the scene "${s.name}"?`))) void act(() => tableApi.deleteScene(s.id));
                }}><Trash2 size={14} /></IconButton>
              </span>
            </span>
          </div>
        ))}
      </div>
    </aside>
  );
}

// ------------------------------------------------------------------ scene settings

/** Mounted per scene version (key), so a saved change starts a fresh form. */
export function SceneTab({ scene, act, onGridDraft }: { scene: SceneView; act: Act; onGridDraft: (grid: SceneGrid | null) => void }) {
  const toast = useToast();
  const encounters = useEncounters();
  const sources = useTokenSources();
  const [name, setName] = useState(scene.name);
  const [grid, setGrid] = useState<SceneGrid>(scene.grid);
  const [uploading, setUploading] = useState(false);
  const [token, setToken] = useState({ label: '', color: '#00f0ff', size: 1, source: null as TokenSource | null, hidden: false });

  const editGrid = (change: Partial<SceneGrid>) => { const next = { ...grid, ...change }; setGrid(next); onGridDraft(next); };
  const gridChanged = JSON.stringify(grid) !== JSON.stringify(scene.grid);

  const uploadMap = async (file: File | undefined) => {
    setUploading(true);
    try {
      const media = await pickAndUpload(file);
      if (media) await act(() => tableApi.updateScene(scene.id, sceneUpdate(scene, { backgroundMediaId: media.id, backgroundWidth: media.width, backgroundHeight: media.height })), t('Mapa trocado.', 'Map replaced.'));
    } catch (e) {
      toast.notify(problemMessage(e), 'error');
    } finally {
      setUploading(false);
    }
  };

  const center = () => {
    const g = scene.grid;
    const w = scene.background.width || g.offsetX + 30 * g.sizePx;
    const h = scene.background.height || g.offsetY + 20 * g.sizePx;
    const c = pxToCell(g, w / 2, h / 2);
    return { x: Math.max(0, Math.floor(c.x)), y: Math.max(0, Math.floor(c.y)) };
  };

  return (
    <div className={styles.stack}>
      <form className={styles.row} onSubmit={(e) => { e.preventDefault(); void act(() => tableApi.updateScene(scene.id, sceneUpdate(scene, { name }))); }}>
        <input className={styles.grow} aria-label={t('Nome da cena', 'Scene name')} value={name} maxLength={80} onChange={(e) => setName(e.target.value)} />
        <Button type="submit" disabled={name.trim() === scene.name || !name.trim()}>{t('Renomear', 'Rename')}</Button>
      </form>

      <div className={styles.section}>
        <span className={styles.label}>{t('Mapa', 'Map')}</span>
        <div className={styles.row}>
          <ImagePicker busy={uploading} onPick={(file) => void uploadMap(file)}
            label={scene.background.url ? t('Trocar imagem', 'Replace image') : t('Enviar imagem', 'Upload image')} />
          {scene.background.url && <Button variant="ghost" onClick={() => act(() => tableApi.updateScene(scene.id, sceneUpdate(scene, { backgroundMediaId: null, backgroundWidth: 0, backgroundHeight: 0 })))}>{t('Remover', 'Remove')}</Button>}
        </div>
        {scene.background.url && <span className={styles.source}>{scene.background.width} × {scene.background.height} px</span>}
        <span className={styles.muted}>{t('PNG, JPEG, WebP ou GIF. Imagens grandes são reduzidas para 4096 px no navegador.', 'PNG, JPEG, WebP or GIF. Big images are shrunk to 4096 px in the browser.')}</span>
      </div>

      <div className={styles.section}>
        <span className={styles.label}>{t('Grade', 'Grid')}</span>
        <Segmented ariaLabel={t('Tipo de grade', 'Grid type')} size="sm" value={grid.type}
          options={[{ value: 'Square', label: t('Quadrada', 'Square') }, { value: 'None', label: t('Sem grade', 'No grid') }]}
          onChange={(type) => editGrid({ type })} />
        <div className={styles.row}>
          <Field label={t('Célula (px)', 'Cell (px)')}><NumField label={t('Tamanho da célula em pixels', 'Cell size in pixels')} value={grid.sizePx} min={8} max={1000} step={0.5} onChange={(sizePx) => editGrid({ sizePx })} /></Field>
          <Field label={t('Metros/célula', 'Meters/cell')}><NumField label={t('Metros por célula', 'Meters per cell')} value={grid.metersPerCell} min={0.1} max={100} step={0.5} onChange={(metersPerCell) => editGrid({ metersPerCell })} /></Field>
        </div>
        <div className={styles.row}>
          <Field label={t('Deslocar X', 'Offset X')}><NumField label={t('Deslocamento horizontal', 'Horizontal offset')} value={grid.offsetX} min={-1000} max={1000} step={0.5} onChange={(offsetX) => editGrid({ offsetX })} /></Field>
          <Field label={t('Deslocar Y', 'Offset Y')}><NumField label={t('Deslocamento vertical', 'Vertical offset')} value={grid.offsetY} min={-1000} max={1000} step={0.5} onChange={(offsetY) => editGrid({ offsetY })} /></Field>
        </div>
        <div className={styles.row}>
          <input type="color" aria-label={t('Cor da grade', 'Grid color')} value={grid.color} onChange={(e) => editGrid({ color: e.target.value })} />
          <input type="range" aria-label={t('Opacidade da grade', 'Grid opacity')} min={0} max={1} step={0.05} value={grid.opacity} onChange={(e) => editGrid({ opacity: Number(e.target.value) })} />
          <Switch checked={grid.snap} onChange={(snap) => editGrid({ snap })} label={t('Encaixar', 'Snap')} />
        </div>
        <span className={styles.muted}>{t('Ajuste a célula e o deslocamento até a grade bater com a do mapa. No Cyberpunk RED, cada quadrado tem 2 m (CP:R p.168).', 'Adjust the cell and offset until the grid matches the map\'s. In Cyberpunk RED each square is 2 m (CP:R p.168).')}</span>
        <div className={styles.row}>
          <Button variant="primary" disabled={!gridChanged} onClick={() => act(() => tableApi.updateScene(scene.id, sceneUpdate(scene, { grid })), t('Grade salva.', 'Grid saved.'))}>{t('Salvar grade', 'Save grid')}</Button>
          {gridChanged && <Button variant="ghost" onClick={() => { setGrid(scene.grid); onGridDraft(null); }}>{t('Desfazer', 'Undo')}</Button>}
          <Button variant="ghost" onClick={() => editGrid({ ...DEFAULT_GRID })}>{t('Padrão', 'Default')}</Button>
        </div>
      </div>

      <div className={styles.section}>
        <span className={styles.label}>{t('Combate vinculado', 'Linked fight')}</span>
        <Select aria-label={t('Combate vinculado', 'Linked fight')} value={scene.encounterId ?? ''}
          onChange={(e) => act(() => tableApi.updateScene(scene.id, sceneUpdate(scene, { encounterId: e.target.value ? Number(e.target.value) : null })))}>
          <option value="">{t('Nenhum', 'None')}</option>
          {(encounters.data ?? []).map((e) => <option key={e.id} value={e.id}>{tv(e.name)}</option>)}
        </Select>
        {scene.encounterId && (
          <div className={styles.row}>
            <Button icon={<Swords size={15} />} onClick={() => act(() => tableApi.addEncounterTokens(scene.id), t('Tokens criados.', 'Tokens created.'))}>{t('Criar tokens dos combatentes', 'Create the fighters\' tokens')}</Button>
            <Link to={`/combat/${scene.encounterId}`} target="_blank" className={styles.link}>{t('Abrir o combate', 'Open the fight')} <ExternalLink size={12} /></Link>
          </div>
        )}
        <span className={styles.muted}>{t('Monte o combate antes (Friday Night Firefight), crie os tokens e arraste-os para as posições iniciais: a cena guarda tudo. Os jogadores só veem inimigos que têm token visível; numa cena já ao vivo, os tokens de inimigos nascem ocultos (revele-os na aba Seleção).', 'Build the fight first (Friday Night Firefight), create the tokens and drag them to their starting spots: the scene keeps it all. Players only see enemies with a visible token; on a scene that is already live, enemy tokens start hidden (reveal them in the Selection tab).')}</span>
      </div>

      <form className={styles.section} onSubmit={(e) => {
        e.preventDefault();
        const c = center();
        const input: TokenInput = { label: token.label.trim(), x: c.x, y: c.y, width: token.size, height: token.size, color: token.color, ...sourceFields(token.source), visibility: token.hidden ? 'Gm' : 'All' };
        void act(async () => { const v = await tableApi.addToken(scene.id, input); setToken((tk) => ({ ...tk, label: '' })); return v; });
      }}>
        <span className={styles.label}>{t('Novo token', 'New token')}</span>
        <div className={styles.row}>
          <input className={styles.grow} aria-label={t('Nome do token', 'Token name')} value={token.label} maxLength={40} placeholder={t('Ex.: Guarda da Arasaka', 'E.g. Arasaka guard')}
            onChange={(e) => setToken({ ...token, label: e.target.value })} />
          <input type="color" aria-label={t('Cor', 'Color')} value={token.color} onChange={(e) => setToken({ ...token, color: e.target.value })} />
        </div>
        <div className={styles.row}>
          <Select aria-label={t('Tamanho', 'Size')} value={token.size} onChange={(e) => setToken({ ...token, size: Number(e.target.value) })}>
            {[0.5, 1, 2, 3, 4].map((n) => <option key={n} value={n}>{n} × {n}</option>)}
          </Select>
          <Checkbox checked={token.hidden} onChange={(hidden) => setToken({ ...token, hidden })} label={t('Oculto', 'Hidden')} />
        </div>
        <SourcePicker sources={sources} value={sourceFields(token.source)}
          onChange={(source) => setToken({ ...token, source, label: token.label || (source?.name.slice(0, 40) ?? '') })} />
        <span className={styles.muted}>{t('Vincule a um PC, a um NPC salvo ou a um NPC do compêndio: o token usa a imagem dele.', 'Link it to a PC, a saved NPC or a compendium NPC: the token uses its image.')}</span>
        <Button type="submit" icon={<Plus size={15} />} disabled={!token.label.trim()}>{t('Pôr no mapa', 'Put on the map')}</Button>
      </form>

      <BarrierStock scene={scene} act={act} at={center} />
    </div>
  );
}

// ------------------------------------------------------------------ token and template inspector

/** Mounted per token (key). */
export function TokenInspector({ scene, token, seats, encounter, act, onDone }: {
  scene: SceneView; token: TokenView; seats: Seat[]; encounter: EncounterDetail | undefined; act: Act; onDone: () => void;
}) {
  const toast = useToast();
  const sources = useTokenSources();
  const [form, setForm] = useState<TokenInput>(() => tokenInputOf(token));
  const fighter = token.combatantId ? encounter?.queue.find((f) => f.id === token.combatantId) : undefined;

  // Position comes from the board, never from this form.
  const save = (change: Partial<TokenInput> = {}) =>
    act(() => tableApi.updateToken(scene.id, token.id, { ...form, ...change, x: token.x, y: token.y }), t('Token salvo.', 'Token saved.'));
  const openSeats = seats.filter((s) => !s.revoked);

  return (
    <div className={styles.stack}>
      <div className={styles.row}>
        <input className={styles.grow} aria-label={t('Nome', 'Name')} value={form.label} maxLength={40} onChange={(e) => setForm({ ...form, label: e.target.value })} />
        <input type="color" aria-label={t('Cor', 'Color')} value={form.color ?? '#00f0ff'} onChange={(e) => setForm({ ...form, color: e.target.value })} />
      </div>
      {token.characterId != null && <AwarenessBlock characterId={token.characterId} roundFlags={fighter?.roundFlags} />}
      <div className={styles.row}>
        <Field label={t('Largura', 'Width')}><NumField label={t('Largura em células', 'Width in cells')} value={form.width ?? 1} min={0.25} max={20} step={0.25} onChange={(width) => setForm({ ...form, width })} /></Field>
        <Field label={t('Altura', 'Height')}><NumField label={t('Altura em células', 'Height in cells')} value={form.height ?? 1} min={0.25} max={20} step={0.25} onChange={(height) => setForm({ ...form, height })} /></Field>
        <Field label={t('Giro (°)', 'Turn (°)')}>
          <span className={styles.row}>
            <IconButton label={t('Girar −45°', 'Turn −45°')} onClick={() => save({ rotation: ((form.rotation ?? 0) + 315) % 360 }).then(() => setForm((f) => ({ ...f, rotation: ((f.rotation ?? 0) + 315) % 360 })))}><RotateCcw size={14} /></IconButton>
            <IconButton label={t('Girar +45°', 'Turn +45°')} onClick={() => save({ rotation: ((form.rotation ?? 0) + 45) % 360 }).then(() => setForm((f) => ({ ...f, rotation: ((f.rotation ?? 0) + 45) % 360 })))}><RotateCw size={14} /></IconButton>
          </span>
        </Field>
      </div>
      <Switch checked={form.locked ?? false} onChange={(locked) => setForm({ ...form, locked })} label={t('Travado (ninguém arrasta)', 'Locked (nobody drags it)')} />
      {token.barrier ? <BarrierControls scene={scene} token={token} act={act} /> : (
        <Switch checked={form.layer === 'Scenery'} onChange={(on) => setForm({ ...form, layer: on ? 'Scenery' : 'Token' })}
          label={t('Cenário (fica sob os personagens)', 'Scenery (sits under the characters)')} />
      )}

      <div className={styles.section}>
        <span className={styles.label}>{t('Quem vê e quem move', 'Who sees and moves it')}</span>
        <Select aria-label={t('Visibilidade', 'Visibility')} value={form.visibility ?? 'All'} onChange={(e) => setForm({ ...form, visibility: e.target.value as TokenVisibility })}>
          {(['All', 'Gm', 'Seats'] as TokenVisibility[]).map((v) => <option key={v} value={v}>{VISIBILITY_LABEL[v]}</option>)}
        </Select>
        {form.visibility === 'Seats' && openSeats.map((s) => (
          <Checkbox key={s.id} label={s.label} checked={(form.visibleSeatIds ?? []).includes(s.id)}
            onChange={(on) => setForm({ ...form, visibleSeatIds: on ? [...(form.visibleSeatIds ?? []), s.id] : (form.visibleSeatIds ?? []).filter((x) => x !== s.id) })} />
        ))}
        <Select aria-label={t('Dono', 'Owner')} value={form.ownerSeatId ?? ''} onChange={(e) => setForm({ ...form, ownerSeatId: e.target.value ? Number(e.target.value) : null })}>
          <option value="">{t('Sem dono (só o mestre move)', 'No owner (only the GM moves it)')}</option>
          {openSeats.map((s) => <option key={s.id} value={s.id}>{t(`Movido por ${s.label}`, `Moved by ${s.label}`)}</option>)}
        </Select>
        <span className={styles.muted}>{t('O jogador do personagem vinculado também move o token.', 'The linked character\'s player moves the token too.')}</span>
      </div>

      <div className={styles.section}>
        <span className={styles.label}>{t('Vínculos', 'Links')}</span>
        <SourcePicker sources={sources} value={form} onChange={(source) => setForm({ ...form, ...sourceFields(source) })} />
        {encounter && (
          <Select aria-label={t('Combatente', 'Fighter')} value={form.combatantId ?? ''} onChange={(e) => setForm({ ...form, combatantId: e.target.value || null })}>
            <option value="">{t('Fora do combate', 'Not in the fight')}</option>
            {encounter.queue.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
          </Select>
        )}
        <Field label={t('Anel de alcance (m)', 'Range ring (m)')}>
          <NumField label={t('Anel de alcance em metros', 'Range ring in meters')} value={form.rangeRingMeters ?? 0} min={0} max={1000} step={1}
            onChange={(n) => setForm({ ...form, rangeRingMeters: n > 0 ? n : null })} />
        </Field>
        <div className={styles.row}>
          <ImagePicker label={t('Imagem', 'Image')} onPick={async (file) => {
            try {
              const media = await pickAndUpload(file);
              if (media) { setForm((f) => ({ ...f, imageMediaId: media.id })); await save({ imageMediaId: media.id }); }
            } catch (err) { toast.notify(problemMessage(err), 'error'); }
          }} />
          {form.imageMediaId && <Button variant="ghost" onClick={() => { setForm({ ...form, imageMediaId: null }); void save({ imageMediaId: null }); }}>{t('Tirar imagem', 'Drop image')}</Button>}
        </div>
      </div>

      {fighter && encounter && <FighterQuick fighter={fighter} encounterId={encounter.id} />}

      <div className={styles.rowBetween}>
        <Button variant="primary" onClick={() => save()}>{t('Salvar', 'Save')}</Button>
        <Button variant="danger" icon={<Trash2 size={15} />} onClick={() => act(async () => { const v = await tableApi.removeToken(scene.id, token.id); onDone(); return v; })}>{t('Remover', 'Remove')}</Button>
      </div>
    </div>
  );
}

/**
 * The fighter behind a token, for the GM: armor of head and body with +/− (a PC's lands on their sheet), and the
 * weapons with Attack and Damage rolled in the chat.
 */
function FighterQuick({ fighter, encounterId }: { fighter: EncounterDetail['queue'][number]; encounterId: number }) {
  const toast = useToast();
  const command = useEncounterCommand(encounterId);
  const [secret, setSecret] = useState(false);
  const roll = (formula: string, kind: 'Check' | 'Damage', label: string) =>
    tableApi.roll({ formula, kind, label: `${fighter.name}: ${label}`, visibility: secret ? 'Secret' : 'Public' }).catch((e) => toast.notify(problemMessage(e), 'error'));
  const signed = (n: number) => (n >= 0 ? `+${n}` : `${n}`);
  return (
    <div className={styles.section}>
      <span className={styles.label}>{t('Combatente', 'Fighter')}</span>
      <span className={styles.row}>
        <span className={styles.stat}>{fighter.hp}/{fighter.maxHp} HP</span>
        <Badge tone={WOUND_TONE[fighter.woundState]}>{fighter.dead ? t('Morto', 'Dead') : WOUND_LABEL[fighter.woundState]}</Badge>
      </span>
      <ArmorControl armor={fighter} busy={command.isPending}
        onAdjust={(location, delta) => command.mutate({ path: `combatants/${fighter.id}/armor`, body: { location, delta } }, { onError: (e) => toast.notify(problemMessage(e), 'error') })} />
      {fighter.weapons.length > 0 && (
        <>
          <Switch checked={secret} onChange={setSecret} label={t('Rolar em segredo', 'Roll in secret')} />
          {fighter.weapons.map((w) => {
            const damage = damageFormula(w.damage);
            return (
              <div key={w.key} className={styles.weapon}>
                <div className={styles.rowBetween}>
                  <strong>{w.name}</strong>
                  {w.magazine != null && <span className={styles.stat}>{w.rounds ?? 0}/{w.magazine}</span>}
                </div>
                <div className={styles.row}>
                  <Button onClick={() => void roll(`1d10${signed(w.attackTotal)}`, 'Check', t(`ataque com ${w.name}`, `attack with ${w.name}`))}>{t('Ataque', 'Attack')} {signed(w.attackTotal)}</Button>
                  {damage && <Button variant="ghost" onClick={() => void roll(damage, 'Damage', t(`dano de ${w.name}`, `${w.name} damage`))}>{t('Dano', 'Damage')} {w.damage}</Button>}
                </div>
              </div>
            );
          })}
        </>
      )}
    </div>
  );
}

export function TemplateInspector({ template, onSave, onRemove, editable }: {
  template: SceneTemplate; onSave: (input: SceneTemplate) => void; onRemove: () => void; editable: boolean;
}) {
  const [form, setForm] = useState(template);
  if (!editable) return <span className={styles.muted}>{t('Esta área é de outra pessoa.', 'This area belongs to someone else.')}</span>;
  return (
    <div className={styles.stack}>
      <div className={styles.row}>
        <Select aria-label={t('Forma', 'Shape')} value={form.shape} onChange={(e) => setForm({ ...form, shape: e.target.value as TemplateShapeName })}>
          {(['Circle', 'Square', 'Cone', 'Line'] as TemplateShapeName[]).map((s) => <option key={s} value={s}>{SHAPE_LABEL[s]}</option>)}
        </Select>
        <input type="color" aria-label={t('Cor', 'Color')} value={form.color} onChange={(e) => setForm({ ...form, color: e.target.value })} />
      </div>
      <div className={styles.row}>
        <Field label={t('Tamanho (m)', 'Size (m)')}><NumField label={t('Tamanho em metros', 'Size in meters')} value={form.sizeMeters} min={0.5} max={200} step={0.5} onChange={(sizeMeters) => setForm({ ...form, sizeMeters })} /></Field>
        <Field label={t('Ângulo (°)', 'Angle (°)')}><NumField label={t('Ângulo em graus', 'Angle in degrees')} value={form.angle} min={-360} max={360} step={15} onChange={(angle) => setForm({ ...form, angle })} /></Field>
      </div>
      <input aria-label={t('Rótulo', 'Label')} value={form.label ?? ''} maxLength={40} placeholder={t('Rótulo (opcional)', 'Label (optional)')} onChange={(e) => setForm({ ...form, label: e.target.value })} />
      <Switch checked={form.locked} onChange={(locked) => setForm({ ...form, locked })} label={t('Travada', 'Locked')} />
      <div className={styles.rowBetween}>
        <Button variant="primary" onClick={() => onSave(form)}>{t('Salvar', 'Save')}</Button>
        <Button variant="danger" icon={<Trash2 size={15} />} onClick={onRemove}>{t('Remover', 'Remove')}</Button>
      </div>
    </div>
  );
}

// ------------------------------------------------------------------ combat

export function CombatTab({ scene, encounter, act, onSelectToken }: { scene: SceneView; encounter: EncounterDetail | undefined; act: Act; onSelectToken: (id: string) => void }) {
  const toast = useToast();
  const command = useEncounterCommand(scene.encounterId ?? 0);
  const rules = useEncounterRules();
  const [adding, setAdding] = useState(false);
  if (!scene.encounterId) {
    return (
      <div className={styles.stack}>
        <NewFight key={`${scene.id}:${scene.tokens.map((tk) => tk.id).join(',')}`} scene={scene} act={act} />
        <span className={styles.muted}>{t('Ou vincule um combate já montado no Friday Night Firefight, na aba Cena.', 'Or link a fight already built in Friday Night Firefight, in the Scene tab.')}</span>
      </div>
    );
  }
  if (!encounter) return <Spinner />;
  const run = (path: string, body?: Record<string, unknown>) => command.mutate({ path, body }, { onError: (e) => toast.notify(problemMessage(e), 'error') });
  // The combat page's dialog, wired to this fight.
  const addRun: Run = (path, body = {}, done, method) =>
    command.mutate({ path, body, method }, {
      onSuccess: (d) => { if (done) toast.notify(done); d.notices.forEach((n) => toast.notify(n, 'info')); },
      onError: (error) => toast.notify(problemMessage(error), 'error'),
    });
  const untokened = encounter.queue.some((f) => !scene.tokens.some((tk) => tk.combatantId === f.id));
  const rolled = encounter.queue.some((f) => f.initiative != null);
  return (
    <div className={styles.stack}>
      <div className={styles.rowBetween}>
        <span className={styles.title}>{tv(encounter.name)}</span>
        <Badge tone="red">{t(`Rodada ${encounter.round}`, `Round ${encounter.round}`)}</Badge>
      </div>
      <div className={styles.row}>
        <Button disabled={command.isPending} onClick={() => run('initiative')}>{rolled ? t('Rolar iniciativa de novo', 'Re-roll Initiative') : t('Rolar iniciativa', 'Roll Initiative')}</Button>
        <Button variant="primary" icon={<SkipForward size={15} />} disabled={command.isPending || !rolled || !!encounter.pending} onClick={() => run('next-turn')}>{t('Próximo turno', 'Next turn')}</Button>
      </div>
      <div className={styles.queue}>
        {encounter.queue.map((f, i, queue) => {
          const token = scene.tokens.find((tk) => tk.combatantId === f.id);
          return (
            <div key={f.id} className={`${styles.queueRow} ${styles.queueRowEdit} ${f.side === 'Ally' ? styles.queueAlly : styles.queueEnemy} ${f.id === encounter.activeId ? styles.queueActive : ''}`}>
              <span className={styles.stack} style={{ gap: 0 }}>
                <IconButton label={t(`Subir ${f.name} na fila`, `Move ${f.name} up`)} disabled={command.isPending || i === 0 || !rolled}
                  onClick={() => run(`combatants/${f.id}/move`, { beforeId: queue[i - 1].id })}><ArrowUp size={12} /></IconButton>
                <IconButton label={t(`Descer ${f.name} na fila`, `Move ${f.name} down`)} disabled={command.isPending || i === queue.length - 1 || !rolled}
                  onClick={() => run(`combatants/${f.id}/move`, { beforeId: queue[i + 2]?.id ?? null })}><ArrowDown size={12} /></IconButton>
              </span>
              <InitiativeField key={`${f.id}:${f.initiative ?? ''}`} value={f.initiative} label={t(`Iniciativa de ${f.name}`, `${f.name}'s Initiative`)}
                onSave={(initiative) => run(`combatants/${f.id}/initiative`, { initiative })} />
              <span className={styles.stack} style={{ gap: 2 }}>
                <span className={styles.queueName}>{f.name}</span>
                <span className={styles.row}>
                  <span className={styles.stat}>{f.hp}/{f.maxHp} HP</span>
                  <Badge tone={WOUND_TONE[f.woundState]}>{f.dead ? t('Morto', 'Dead') : WOUND_LABEL[f.woundState]}</Badge>
                </span>
              </span>
              {token ? <Button variant="ghost" onClick={() => onSelectToken(token.id)}>{t('Token', 'Token')}</Button> : <span className={styles.source}>{t('sem token', 'no token')}</span>}
            </div>
          );
        })}
      </div>
      <div className={styles.row}>
        <Button icon={<Plus size={15} />} disabled={!rules.data} onClick={() => setAdding(true)}>{t('Adicionar combatentes', 'Add fighters')}</Button>
        {untokened && <Button icon={<Swords size={15} />} onClick={() => act(() => tableApi.addEncounterTokens(scene.id), t('Tokens criados.', 'Tokens created.'))}>{t('Pôr no mapa quem está sem token', 'Put the ones without a token on the map')}</Button>}
      </div>
      <Link to={`/combat/${scene.encounterId}`} target="_blank" className={styles.link}>{t('Ataques, dano e cobertura na mesa de combate completa', 'Attacks, damage and cover on the full combat table')} <ExternalLink size={12} /></Link>
      {rules.data && <AddCombatantsDialog open={adding} onClose={() => setAdding(false)} rules={rules.data} busy={command.isPending} run={addRun} />}
    </div>
  );
}

/** A fighter's Initiative, typed by the GM: saved when the field loses focus (or on Enter) if it changed. */
function InitiativeField({ value, label, onSave }: { value: number | null; label: string; onSave: (n: number) => void }) {
  const [draft, setDraft] = useState(value);
  const save = () => { if (draft != null && draft !== value) onSave(draft); };
  return (
    <span className={styles.queueInit} onBlur={save} onKeyDown={(e) => { if (e.key === 'Enter') save(); }}>
      <NumField label={label} min={-10} max={60} value={draft ?? 0} onChange={(n) => setDraft(Math.round(n))} />
    </span>
  );
}

// ------------------------------------------------------------------ invites and tunnel

const TUNNEL_TEXT: Record<TunnelStatus['state'], string> = {
  Missing: t('O cloudflared não está instalado.', 'cloudflared is not installed.'),
  Downloading: t('Baixando o cloudflared…', 'Downloading cloudflared…'),
  Stopped: t('Fechada: só esta máquina acessa.', 'Closed: only this machine gets in.'),
  Starting: t('Abrindo o túnel…', 'Opening the tunnel…'),
  Running: t('Aberta na internet.', 'Open on the internet.'),
  Failed: t('O túnel falhou.', 'The tunnel failed.'),
};

export function InvitesTab({ seats, act }: { seats: Seat[]; act: Act }) {
  const toast = useToast();
  const characters = useCharacters();
  const tunnel = useQuery({ queryKey: tableKeys.tunnel, queryFn: tableApi.tunnel, refetchInterval: (q) => (q.state.data?.state === 'Starting' || q.state.data?.state === 'Downloading' ? 1500 : 10000) });
  const online = useQuery({ queryKey: tableKeys.online, queryFn: tableApi.onlineSeats, refetchInterval: 5000 });
  const lobby = useQuery({ queryKey: tableKeys.lobbyStatus, queryFn: tableApi.lobby });
  const client = useQueryClient();
  const [busy, setBusy] = useState(false);
  const [label, setLabel] = useState('');
  const [characterId, setCharacterId] = useState<number | null>(null);
  const [showRevoked, setShowRevoked] = useState(false);
  const revoked = seats.filter((s) => s.revoked).length;

  const tunnelAction = async (fn: () => Promise<TunnelStatus>) => {
    setBusy(true);
    try { await fn(); } catch (e) { toast.notify(problemMessage(e), 'error'); } finally { setBusy(false); void tunnel.refetch(); }
  };
  const status = tunnel.data;
  const base = status?.state === 'Running' && status.url ? status.url : status?.localUrl ?? null;
  const copy = (text: string) => navigator.clipboard.writeText(text).then(() => toast.notify(t('Link copiado.', 'Link copied.')), () => toast.notify(text));
  const setEntrance = async (open: boolean) => {
    try { client.setQueryData(tableKeys.lobbyStatus, await tableApi.setLobby(open)); } catch (e) { toast.notify(problemMessage(e), 'error'); }
  };
  const general = base ? `${base}/` : null;

  return (
    <div className={styles.stack}>
      <div className={styles.stack}>
        <span className={styles.label}>{t('Internet', 'Internet')}</span>
        <span className={styles.status}>
          <span className={`${styles.dot} ${status?.state === 'Running' ? styles.dotOn : status?.state === 'Failed' ? styles.dotOff : styles.dotWarn}`} />
          {status ? TUNNEL_TEXT[status.state] : t('Verificando…', 'Checking…')}
        </span>
        {status?.error && <span className={styles.error}>{status.error}</span>}
        <div className={styles.row}>
          {status?.state === 'Missing' && <Button icon={busy ? <Spinner /> : <Download size={15} />} disabled={busy} onClick={() => tunnelAction(tableApi.downloadTunnel)}>{t('Baixar cloudflared', 'Download cloudflared')}</Button>}
          {(status?.state === 'Stopped' || status?.state === 'Failed') && <Button variant="primary" icon={busy ? <Spinner /> : <Power size={15} />} disabled={busy} onClick={() => tunnelAction(tableApi.startTunnel)}>{t('Abrir para a internet', 'Open to the internet')}</Button>}
          {(status?.state === 'Running' || status?.state === 'Starting') && <Button icon={<Power size={15} />} disabled={busy} onClick={() => tunnelAction(tableApi.stopTunnel)}>{t('Fechar', 'Close')}</Button>}
        </div>
        <span className={styles.muted}>
          {status?.state === 'Missing'
            ? t('O cloudflared é o programa gratuito da Cloudflare que cria o endereço público (cerca de 60 MB, baixado do GitHub oficial da Cloudflare). Não precisa de conta nem de mexer no roteador.', 'cloudflared is Cloudflare\'s free program that creates the public address (about 60 MB, downloaded from Cloudflare\'s official GitHub). No account or router setup needed.')
            : t('O endereço muda a cada vez que o túnel abre: recopie os links. Por ele, os jogadores só alcançam a mesa.', 'The address changes every time the tunnel opens: copy the links again. Through it, players only reach the table.')}
        </span>
      </div>

      <div className={styles.section}>
        <span className={styles.label}>{t('Link geral', 'General link')}</span>
        {general && (
          <div className={styles.row}>
            <span className={`${styles.link} ${styles.grow}`}>{general}</span>
            <IconButton label={t('Copiar link geral', 'Copy the general link')} onClick={() => copy(general)}><Copy size={14} /></IconButton>
          </div>
        )}
        <Switch checked={lobby.data?.open ?? false} onChange={(open) => void setEntrance(open)} label={t('Entrada aberta', 'Entrance open')} />
        <span className={styles.muted}>
          {t('Mande este link a todos: enquanto a entrada estiver aberta, cada jogador escolhe um dos lugares livres abaixo. Feche depois que todos entrarem. A entrada fecha sozinha quando o programa reinicia.', 'Send this link to everyone: while the entrance is open, each player picks one of the free seats below. Close it once everyone is in. It closes by itself when the program restarts.')}
        </span>
      </div>

      <div className={styles.section}>
        <span className={styles.label}>{t('Jogadores', 'Players')}</span>
        <form className={styles.stack} onSubmit={(e) => {
          e.preventDefault();
          void act(async () => { await tableApi.createSeat(label.trim(), characterId); setLabel(''); setCharacterId(null); });
        }}>
          <input aria-label={t('Nome do jogador', 'Player name')} value={label} maxLength={60} placeholder={t('Nome do jogador', 'Player name')} onChange={(e) => setLabel(e.target.value)} />
          <div className={styles.row}>
            <Select className={styles.grow} aria-label={t('Personagem', 'Character')} value={characterId ?? ''} onChange={(e) => setCharacterId(e.target.value ? Number(e.target.value) : null)}>
              <option value="">{t('Sem personagem', 'No character')}</option>
              {(characters.data ?? []).map((c) => <option key={c.id} value={c.id}>{c.handle ?? c.name}</option>)}
            </Select>
            <Button type="submit" icon={<Plus size={15} />} disabled={!label.trim()}>{t('Convidar', 'Invite')}</Button>
          </div>
        </form>
        {seats.filter((s) => showRevoked || !s.revoked).map((s) => {
          const link = base ? `${base}/mesa/j/${s.token}` : null;
          const character = characters.data?.find((c) => c.id === s.characterId);
          return (
            <div key={s.id} className={`${styles.seat} ${s.revoked ? styles.seatRevoked : ''}`}>
              <div className={styles.rowBetween}>
                <span className={styles.row}>
                  <span className={styles.swatch} style={{ background: s.color }} />
                  <strong>{s.label}</strong>
                  {character && <span className={styles.muted}>{character.handle ?? character.name}</span>}
                </span>
                <span className={styles.status}>
                  <span className={`${styles.dot} ${online.data?.includes(s.id) ? styles.dotOn : s.claimed ? styles.dotWarn : ''}`} />
                  {s.revoked ? t('revogado', 'revoked') : online.data?.includes(s.id) ? t('conectado', 'connected') : s.claimed ? t('na mesa', 'taken') : t('livre', 'free')}
                </span>
              </div>
              {!s.revoked && link && (
                <div className={styles.row}>
                  <span className={`${styles.link} ${styles.grow}`}>{link}</span>
                  <IconButton label={t('Copiar link', 'Copy link')} onClick={() => copy(link)}><Copy size={14} /></IconButton>
                </div>
              )}
              {!s.revoked && (
                <div className={styles.row}>
                  {s.claimed && (
                    <Button variant="ghost" onClick={() => {
                      if (window.confirm(t(`Liberar o lugar de ${s.label}? Quem está nele sai da mesa, o link antigo para de funcionar e o lugar volta para a entrada.`, `Release ${s.label}'s seat? Whoever holds it leaves the table, the old link stops working and the seat goes back to the entrance.`))) void act(async () => { await tableApi.releaseSeat(s.id); });
                    }}>{t('Liberar', 'Release')}</Button>
                  )}
                  <Button variant="ghost" onClick={() => {
                    if (window.confirm(t(`Revogar o convite de ${s.label}? O link para de funcionar.`, `Revoke ${s.label}'s invite? The link stops working.`))) void act(async () => { await tableApi.revokeSeat(s.id); });
                  }}>{t('Revogar', 'Revoke')}</Button>
                </div>
              )}
            </div>
          );
        })}
        {revoked > 0 && (
          <Switch checked={showRevoked} onChange={setShowRevoked} label={t(`Mostrar revogados (${revoked})`, `Show revoked (${revoked})`)} />
        )}
        {status?.state !== 'Running' && seats.length > 0 && <span className={styles.muted}>{t('Com o túnel fechado, os links só abrem nesta máquina (bom para testar).', 'With the tunnel closed, the links only open on this machine (good for testing).')}</span>}
      </div>
    </div>
  );
}
