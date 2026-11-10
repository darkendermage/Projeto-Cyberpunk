// Panels and collapsible-section bodies of the Caminho de Vida tab.
import type { ReactNode } from 'react';
import { ChevronDown, Cpu, HeartPulse, Landmark, NotebookPen, Shirt, UserRound } from 'lucide-react';
import type { CharacterDetail, CharacterRules, CharacterSheet } from '../../api/characterTypes';
import { Button, NumberInput, Select } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { Portrait, SheetPanel, StateTag } from './CharacterUi';
import type { Run } from './CombatPanels';
import { StyleTablesFields } from './LifepathPanels';
import type { SheetEditor } from './useSheetEditor';
import styles from './Characters.module.css';
import lp from './Lifepath.module.css';
import { locale, t, tv } from '../../i18n';

export interface SectionProps { detail: CharacterDetail; rules: CharacterRules; editor: SheetEditor }

const PAYMENT_LABEL = { Paid: t('Em dia', 'Up to date'), Pending: t('Pendente (semana de carência)', 'Overdue (grace week)'), Overdue: t('Atrasado', 'Late'), NotApplicable: t('Sem custo', 'Free') } as const;
const PAYMENT_TONE = { Paid: 'ok', Pending: 'warn', Overdue: 'bad', NotApplicable: 'ok' } as const;

// identity and origins

export function OriginsFields({ detail, rules, editor }: SectionProps) {
  const s = editor.draft;
  const finalized = detail.sheet.creation.finalized;
  const region = rules.culturalOrigins.find((o) => o.region === s.lifepath.culturalRegion);
  return (
    <div className={styles.fieldGrid}>
      <div className={styles.tableField}>
        <label className={styles.idLabel} htmlFor="lp-region">{t('Origem cultural', 'Cultural origin')}</label>
        <Select id="lp-region" value={s.lifepath.culturalRegion ?? ''}
          onChange={(e) => editor.update((x) => ({ ...x, lifepath: { ...x.lifepath, culturalRegion: e.target.value || null } }))}>
          <option value="">—</option>
          {rules.culturalOrigins.map((o) => <option key={o.region} value={o.region}>{o.region}</option>)}
        </Select>
      </div>
      <div className={styles.tableField}>
        <span className={styles.idLabel}>{t('Idioma de origem', 'Native language')}</span>
        <span className={styles.small}>
          {s.identity.nativeLanguage ?? '—'} <span className={styles.muted}>{t('(Language ', '(Language ')}{rules.originLanguageLevel}{t(', grátis)', ', free)')}</span>
        </span>
        {finalized && <span className={styles.source}>{t('Definido na criação; muda só por ajuste do Mestre.', 'Set at creation; only changes through a GM adjustment.')}</span>}
        {region && <span className={styles.source}>{t('Línguas da região: ', 'Languages of the region: ')}{region.languages.join(', ')}</span>}
      </div>
      <label className={styles.tableField}>
        <span className={styles.idLabel}>{t('Reputação', 'Reputation')}</span>
        <NumberInput className={`${styles.input} ${styles.inputSmall}`} min={0} max={10} value={s.identity.reputation}
          onChange={(v) => editor.update((x) => ({ ...x, identity: { ...x.identity, reputation: v ?? 0 } }))} />
      </label>
      <div className={styles.tableField}>
        <span className={styles.idLabel}>Improvement Points</span>
        <span className={`${styles.mono} ${styles.cyan}`}>{detail.sheet.progress.ip} <span className={styles.muted}>(total {detail.sheet.progress.ipEarned})</span></span>
      </div>
    </div>
  );
}

/** Read-only summary of the identity; "Editar" opens the identity section. */
export function QuickProfile({ detail, rules, editor, onEdit }: SectionProps & { onEdit: () => void }) {
  const s = editor.draft;
  const rows: { label: string; value: ReactNode }[] = [
    { label: t('Origem cultural', 'Cultural origin'), value: s.lifepath.culturalRegion ?? '—' },
    {
      label: t('Idioma de origem', 'Native language'),
      value: <>{s.identity.nativeLanguage ?? '—'} <span className={styles.muted}>{t(`(Language ${rules.originLanguageLevel}, grátis)`, `(Language ${rules.originLanguageLevel}, free)`)}</span></>,
    },
    { label: t('Reputação', 'Reputation'), value: <span className={styles.mono}>{s.identity.reputation}</span> },
    { label: 'Improvement Points', value: <span className={styles.mono}>{detail.sheet.progress.ip} <span className={styles.muted}>(total {detail.sheet.progress.ipEarned})</span></span> },
  ];
  return (
    <SheetPanel title={t('Perfil rápido', 'Quick profile')} icon={<UserRound size={16} />}
      actions={<Button variant="ghost" onClick={onEdit}>{t('Editar', 'Edit')}</Button>}>
      <dl className={lp.profile}>
        {rows.map((r) => (
          <div key={r.label} className={lp.profileRow}>
            <dt>{r.label}</dt>
            <dd>{r.value}</dd>
          </div>
        ))}
      </dl>
    </SheetPanel>
  );
}

/** Current Hit Points and Humanity, read from the calculated report. */
export function ConditionPanel({ detail }: { detail: CharacterDetail }) {
  const { health, humanity } = detail.report;
  const bar = (label: string, current: number, max: number) => {
    const pct = max > 0 ? Math.max(0, Math.min(100, (current / max) * 100)) : 0;
    return (
      <div className={lp.condition}>
        <div className={lp.conditionHead}><span>{label}</span><span className={styles.mono}><strong>{current}</strong> / {max}</span></div>
        <div className={styles.meter} role="meter" aria-label={label} aria-valuemin={0} aria-valuemax={max} aria-valuenow={current}>
          <div className={styles.meterFill} style={{ width: `${pct}%` }} />
        </div>
      </div>
    );
  };
  return (
    <SheetPanel title={t('Condição atual', 'Current condition')} icon={<HeartPulse size={16} />}>
      {bar(t('Pontos de vida', 'Hit Points'), health.currentHp, health.maxHp)}
      {bar(t('Humanidade', 'Humanity'), humanity.current, humanity.max)}
    </SheetPanel>
  );
}

// style and fashion

export function StylePanel({ detail, rules, editor }: SectionProps) {
  const style = editor.draft.style;
  const set = (patch: Partial<CharacterSheet['style']>) => editor.update((s) => ({ ...s, style: { ...s.style, ...patch } }));
  const fashion = detail.report.inventory.filter((i) => i.fashion);
  const fashionware = detail.report.cyberware.filter((c) => c.type === 'Fashionware');
  const worn = style.outfitItems.length;
  return (
    <SheetPanel title={t('Moda e estilo', 'Fashion and style')} icon={<Shirt size={16} />}>
      <div className={styles.row} style={{ alignItems: 'flex-start' }}>
        <div className={lp.stylePortrait}><Portrait detail={detail} compact /></div>
        <div className={`${styles.column} ${styles.grow}`} style={{ gap: 8 }}>
          <StyleTablesFields rules={rules} editor={editor} />
        </div>
      </div>
      <details className={lp.more}>
        <summary>
          {t('Aparência e roupas', 'Look and clothing')}
          {worn > 0 && <span className={styles.muted}>{t(` · ${worn} peça(s) no visual`, ` · ${worn} piece(s) worn`)}</span>}
          <ChevronDown size={14} aria-hidden />
        </summary>
        <div className={lp.moreBody}>
          <textarea className={styles.textarea} rows={3} value={style.appearance ?? ''} placeholder={t('Aparência: roupa, cabelo, marcas, como as pessoas a veem.', 'Look: clothes, hair, marks, how people see them.')}
            aria-label={t('Aparência', 'Look')} onChange={(e) => set({ appearance: e.target.value })} />
          {fashion.length > 0 && (
            <div className={styles.column} style={{ gap: 4 }}>
              <span className={styles.idLabel}>{t('Roupa do inventário usada no visual', 'Inventory clothing worn for the look')}</span>
              {fashion.map((i) => (
                <label key={i.instanceId} className={styles.row}>
                  <input type="checkbox" checked={style.outfitItems.includes(i.instanceId)}
                    onChange={(e) => set({ outfitItems: e.target.checked ? [...style.outfitItems, i.instanceId] : style.outfitItems.filter((x) => x !== i.instanceId) })} />
                  <span className={styles.small}>{tv(i.name)}{i.quantity > 1 ? ` x${i.quantity}` : ''}</span>
                </label>
              ))}
            </div>
          )}
          {fashionware.length > 0 && <span className={styles.small}>Fashionware: {fashionware.map((f) => tv(f.name)).join(', ')}</span>}
          <input className={styles.input} value={style.notes ?? ''} placeholder={t('Observações sobre a estética', 'Notes about the look')} aria-label={t('Observações de estilo', 'Style notes')}
            onChange={(e) => set({ notes: e.target.value })} />
        </div>
      </details>
    </SheetPanel>
  );
}

// housing and lifestyle

export function HousingFields({ detail, rules, editor, run, busy }: SectionProps & { run: Run; busy: boolean }) {
  const h = editor.draft.housing;
  const report = detail.report.housing;
  const set = (patch: Partial<CharacterSheet['housing']>) => editor.update((s) => ({ ...s, housing: { ...s.housing, ...patch } }));
  const finalized = detail.sheet.creation.finalized;
  return (
    <>
      <div className={lp.housing}>
        <div className={lp.housingFields}>
          <div className={styles.tableField}>
            <span className={styles.idLabel}>{t('Moradia', 'Housing')}</span>
            <Select value={h.housingId ?? ''} aria-label={t('Moradia', 'Housing')} onChange={(e) => set({ housingId: e.target.value || null })}>
              <option value="">—</option>
              {rules.housing.map((o) => (
                <option key={o.id} value={o.id}>{tv(o.label)}{o.rent != null ? t(` (${formatEb(o.rent)}/mês)`, ` (${formatEb(o.rent)}/month)`) : o.corporate ? t(' (dada pela Corp)', ' (provided by the Corp)') : ''}</option>
              ))}
            </Select>
          </div>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>{t('Local', 'Location')}</span>
            <input className={styles.input} value={h.location ?? ''} placeholder={t('Combat Zone, Watson…', 'Combat Zone, Watson…')} onChange={(e) => set({ location: e.target.value })} />
          </label>
          <div className={styles.tableField}>
            <span className={styles.idLabel}>Lifestyle</span>
            <Select value={h.lifestyleId ?? ''} aria-label="Lifestyle" onChange={(e) => set({ lifestyleId: e.target.value || null })}>
              <option value="">—</option>
              {rules.lifestyles.map((o) => <option key={o.id} value={o.id}>{tv(o.label)} ({formatEb(o.value)}{t('/mês)', '/month)')}</option>)}
            </Select>
          </div>
          <label className={styles.tableField}>
            <span className={styles.idLabel} title={t('Deixe vazio para usar a tabela', 'Leave empty to use the table')}>{t('Aluguel combinado (eb)', 'Agreed rent (eb)')}</span>
            <input className={styles.input} type="number" min={0} value={h.rentOverride ?? ''} placeholder={String(report.rent)}
              onChange={(e) => set({ rentOverride: e.target.value === '' ? null : Number(e.target.value) })} />
          </label>
        </div>
        <dl className={`${styles.kv} ${lp.housingTotals}`}>
          <dt>{t('Aluguel ', 'Rent ')}{report.rentFromTable ? t('(tabela)', '(table)') : t('(combinado)', '(agreed)')}</dt><dd>{formatEb(report.rent)}</dd>
          <dt>Lifestyle</dt><dd>{formatEb(report.lifestyleCost)}</dd>
          <dt>{t('Total por mês', 'Total per month')}</dt><dd>{formatEb(report.monthlyTotal)}</dd>
          <dt>{t('Pago até', 'Paid until')}</dt><dd>{report.paidUntil ? new Date(`${report.paidUntil}T00:00:00`).toLocaleDateString(locale) : '—'}</dd>
        </dl>
      </div>
      <div className={styles.row}>
        <StateTag tone={PAYMENT_TONE[report.status]}>{PAYMENT_LABEL[report.status]}{report.daysOverdue > 0 ? t(` · ${report.daysOverdue} dia(s)`, ` · ${report.daysOverdue} day(s)`) : ''}</StateTag>
        <span className={styles.grow} />
        <Button icon={<Landmark size={14} />} disabled={busy || !finalized || report.monthlyTotal === 0}
          title={finalized ? t(`Debita ${formatEb(report.monthlyTotal)} do saldo`, `Takes ${formatEb(report.monthlyTotal)} from the balance`) : t('Conclua a criação antes', 'Finish creation first')}
          onClick={() => run('housing/pay', { months: 1 }, t('Mês pago.', 'Month paid.'))}>{t('Pagar mês', 'Pay month')}</Button>
      </div>
      {report.status === 'Overdue' && (
        <p className={`${styles.small} ${styles.red}`}>{t('Lifestyle sem pagamento há mais de uma semana: Death Save no início de cada dia (CP:R p.377).', 'Lifestyle unpaid for more than a week: Death Save at the start of each day (CP:R p.377).')}</p>
      )}
    </>
  );
}

// free text

export function HistoryField({ editor }: { editor: SheetEditor }) {
  return (
    <textarea className={styles.textarea} rows={5} value={editor.draft.lifepath.background ?? ''} aria-label={t('Histórico pessoal', 'Personal history')}
      placeholder={t('O que aconteceu antes de Night City, em suas palavras.', 'What happened before Night City, in your own words.')}
      onChange={(e) => editor.update((s) => ({ ...s, lifepath: { ...s.lifepath, background: e.target.value } }))} />
  );
}

export function NotesPanel({ editor }: { editor: SheetEditor }) {
  return (
    <SheetPanel title={t('Notas', 'Notes')} icon={<NotebookPen size={16} />}>
      <textarea className={styles.textarea} rows={4} value={editor.draft.notes ?? ''} aria-label={t('Notas', 'Notes')}
        placeholder={t('Registre observações, ideias ou ganchos de trama…', 'Write down observations, ideas or plot hooks…')}
        onChange={(e) => editor.update((s) => ({ ...s, notes: e.target.value }))} />
    </SheetPanel>
  );
}

/** Cyberware effects in play (skill bonuses, senses); the Lifepath itself has no mechanical effects. */
export function EffectsPanel({ detail }: { detail: CharacterDetail }) {
  const effects = detail.report.cyberwareEffects;
  return (
    <SheetPanel title={t('Efeitos ativos', 'Active effects')} icon={<Cpu size={16} />}>
      {effects.length === 0 && <span className={`${styles.small} ${styles.muted}`}>{t('Nenhum efeito de cyberware.', 'No cyberware effects.')}</span>}
      <ul className={lp.effects}>
        {effects.map((e, i) => (
          <li key={`${e.item}-${i}`} title={e.source}>
            <strong>{tv(e.item)}</strong>
            <span>{tv(e.description)}</span>
          </li>
        ))}
      </ul>
    </SheetPanel>
  );
}
