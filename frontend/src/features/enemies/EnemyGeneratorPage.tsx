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
import { BudgetBars, ConflictsPanel, LevelPicker, RulesSources, StatusBanner, ValidationPanel } from './NpcPanels';
import {
  buildGenerationRequest, type CreationMode, draftReducer, emptySheet, type GeneratorSettings, mergeGenerated,
} from './npcDraft';
import { NpcLibrary, SheetPreview } from './SheetPreview';
import styles from './EnemyGenerator.module.css';

const MODE_HINT: Record<CreationMode, string> = {
  auto: 'Escolha a categoria e, se quiser, um perfil e restrições de cyberware. O gerador monta a ficha inteira.',
  semi: 'Preencha e trave (cadeado) o que deve ficar como está. O gerador completa o resto com o orçamento que sobrar.',
  manual: 'Monte a ficha à mão. A validação roda a cada alteração com as mesmas regras dos outros modos.',
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
  const [settings, setSettings] = useState<GeneratorSettings>({ profileId: '', seed: '', applyEmpGuideline: false });
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
        toast.notify(`NPC gerado: ${result.sheet.name} (seed ${result.seed}).`);
      }
    },
    onError: (error) => toast.error(error),
  });

  const save = (draft: boolean) => {
    setSaveErrors([]);
    mutations.save.mutate({ id: currentId, sheet, draft }, {
      onSuccess: (detail) => {
        setCurrentId(detail.id);
        toast.notify(detail.status === 'Draft' ? 'Rascunho salvo (validação pendente).' : `${detail.sheet.name} salvo.`);
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
      toast.notify(`Cópia criada: ${detail.sheet.name}.`);
    },
    onError: (error) => toast.error(error),
  });

  const remove = (npc: NpcSummary) => {
    if (!window.confirm(`Excluir "${npc.name}"? Esta ação não pode ser desfeita.`)) return;
    mutations.remove.mutate(npc.id, {
      onSuccess: () => {
        if (npc.id === currentId) setCurrentId(null);
        toast.notify(`${npc.name} excluído.`);
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

  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch="Gerador de Inimigos">Gerador de Inimigos</h1>
        <p className={page.lead}>
          Mooks, Lieutenants, Mini-Bosses e Bosses montados com os templates do Danger Gal Dossier (p.149–152) e as regras do
          Core. Toda ficha passa pela mesma validação, nos modos automático, semiautomático e manual.
        </p>
      </header>

      <div className={styles.toolbar}>
        <div className={styles.toolbarGroup}>
          <span className={styles.toolbarLabel}>Modo de criação</span>
          <Segmented<CreationMode> ariaLabel="Modo de criação" value={mode} onChange={setMode}
            options={[{ value: 'auto', label: 'Automático' }, { value: 'semi', label: 'Semiautomático' }, { value: 'manual', label: 'Manual' }]} />
        </div>
        <p className={styles.modeHint}>{MODE_HINT[mode]}</p>
      </div>

      <LevelPicker rules={rules} level={sheet.level} hardened={sheet.hardened}
        onChange={(level, hardened) => dispatch({ type: 'set', patch: { level, hardened } })} />

      {mode !== 'manual' && (
        <div className={styles.card}>
          <p className={styles.cardTitle}>Configuração da geração</p>
          <div className={styles.formGrid}>
            <Field label="Perfil" htmlFor="npc-profile" hint={profile?.description ?? 'Sorteado entre os perfis disponíveis.'}>
              <Select id="npc-profile" value={settings.profileId} onChange={(e) => setSettings({ ...settings, profileId: e.target.value })}>
                <option value="">Aleatório</option>
                {rules.profiles.map((p) => <option key={p.id} value={p.id}>{p.label}</option>)}
              </Select>
            </Field>
            <Field label="Seed (opcional)" htmlFor="npc-seed" hint="A mesma seed reproduz o mesmo NPC.">
              <input id="npc-seed" inputMode="numeric" value={settings.seed} placeholder="Aleatória"
                onChange={(e) => setSettings({ ...settings, seed: e.target.value.replace(/\D/g, '') })} />
            </Field>
            <div className={styles.toolbarGroup}>
              <Switch checked={settings.applyEmpGuideline} onChange={(v) => setSettings({ ...settings, applyEmpGuideline: v })}
                label="-1 EMP a cada 5 implantes (DGD p.152, opcional)" />
            </div>
          </div>
          <Collapsible title="Cyberware obrigatório e proibido" active={sheet.cyberwareRules.length}>
            <CyberwareRulesEditor sheet={sheet} dispatch={dispatch} />
          </Collapsible>
          <div className={styles.actions}>
            <Button variant="primary" icon={generate.isPending ? <Spinner /> : <Sparkles size={15} />} disabled={generate.isPending}
              onClick={() => generate.mutate()}>
              {lastRun ? 'Gerar novamente' : 'Gerar NPC'}
            </Button>
            {lastRun?.success && (
              <Button variant="ghost" icon={<Dices size={15} />} onClick={() => setSettings({ ...settings, seed: String(lastRun.seed) })}>
                Fixar seed {lastRun.seed}
              </Button>
            )}
          </div>
          {lastRun?.success && (
            <p className={styles.muted}>
              Perfil {rules.profiles.find((p) => p.id === lastRun.profileId)?.label ?? '—'} · seed {lastRun.seed} · {lastRun.attempts} tentativa(s).
            </p>
          )}
          <ConflictsPanel conflicts={conflicts} />
        </div>
      )}

      <div className={styles.layout}>
        <div className={styles.editors}>
          <Collapsible title="Identidade e Role" defaultOpen>
            <IdentityEditor rules={rules} sheet={sheet} dispatch={dispatch} semi={semi} />
          </Collapsible>
          <Collapsible title="STATs e valores derivados" defaultOpen>
            <StatsEditor sheet={sheet} report={report} dispatch={dispatch} semi={semi} />
          </Collapsible>
          <Collapsible title="Perícias" defaultOpen={mode !== 'auto'}>
            <SkillsEditor rules={rules} sheet={sheet} report={report} dispatch={dispatch} semi={semi} />
          </Collapsible>
          <Collapsible title="Combate e equipamento" defaultOpen={mode !== 'auto'}>
            <GearEditor sheet={sheet} report={report} dispatch={dispatch} semi={semi} />
          </Collapsible>
          <Collapsible title="Cyberware" defaultOpen={mode !== 'auto'}>
            <CyberwareEditor sheet={sheet} report={report} dispatch={dispatch} semi={semi} />
            {mode === 'manual' && (
              <Collapsible title="Obrigatórios e proibidos" active={sheet.cyberwareRules.length}>
                <CyberwareRulesEditor sheet={sheet} dispatch={dispatch} />
              </Collapsible>
            )}
          </Collapsible>
          <Collapsible title="Anotações">
            <textarea value={sheet.notes ?? ''} maxLength={4000} aria-label="Anotações"
              onChange={(e) => dispatch({ type: 'set', patch: { notes: e.target.value || null } })}
              placeholder="Táticas, ganchos, aliados…" />
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
                <p className={styles.cardTitle}>Ações</p>
                <div className={styles.actions}>
                  <Button variant="primary" icon={<Save size={15} />} disabled={!report.isValid || pending || mutations.save.isPending}
                    title={report.isValid ? undefined : 'Corrija os erros, registre exceções ou salve como rascunho.'}
                    onClick={() => save(false)}>
                    {currentId ? 'Salvar alterações' : 'Salvar NPC'}
                  </Button>
                  <Button icon={<Save size={15} />} disabled={mutations.save.isPending} onClick={() => save(true)}>Salvar rascunho</Button>
                  <Button icon={<Copy size={15} />} disabled={currentId === null || mutations.duplicate.isPending}
                    title={currentId === null ? 'Salve o NPC antes de duplicar.' : undefined} onClick={() => currentId && duplicate(currentId)}>
                    Duplicar
                  </Button>
                  <Button variant="ghost" icon={<Download size={15} />}
                    onClick={() => downloadJson(`${sheet.name || 'npc'}.json`, { sheet, report })}>
                    Exportar JSON
                  </Button>
                  <Button variant="ghost" icon={<FilePlus2 size={15} />} onClick={reset}>Nova ficha</Button>
                </div>
                {saveErrors.length > 0 && (
                  <ul className={styles.issues}>{saveErrors.map((e) => <li key={e} className={styles.muted}>{e}</li>)}</ul>
                )}
              </div>
              <div className={styles.card}>
                <p className={styles.cardTitle}>Validação</p>
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
            <p className={styles.cardTitle}>NPCs salvos</p>
            {npcs.data ? (
              <NpcLibrary npcs={npcs.data} currentId={currentId} onOpen={open} onDuplicate={duplicate} onDelete={remove} />
            ) : <Spinner />}
          </div>
        </aside>
      </div>
    </div>
  );
}
