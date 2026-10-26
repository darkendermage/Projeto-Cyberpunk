import { useReducer, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Copy, Dices, Download, FilePlus2, Save, Sparkles } from 'lucide-react';
import { api, ApiError } from '../../api/client';
import { useNpcMutations, useNpcRules, useNpcs, useNpcValidation } from '../../api/hooks';
import type { GenerationConflict, NpcGenerationResult, NpcRules, NpcSummary } from '../../api/types';
import { Button, Collapsible, ErrorState, Field, Segmented, Select, Spinner, Switch } from '../../components/ui/ui';
import { downloadJson } from '../../lib/format';
import { useDebouncedValue } from '../../lib/hooks';
import { useToast } from '../../state/ToastContext';
import page from '../../styles/Page.module.css';
import { IdentityEditor, SkillsEditor, StatsEditor } from './NpcEditors';
import { CyberwareEditor, CyberwareRulesEditor, GearEditor } from './NpcEquipment';
import { BudgetBars, ConflictsPanel, GangPanel, LevelPicker, RulesSources, StatusBanner, ValidationPanel } from './NpcPanels';
import {
  buildGenerationRequest, type CreationMode, draftReducer, emptySheet, ERA_LABEL, gangsByEra, type GeneratorSettings, mergeGenerated,
} from './npcDraft';
import { NpcLibrary, SheetPreview } from './SheetPreview';
import styles from './EnemyGenerator.module.css';
import { t, tv } from '../../i18n';

const MODE_HINT: Record<CreationMode, string> = {
  auto: t('Escolha a categoria e, se quiser, uma gangue, um perfil e restrições de cyberware. O gerador monta a ficha inteira.', 'Pick the category and, if you like, a gang, a profile and cyberware restrictions. The generator builds the whole sheet.'),
  semi: t('Preencha e trave (cadeado) o que deve ficar como está. O gerador completa o resto com o orçamento que sobrar.', 'Fill in and lock (padlock) what should stay as is. The generator completes the rest with the remaining budget.'),
  manual: t('Monte a ficha à mão. A validação roda a cada alteração com as mesmas regras dos outros modos.', 'Build the sheet by hand. Validation runs on every change with the same rules as the other modes.'),
};

export function EnemyGeneratorPage() {
  const rules = useNpcRules();
  if (rules.isLoading) return <div className={page.page}><Spinner /></div>;
  if (rules.error || !rules.data) return <div className={page.page}><ErrorState error={rules.error} onRetry={() => rules.refetch()} /></div>;
  return <EnemyGenerator rules={rules.data} />;
}

function EnemyGenerator({ rules }: { rules: NpcRules }) {
  const toast = useToast();
  const [mode, setMode] = useState<CreationMode>('auto');
  const [sheet, dispatch] = useReducer(draftReducer, undefined, () => emptySheet());
  const [settings, setSettings] = useState<GeneratorSettings>({ profileId: '', gangId: '', seed: '', applyEmpGuideline: false });
  const [currentId, setCurrentId] = useState<number | null>(null);
  const [conflicts, setConflicts] = useState<GenerationConflict[]>([]);
  const [lastRun, setLastRun] = useState<NpcGenerationResult | null>(null);
  const [saveErrors, setSaveErrors] = useState<string[]>([]);

  const debounced = useDebouncedValue(sheet, 300);
  const validation = useNpcValidation(debounced);
  const report = validation.data;
  const pending = validation.isFetching || debounced !== sheet;

  const npcs = useNpcs();
  const mutations = useNpcMutations();
  const semi = mode === 'semi';

  const generate = useMutation({
    mutationFn: () => api.generateNpc(buildGenerationRequest(mode, sheet, settings)),
    onSuccess: (result) => {
      setLastRun(result);
      setConflicts(result.conflicts);
      if (result.success && result.sheet) {
        dispatch({ type: 'load', sheet: mergeGenerated(result.sheet, sheet, mode) });
        setCurrentId(null);
        toast.notify(t(`NPC gerado: ${result.sheet.name} (seed ${result.seed}).`, `NPC generated: ${result.sheet.name} (seed ${result.seed}).`));
      }
    },
    onError: (error) => toast.error(error),
  });

  const save = (draft: boolean) => {
    setSaveErrors([]);
    mutations.save.mutate({ id: currentId, sheet, draft }, {
      onSuccess: (detail) => {
        setCurrentId(detail.id);
        toast.notify(detail.status === 'Draft' ? t('Rascunho salvo (validação pendente).', 'Draft saved (validation pending).') : t(`${detail.sheet.name} salvo.`, `${detail.sheet.name} saved.`));
      },
      onError: (error) => {
        if (error instanceof ApiError) setSaveErrors(Object.values(error.fieldErrors).flat());
        toast.error(error);
      },
    });
  };

  const open = async (id: number) => {
    try {
      const detail = await api.npc(id);
      dispatch({ type: 'load', sheet: detail.sheet });
      setCurrentId(detail.id);
      setMode('manual');
      setConflicts([]);
    } catch (error) {
      toast.error(error);
    }
  };

  const duplicate = (id: number) => mutations.duplicate.mutate(id, {
    onSuccess: (detail) => {
      dispatch({ type: 'load', sheet: detail.sheet });
      setCurrentId(detail.id);
      setMode('manual');
      toast.notify(t(`Cópia criada: ${detail.sheet.name}.`, `Copy created: ${detail.sheet.name}.`));
    },
    onError: (error) => toast.error(error),
  });

  const remove = (npc: NpcSummary) => {
    if (!window.confirm(t(`Excluir "${npc.name}"? Esta ação não pode ser desfeita.`, `Delete "${npc.name}"? This can't be undone.`))) return;
    mutations.remove.mutate(npc.id, {
      onSuccess: () => {
        if (npc.id === currentId) setCurrentId(null);
        toast.notify(t(`${npc.name} excluído.`, `${npc.name} deleted.`));
      },
      onError: (error) => toast.error(error),
    });
  };

  const reset = () => {
    dispatch({ type: 'load', sheet: { ...emptySheet(sheet.level), hardened: sheet.hardened } });
    setCurrentId(null);
    setConflicts([]);
    setLastRun(null);
  };

  const profile = rules.profiles.find((p) => p.id === settings.profileId);
  const gang = rules.gangs.find((g) => g.id === settings.gangId);
  const ranGang = rules.gangs.find((g) => g.id === lastRun?.sheet?.gangId);

  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch={t('Gerador de Inimigos', 'Enemy Generator')}>{t('Gerador de Inimigos', 'Enemy Generator')}</h1>
        <p className={page.lead}>
          {t('Mooks, Lieutenants, Mini-Bosses e Bosses montados com os templates do Danger Gal Dossier (p.149–152) e as regras do Core. Toda ficha passa pela mesma validação, nos modos automático, semiautomático e manual.', 'Mooks, Lieutenants, Mini-Bosses and Bosses built with the Danger Gal Dossier templates (p.149–152) and the Core rules. Every sheet goes through the same validation, in automatic, semi-automatic and manual modes.')}
        </p>
      </header>

      <div className={styles.toolbar}>
        <div className={styles.toolbarGroup}>
          <span className={styles.toolbarLabel}>{t('Modo de criação', 'Creation mode')}</span>
          <Segmented<CreationMode> ariaLabel={t('Modo de criação', 'Creation mode')} value={mode} onChange={setMode}
            options={[{ value: 'auto', label: t('Automático', 'Automatic') }, { value: 'semi', label: t('Semiautomático', 'Semi-automatic') }, { value: 'manual', label: 'Manual' }]} />
        </div>
        <p className={styles.modeHint}>{MODE_HINT[mode]}</p>
      </div>

      <LevelPicker rules={rules} level={sheet.level} hardened={sheet.hardened}
        onChange={(level, hardened) => dispatch({ type: 'set', patch: { level, hardened } })} />

      {mode !== 'manual' && (
        <div className={styles.card}>
          <p className={styles.cardTitle}>{t('Configuração da geração', 'Generation settings')}</p>
          <div className={styles.formGrid}>
            <Field label={t('Gangue', 'Gang')} htmlFor="npc-gang" hint={gang ? `${tv(gang.kind)} · ${ERA_LABEL[gang.era] ?? gang.era}` : t('Sem gangue: só o arquétipo.', 'No gang: archetype only.')}>
              <Select id="npc-gang" value={settings.gangId} highlighted={!!gang}
                onChange={(e) => setSettings({ ...settings, gangId: e.target.value })}>
                <option value="">{t('Nenhuma', 'None')}</option>
                {gangsByEra(rules.gangs).map(([era, gangs]) => (
                  <optgroup key={era} label={ERA_LABEL[era] ?? era}>
                    {gangs.map((g) => <option key={g.id} value={g.id}>{tv(g.label)}</option>)}
                  </optgroup>
                ))}
              </Select>
            </Field>
            <Field label={t('Perfil', 'Profile')} htmlFor="npc-profile"
              hint={tv(profile?.description) ?? (gang ? t('Sorteado entre os arquétipos típicos da gangue (★).', 'Drawn from the gang\'s typical archetypes (★).') : t('Sorteado entre os perfis disponíveis.', 'Drawn from the available profiles.'))}>
              <Select id="npc-profile" value={settings.profileId} onChange={(e) => setSettings({ ...settings, profileId: e.target.value })}>
                <option value="">{t('Aleatório', 'Random')}</option>
                {rules.profiles.map((p) => (
                  <option key={p.id} value={p.id}>{gang?.preferredProfiles.includes(p.id) ? `★ ${tv(p.label)}` : tv(p.label)}</option>
                ))}
              </Select>
            </Field>
            <Field label={t('Seed (opcional)', 'Seed (optional)')} htmlFor="npc-seed" hint={t('A mesma seed reproduz o mesmo NPC.', 'The same seed reproduces the same NPC.')}>
              <input id="npc-seed" inputMode="numeric" value={settings.seed} placeholder={t('Aleatória', 'Random')}
                onChange={(e) => setSettings({ ...settings, seed: e.target.value.replace(/\D/g, '') })} />
            </Field>
            <div className={styles.toolbarGroup}>
              <Switch checked={settings.applyEmpGuideline} onChange={(v) => setSettings({ ...settings, applyEmpGuideline: v })}
                label={t('-1 EMP a cada 5 implantes (DGD p.152, opcional)', '-1 EMP per 5 implants (DGD p.152, optional)')} />
            </div>
          </div>
          {gang && <GangPanel gang={gang} rules={rules} />}
          <Collapsible title={t('Cyberware obrigatório e proibido', 'Required and forbidden cyberware')} active={sheet.cyberwareRules.length}>
            <CyberwareRulesEditor sheet={sheet} dispatch={dispatch} />
          </Collapsible>
          <div className={styles.actions}>
            <Button variant="primary" icon={generate.isPending ? <Spinner /> : <Sparkles size={15} />} disabled={generate.isPending}
              onClick={() => generate.mutate()}>
              {lastRun ? t('Gerar novamente', 'Generate again') : t('Gerar NPC', 'Generate NPC')}
            </Button>
            {lastRun?.success && (
              <Button variant="ghost" icon={<Dices size={15} />} onClick={() => setSettings({ ...settings, seed: String(lastRun.seed) })}>
                {t('Fixar seed ', 'Lock seed ')}{lastRun.seed}
              </Button>
            )}
          </div>
          {lastRun?.success && (
            <p className={styles.muted}>
              {ranGang && <>{t('Gangue ', 'Gang ')}{tv(ranGang.label)} · </>}
              {t('Perfil ', 'Profile ')}{tv(rules.profiles.find((p) => p.id === lastRun.profileId)?.label) ?? '—'} {t('· seed ', '· seed ')}{lastRun.seed} · {lastRun.attempts} {t('tentativa(s).', 'attempt(s).')}
            </p>
          )}
          <ConflictsPanel conflicts={conflicts} />
        </div>
      )}

      <div className={styles.layout}>
        <div className={styles.editors}>
          <Collapsible title={t('Identidade e Role', 'Identity and Role')} defaultOpen>
            <IdentityEditor rules={rules} sheet={sheet} dispatch={dispatch} semi={semi} />
          </Collapsible>
          <Collapsible title={t('STATs e valores derivados', 'STATs and derived values')} defaultOpen>
            <StatsEditor sheet={sheet} report={report} dispatch={dispatch} semi={semi} />
          </Collapsible>
          <Collapsible title={t('Perícias', 'Skills')} defaultOpen={mode !== 'auto'}>
            <SkillsEditor rules={rules} sheet={sheet} report={report} dispatch={dispatch} semi={semi} />
          </Collapsible>
          <Collapsible title={t('Combate e equipamento', 'Combat and gear')} defaultOpen={mode !== 'auto'}>
            <GearEditor sheet={sheet} report={report} dispatch={dispatch} semi={semi} />
          </Collapsible>
          <Collapsible title="Cyberware" defaultOpen={mode !== 'auto'}>
            <CyberwareEditor sheet={sheet} report={report} dispatch={dispatch} semi={semi} />
            {mode === 'manual' && (
              <Collapsible title={t('Obrigatórios e proibidos', 'Required and forbidden')} active={sheet.cyberwareRules.length}>
                <CyberwareRulesEditor sheet={sheet} dispatch={dispatch} />
              </Collapsible>
            )}
          </Collapsible>
          <Collapsible title={t('Anotações', 'Notes')}>
            <textarea value={sheet.notes ?? ''} maxLength={4000} aria-label={t('Anotações', 'Notes')}
              onChange={(e) => dispatch({ type: 'set', patch: { notes: e.target.value || null } })}
              placeholder={t('Táticas, ganchos, aliados…', 'Tactics, hooks, allies…')} />
          </Collapsible>
          <RulesSources rules={rules} sheet={sheet} />
        </div>

        <aside className={styles.aside}>
          {report ? (
            <>
              <div className={styles.card}>
                <StatusBanner report={report} pending={pending} />
                <div className={styles.spaced}><BudgetBars report={report} /></div>
              </div>
              <div className={styles.card}>
                <p className={styles.cardTitle}>{t('Ações', 'Actions')}</p>
                <div className={styles.actions}>
                  <Button variant="primary" icon={<Save size={15} />} disabled={!report.isValid || pending || mutations.save.isPending}
                    title={report.isValid ? undefined : t('Corrija os erros, registre exceções ou salve como rascunho.', 'Fix the errors, record exceptions or save as a draft.')}
                    onClick={() => save(false)}>
                    {currentId ? t('Salvar alterações', 'Save changes') : t('Salvar NPC', 'Save NPC')}
                  </Button>
                  <Button icon={<Save size={15} />} disabled={mutations.save.isPending} onClick={() => save(true)}>{t('Salvar rascunho', 'Save draft')}</Button>
                  <Button icon={<Copy size={15} />} disabled={currentId === null || mutations.duplicate.isPending}
                    title={currentId === null ? t('Salve o NPC antes de duplicar.', 'Save the NPC before duplicating.') : undefined} onClick={() => currentId && duplicate(currentId)}>
                    {t('Duplicar', 'Duplicate')}
                  </Button>
                  <Button variant="ghost" icon={<Download size={15} />}
                    onClick={() => downloadJson(`${sheet.name || 'npc'}.json`, { sheet, report })}>
                    {t('Exportar JSON', 'Export JSON')}
                  </Button>
                  <Button variant="ghost" icon={<FilePlus2 size={15} />} onClick={reset}>{t('Nova ficha', 'New sheet')}</Button>
                </div>
                {saveErrors.length > 0 && (
                  <ul className={styles.issues}>{saveErrors.map((e) => <li key={e} className={styles.muted}>{e}</li>)}</ul>
                )}
              </div>
              <div className={styles.card}>
                <p className={styles.cardTitle}>{t('Validação', 'Validation')}</p>
                <ValidationPanel report={report}
                  onException={(key, justification) => dispatch({ type: 'addException', key, justification })}
                  onRemoveException={(key) => dispatch({ type: 'removeException', key })} />
              </div>
              <SheetPreview rules={rules} sheet={sheet} report={report} />
            </>
          ) : validation.error ? (
            <ErrorState error={validation.error} onRetry={() => validation.refetch()} />
          ) : <Spinner />}
          <div className={styles.card}>
            <p className={styles.cardTitle}>{t('NPCs salvos', 'Saved NPCs')}</p>
            {npcs.data ? (
              <NpcLibrary npcs={npcs.data} currentId={currentId} onOpen={open} onDuplicate={duplicate} onDelete={remove} />
            ) : <Spinner />}
          </div>
        </aside>
      </div>
    </div>
  );
}
