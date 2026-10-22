import { Home, Landmark, Shirt, UserRound } from 'lucide-react';
import { problemMessage, useCharacterCommand } from '../../api/characters';
import type { CharacterDetail, CharacterRules, CharacterSheet } from '../../api/characterTypes';
import { Button, NumberInput, Select } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import { Portrait, SheetPanel, StateTag } from './CharacterUi';
import type { Run } from './CombatPanels';
import { RelationsPanel, RoleLifepathPanels, RollMissingButton, StyleTablesFields, TablesPanel } from './LifepathPanels';
import type { SheetEditor } from './useSheetEditor';
import styles from './Characters.module.css';

interface Props { detail: CharacterDetail; rules: CharacterRules; editor: SheetEditor }

const PAYMENT_LABEL = { Paid: 'Em dia', Pending: 'Pendente (semana de carência)', Overdue: 'Atrasado', NotApplicable: 'Sem custo' } as const;
const PAYMENT_TONE = { Paid: 'ok', Pending: 'warn', Overdue: 'bad', NotApplicable: 'ok' } as const;

export function LifepathView({ detail, rules, editor }: Props) {
  const toast = useToast();
  const command = useCharacterCommand(detail.id);
  const run: Run = async (path, body = {}, success) => {
    await editor.flush();
    try {
      const result = await command.mutateAsync({ path, body });
      if (success) toast.notify(success);
      return result;
    } catch (error) {
      toast.notify(problemMessage(error), 'error');
      return null;
    }
  };

  return (
    <div className={styles.lifepath}>
      <div className={styles.column}>
        <OriginsPanel detail={detail} rules={rules} editor={editor} />
        <TablesPanel rules={rules} editor={editor} />
        <SheetPanel title="Histórico pessoal">
          <textarea className={styles.textarea} rows={5} value={editor.draft.lifepath.background ?? ''} aria-label="Histórico pessoal"
            placeholder="O que aconteceu antes de Night City, em suas palavras."
            onChange={(e) => editor.update((s) => ({ ...s, lifepath: { ...s.lifepath, background: e.target.value } }))} />
        </SheetPanel>
        <StylePanel detail={detail} rules={rules} editor={editor} />
      </div>
      <div className={styles.column}>
        <RoleLifepathPanels rules={rules} editor={editor} />
        <HousingPanel detail={detail} rules={rules} editor={editor} run={run} busy={command.isPending} />
        <SheetPanel title="Notas">
          <textarea className={styles.textarea} rows={4} value={editor.draft.notes ?? ''} aria-label="Notas"
            onChange={(e) => editor.update((s) => ({ ...s, notes: e.target.value }))} />
        </SheetPanel>
      </div>
      <div className={styles.column}>
        <RelationsPanel rules={rules} editor={editor} />
      </div>
    </div>
  );
}

// ------------------------------------------------------------ identity and origins

function OriginsPanel({ detail, rules, editor }: Props) {
  const s = editor.draft;
  const finalized = detail.sheet.creation.finalized;
  const region = rules.culturalOrigins.find((o) => o.region === s.lifepath.culturalRegion);
  return (
    <SheetPanel title="Identidade e origens" icon={<UserRound size={16} />} actions={<RollMissingButton rules={rules} editor={editor} />}>
      <div className={styles.fieldGrid}>
        <div className={styles.tableField}>
          <label className={styles.idLabel} htmlFor="lp-region">Origem cultural</label>
          <Select id="lp-region" value={s.lifepath.culturalRegion ?? ''}
            onChange={(e) => editor.update((x) => ({ ...x, lifepath: { ...x.lifepath, culturalRegion: e.target.value || null } }))}>
            <option value="">—</option>
            {rules.culturalOrigins.map((o) => <option key={o.region} value={o.region}>{o.region}</option>)}
          </Select>
        </div>
        <div className={styles.tableField}>
          <span className={styles.idLabel}>Idioma de origem</span>
          <span className={styles.small}>
            {s.identity.nativeLanguage ?? '—'} <span className={styles.muted}>(Language {rules.originLanguageLevel}, grátis)</span>
          </span>
          {finalized && <span className={styles.source}>Definido na criação; muda só por ajuste do Mestre.</span>}
          {region && <span className={styles.source}>Línguas da região: {region.languages.join(', ')}</span>}
        </div>
        <label className={styles.tableField}>
          <span className={styles.idLabel}>Reputação</span>
          <NumberInput className={`${styles.input} ${styles.inputSmall}`} min={0} max={10} value={s.identity.reputation}
            onChange={(v) => editor.update((x) => ({ ...x, identity: { ...x.identity, reputation: v ?? 0 } }))} />
        </label>
        <div className={styles.tableField}>
          <span className={styles.idLabel}>Improvement Points</span>
          <span className={`${styles.mono} ${styles.cyan}`}>{detail.sheet.progress.ip} <span className={styles.muted}>(total {detail.sheet.progress.ipEarned})</span></span>
        </div>
      </div>
    </SheetPanel>
  );
}

// ------------------------------------------------------------ style and fashion

function StylePanel({ detail, rules, editor }: Props) {
  const style = editor.draft.style;
  const set = (patch: Partial<CharacterSheet['style']>) => editor.update((s) => ({ ...s, style: { ...s.style, ...patch } }));
  const fashion = detail.report.inventory.filter((i) => i.fashion);
  const fashionware = detail.report.cyberware.filter((c) => c.type === 'Fashionware');
  return (
    <SheetPanel title="Moda e estilo" icon={<Shirt size={16} />}>
      <div className={styles.row} style={{ alignItems: 'flex-start' }}>
        <div style={{ width: 120 }}><Portrait detail={detail} compact /></div>
        <div className={`${styles.column} ${styles.grow}`} style={{ gap: 8 }}>
          <StyleTablesFields rules={rules} editor={editor} />
        </div>
      </div>
      <textarea className={styles.textarea} rows={3} value={style.appearance ?? ''} placeholder="Aparência: roupa, cabelo, marcas, como as pessoas a veem."
        aria-label="Aparência" onChange={(e) => set({ appearance: e.target.value })} />
      {fashion.length > 0 && (
        <div className={styles.column} style={{ gap: 4 }}>
          <span className={styles.idLabel}>Roupa do inventário usada no visual</span>
          {fashion.map((i) => (
            <label key={i.instanceId} className={styles.row}>
              <input type="checkbox" checked={style.outfitItems.includes(i.instanceId)}
                onChange={(e) => set({ outfitItems: e.target.checked ? [...style.outfitItems, i.instanceId] : style.outfitItems.filter((x) => x !== i.instanceId) })} />
              <span className={styles.small}>{i.name}{i.quantity > 1 ? ` x${i.quantity}` : ''}</span>
            </label>
          ))}
        </div>
      )}
      {fashionware.length > 0 && <span className={styles.small}>Fashionware: {fashionware.map((f) => f.name).join(', ')}</span>}
      <input className={styles.input} value={style.notes ?? ''} placeholder="Observações sobre a estética" aria-label="Observações de estilo"
        onChange={(e) => set({ notes: e.target.value })} />
    </SheetPanel>
  );
}

// ------------------------------------------------------------ housing and lifestyle

function HousingPanel({ detail, rules, editor, run, busy }: Props & { run: Run; busy: boolean }) {
  const h = editor.draft.housing;
  const report = detail.report.housing;
  const set = (patch: Partial<CharacterSheet['housing']>) => editor.update((s) => ({ ...s, housing: { ...s.housing, ...patch } }));
  const finalized = detail.sheet.creation.finalized;
  return (
    <SheetPanel title="Moradia e lifestyle" icon={<Home size={16} />} actions={<span className={styles.source}>{report.source}</span>}>
      <div className={styles.fieldGrid}>
        <div className={styles.tableField}>
          <span className={styles.idLabel}>Moradia</span>
          <Select value={h.housingId ?? ''} aria-label="Moradia" onChange={(e) => set({ housingId: e.target.value || null })}>
            <option value="">—</option>
            {rules.housing.map((o) => (
              <option key={o.id} value={o.id}>{o.label}{o.rent != null ? ` (${formatEb(o.rent)}/mês)` : o.corporate ? ' (dada pela Corp)' : ''}</option>
            ))}
          </Select>
        </div>
        <label className={styles.tableField}>
          <span className={styles.idLabel}>Local</span>
          <input className={styles.input} value={h.location ?? ''} placeholder="Combat Zone, Watson…" onChange={(e) => set({ location: e.target.value })} />
        </label>
        <div className={styles.tableField}>
          <span className={styles.idLabel}>Lifestyle</span>
          <Select value={h.lifestyleId ?? ''} aria-label="Lifestyle" onChange={(e) => set({ lifestyleId: e.target.value || null })}>
            <option value="">—</option>
            {rules.lifestyles.map((o) => <option key={o.id} value={o.id}>{o.label} ({formatEb(o.value)}/mês)</option>)}
          </Select>
        </div>
        <label className={styles.tableField}>
          <span className={styles.idLabel} title="Deixe vazio para usar a tabela">Aluguel combinado (eb)</span>
          <input className={styles.input} type="number" min={0} value={h.rentOverride ?? ''} placeholder={String(report.rent)}
            onChange={(e) => set({ rentOverride: e.target.value === '' ? null : Number(e.target.value) })} />
        </label>
      </div>
      <dl className={styles.kv}>
        <dt>Aluguel {report.rentFromTable ? '(tabela)' : '(combinado)'}</dt><dd>{formatEb(report.rent)}</dd>
        <dt>Lifestyle</dt><dd>{formatEb(report.lifestyleCost)}</dd>
        <dt>Total por mês</dt><dd>{formatEb(report.monthlyTotal)}</dd>
        <dt>Pago até</dt><dd>{report.paidUntil ? new Date(`${report.paidUntil}T00:00:00`).toLocaleDateString('pt-BR') : '—'}</dd>
      </dl>
      <div className={styles.row}>
        <StateTag tone={PAYMENT_TONE[report.status]}>{PAYMENT_LABEL[report.status]}{report.daysOverdue > 0 ? ` · ${report.daysOverdue} dia(s)` : ''}</StateTag>
        <span className={styles.grow} />
        <Button icon={<Landmark size={14} />} disabled={busy || !finalized || report.monthlyTotal === 0}
          title={finalized ? `Debita ${formatEb(report.monthlyTotal)} do saldo` : 'Conclua a criação antes'}
          onClick={() => run('housing/pay', { months: 1 }, 'Mês pago.')}>Pagar mês</Button>
      </div>
      {report.status === 'Overdue' && (
        <p className={`${styles.small} ${styles.red}`}>Lifestyle sem pagamento há mais de uma semana: Death Save no início de cada dia (CP:R p.377).</p>
      )}
    </SheetPanel>
  );
}
