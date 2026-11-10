// Panels of the cyberware tab around the figure: status cards, region list, the selected region's pieces, the
// implant list, therapy, history, stored cyberware and the remove dialog.
import { useState, type ReactNode } from 'react';
import { Brain, Cpu, HeartPulse, History, Layers, Pencil, ShieldCheck, Trash2, Wallet } from 'lucide-react';
import { useCharacterEvents } from '../../api/characters';
import type { CharacterDetail, CharacterRules, CyberLine, CyberRegionReport } from '../../api/characterTypes';
import { Button, Checkbox, IconButton, Modal, Select } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { CYBER_TYPE_LABEL, REGION_ORDER, regionEffects, regionSlotsLabel, type RegionKey } from './anatomy';
import { formatDate } from './characterLabels';
import { SheetPanel, StateTag } from './CharacterUi';
import type { Run } from './CombatPanels';
import { CyberTypeIcon, RegionIcon } from './CyberIcons';
import type { Selection } from './CyberwareDetails';
import { paginate } from './gearLayout';
import { Pager } from './Pager';
import base from './Characters.module.css';
import styles from './Cyberware.module.css';
import { t, tv } from '../../i18n';

// status cards

function StatusCard({ icon, label, children, title }: { icon: ReactNode; label: string; children: ReactNode; title?: string }) {
  return (
    <div className={styles.statusItem} title={title}>
      <span className={styles.statusIcon} aria-hidden>{icon}</span>
      <div className={styles.statusBody}>
        <span className={styles.statusLabel}>{label}</span>
        {children}
      </div>
    </div>
  );
}

export function StatusBar({ detail }: { detail: CharacterDetail }) {
  const h = detail.report.humanity;
  const v = detail.validation;
  const pct = h.max > 0 ? Math.max(0, (h.current / h.max) * 100) : 0;
  return (
    <div className={styles.status}>
      <StatusCard icon={<Brain size={22} />} label={t('Humanidade', 'Humanity')}>
        <span className={styles.statusValue}>{h.current} <small>/ {h.max} · EMP {h.empEffective}</small></span>
        <div className={base.meter} aria-hidden><div className={base.meterFill} style={{ width: `${pct}%` }} /></div>
        {h.cyberpsychosis && <StateTag tone="bad">{t('Cyberpsicose', 'Cyberpsychosis')}</StateTag>}
      </StatusCard>
      <StatusCard icon={<HeartPulse size={22} />} label={t('Teto da terapia', 'Therapy cap')}
        title={t('Máximo que a terapia recupera com o cyberware instalado (CP:R p.230)', 'Most that therapy recovers with the installed cyberware (CP:R p.230)')}>
        <span className={styles.statusValue}>{h.therapyCap} <small>−2 × {h.piecesLoweringCap} {t('peças, −4 × ', 'pieces, −4 × ')}{h.borgwareLoweringCap} borgware</small></span>
      </StatusCard>
      <StatusCard icon={<Wallet size={22} />} label={v.gear ? t('Orçamento de criação', 'Creation budget') : t('Saldo', 'Balance')}>
        <span className={styles.statusValue}>
          {v.gear ? <>{formatEb(v.gear.remaining)} <small>{t('geral · ', 'general · ')}{formatEb(v.fashion?.remaining ?? 0)} {t('moda', 'fashion')}</small></> : <>{formatEb(detail.sheet.wallet.balance)} <small>eb</small></>}
        </span>
      </StatusCard>
      <StatusCard icon={<ShieldCheck size={22} />} label={t('Implantes', 'Implants')}>
        <span className={styles.statusValue}>{detail.report.cyberware.length} <small>{detail.report.fullBodyConversion ? 'Full Body Conversion (IR3 p.93)' : t('instalados', 'installed')}</small></span>
      </StatusCard>
    </div>
  );
}

// region list

export function RegionList({ regions, selected, onSelect }: {
  regions: Map<RegionKey, CyberRegionReport>; selected: RegionKey; onSelect: (key: RegionKey) => void;
}) {
  return (
    <SheetPanel title={t('Categorias', 'Categories')} icon={<Cpu size={16} />} className={styles.fill}>
      <ul className={styles.regionList}>
        {REGION_ORDER.map((key) => {
          const r = regions.get(key);
          if (!r) return null;
          const active = key === selected;
          return (
            <li key={key}>
              <button type="button" className={`${styles.regionItem} ${active ? styles.regionItemActive : ''} ${r.pieces.length > 0 ? styles.regionItemFilled : ''}`}
                aria-pressed={active} onClick={() => onSelect(key)} title={r.source ?? undefined}>
                <span className={styles.regionItemIcon}><RegionIcon region={key} /></span>
                <span className={styles.regionItemLabel}>{tv(r.label)}{r.meatLimb && <small> · {t('carne', 'meat')}</small>}</span>
                <span className={styles.regionItemSlots}>{regionSlotsLabel(r)}</span>
              </button>
            </li>
          );
        })}
      </ul>
    </SheetPanel>
  );
}

// the selected region's pieces

function PieceRow({ line, selected, creating, busy, onSelect, onReplace, onRemove }: {
  line: CyberLine; selected: boolean; creating: boolean; busy: boolean;
  onSelect: () => void; onReplace: () => void; onRemove: () => void;
}) {
  const typeLabel = CYBER_TYPE_LABEL[line.type ?? ''] ?? line.type;
  return (
    <li className={`${styles.pieceRow} ${selected ? styles.pieceRowSelected : ''} ${line.foundational ? styles.pieceRowFoundation : ''}`}>
      <button type="button" className={styles.pieceMain} onClick={onSelect} aria-pressed={selected} title={line.effect ?? undefined}>
        <span className={styles.pieceIcon}><CyberTypeIcon type={line.type} /></span>
        <span className={styles.pieceText}>
          <strong>{tv(line.name)}</strong>
          <small>HL {line.humanityLossApplied}{typeLabel ? ` · ${typeLabel}` : ''}{line.slotsProvided ? ` · ${line.slotsUsed}/${line.slotsProvided} slots` : ''}</small>
          {line.coveredBy && <small className={base.amber}>{t('coberto por ', 'covered by ')}{line.coveredBy}</small>}
        </span>
      </button>
      <span className={styles.installedTag}>{t('Instalado', 'Installed')}</span>
      {!creating && (
        <>
          <IconButton label={t(`Substituir ${tv(line.name)}`, `Replace ${tv(line.name)}`)} disabled={busy} onClick={onReplace}><Pencil size={15} /></IconButton>
          <IconButton label={t(`Remover ${tv(line.name)}`, `Remove ${tv(line.name)}`)} disabled={busy} onClick={onRemove}><Trash2 size={15} /></IconButton>
        </>
      )}
    </li>
  );
}

export function RegionPieces({ regionKey, region, lines, selection, creating, busy, onSelect, onReplace, onRemove }: {
  regionKey: RegionKey; region: CyberRegionReport; lines: Map<string, CyberLine>; selection: Selection; creating: boolean; busy: boolean;
  onSelect: (id: string) => void; onReplace: (id: string) => void; onRemove: (line: CyberLine) => void;
}) {
  const pieces = region.pieces.map((id) => lines.get(id)).filter((l): l is CyberLine => !!l);
  const effects = regionEffects(pieces);
  const slots = regionSlotsLabel(region);
  return (
    <SheetPanel title={tv(region.label)} icon={<RegionIcon region={regionKey} size={16} />}
      actions={<span className={styles.seal}>{region.slotsTotal != null ? `${slots} slots` : slots === '—' ? t('vazio', 'empty') : t(`${slots} peça(s)`, `${slots} piece(s)`)}</span>}>
      {pieces.length === 0
        ? <p className={`${base.small} ${base.muted}`}>{t('Nada instalado aqui. Escolha um item na clínica ao lado.', 'Nothing installed here. Pick an item in the clinic.')}</p>
        : (
          <ul className={styles.pieceList}>
            {pieces.map((l) => (
              <PieceRow key={l.instanceId} line={l} creating={creating} busy={busy}
                selected={selection?.kind === 'installed' && selection.instanceId === l.instanceId}
                onSelect={() => onSelect(l.instanceId)} onReplace={() => onReplace(l.instanceId)} onRemove={() => onRemove(l)} />
            ))}
          </ul>
        )}
      {effects.length > 0 && (
        <div className={styles.regionEffects}>
          <span className={styles.regionEffectsTitle}>{t(`Efeitos ativos (${tv(region.label)})`, `Active effects (${tv(region.label)})`)}</span>
          <ul>
            {effects.map((e) => <li key={e.name}>{tv(e.effect)} <span className={base.muted}>({tv(e.name)})</span></li>)}
          </ul>
        </div>
      )}
    </SheetPanel>
  );
}

/** Every installed implant, region by region (the "Lista" view of the figure). */
export function ImplantList({ regions, lines, selection, onSelect }: {
  regions: Map<RegionKey, CyberRegionReport>; lines: Map<string, CyberLine>; selection: Selection; onSelect: (region: RegionKey, id: string) => void;
}) {
  const filled = REGION_ORDER.map((key) => [key, regions.get(key)] as const).filter(([, r]) => r && r.pieces.length > 0);
  if (filled.length === 0) return <p className={`${base.small} ${base.muted}`}>{t('Nenhum implante instalado.', 'No implants installed.')}</p>;
  return (
    <div className={styles.implantList}>
      {filled.map(([key, r]) => (
        <section key={key} className={styles.implantGroup}>
          <header><RegionIcon region={key} size={15} /> {tv(r!.label)} <span className={base.muted}>{regionSlotsLabel(r!)}</span></header>
          <ul>
            {r!.pieces.map((id) => {
              const l = lines.get(id);
              if (!l) return null;
              const active = selection?.kind === 'installed' && selection.instanceId === id;
              return (
                <li key={id}>
                  <button type="button" className={active ? styles.implantActive : undefined} onClick={() => onSelect(key, id)}>
                    <CyberTypeIcon type={l.type} size={15} /> {tv(l.name)} <small>HL {l.humanityLossApplied}</small>
                  </button>
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </div>
  );
}

// stored cyberware

export function StoredPanel({ detail, onInstall }: { detail: CharacterDetail; onInstall: (instanceId: string, itemId: number, name: string) => void }) {
  const owned = detail.report.inventory.filter((i) => i.category === 'Cyberware');
  return (
    <SheetPanel title={t('Cyberware guardado', 'Stored cyberware')} icon={<Layers size={16} />} className={styles.fill}>
      {owned.length === 0 ? <p className={`${base.small} ${base.muted}`}>{t('Nenhum implante no inventário. Peças removidas ou compradas sem instalar aparecem aqui.', 'No implants in the inventory. Pieces removed or bought without installing show up here.')}</p> : (
        <ul className={base.list}>
          {owned.map((i) => (
            <li key={i.instanceId} className={base.listItem}>
              <span className={base.grow}>{tv(i.name)}{i.quantity > 1 ? ` x${i.quantity}` : ''}{i.notes && <span className={base.source}> · {tv(i.notes)}</span>}</span>
              <Button onClick={() => onInstall(i.instanceId, i.itemId, i.name)}>{t('Instalar', 'Install')}</Button>
            </li>
          ))}
        </ul>
      )}
    </SheetPanel>
  );
}

// therapy

export function TherapyPanel({ detail, rules, run, busy }: { detail: CharacterDetail; rules: CharacterRules; run: Run; busy: boolean }) {
  const therapies = rules.cyberware.therapies;
  const [kind, setKind] = useState(therapies[0]?.id ?? 'standard');
  const [payment, setPayment] = useState('clinic');
  const [roll, setRoll] = useState('');
  const h = detail.report.humanity;
  const entry = therapies.find((x) => x.id === kind);
  const cost = payment === 'clinic' ? entry?.cost ?? 0 : payment === 'medtech' ? entry?.materials ?? 0 : 0;
  const room = h.therapyCap - h.current;
  const locked = !detail.sheet.creation.finalized;

  return (
    <SheetPanel title={t('Terapia', 'Therapy')} icon={<HeartPulse size={16} />} actions={<span className={base.source}>{rules.cyberware.therapySource}</span>}>
      <p className={base.small}>
        {t('Uma semana de terapia recupera Humanidade até o teto de ', 'A week of therapy recovers Humanity up to the cap of ')}{h.therapyCap}
        {room > 0 ? t(` (faltam ${room}).`, ` (${room} to go).`) : t(': já está no teto. Remover cyberware libera mais.', ': already at the cap. Removing cyberware frees up more.')}
      </p>
      <div className={styles.therapyRow}>
        <Select value={kind} onChange={(e) => setKind(e.target.value)} aria-label={t('Tipo de terapia', 'Therapy type')}>
          {therapies.map((x) => <option key={x.id} value={x.id}>{tv(x.label)} ({tv(x.recovery)}, DV{x.dv})</option>)}
        </Select>
        <Select value={payment} onChange={(e) => setPayment(e.target.value)} aria-label={t('Pagamento', 'Payment')}>
          <option value="clinic">{t('Clínica (', 'Clinic (')}{formatEb(entry?.cost ?? 0)} eb)</option>
          <option value="medtech">{t('Medtech: só materiais (', 'Medtech: materials only (')}{formatEb(entry?.materials ?? 0)} eb)</option>
          <option value="free">{t('Sem custo (Mestre)', 'Free (GM)')}</option>
        </Select>
        <input className={`${base.input} ${base.inputSmall}`} type="number" min={1} value={roll} placeholder={tv(entry?.recovery)}
          aria-label={t('Resultado dos dados (vazio para rolar)', 'Dice result (empty to roll)')} onChange={(e) => setRoll(e.target.value)} />
      </div>
      <div className={base.row}>
        <Button variant="primary" icon={<HeartPulse size={15} />} disabled={busy || locked || room <= 0}
          title={locked ? t('Conclua a criação antes', 'Finish creation first') : t(`Custo: ${formatEb(cost)}`, `Cost: ${formatEb(cost)}`)}
          onClick={async () => { if (await run('therapy', { kind, payment, roll: roll ? Number(roll) : null })) setRoll(''); }}>
          {roll ? t('Registrar', 'Record') : t('Rolar', 'Roll')} {t('terapia', 'therapy')}
        </Button>
        <span className={base.source}>{t('Medtechs não fazem terapia em si mesmos (CP:R p.229).', 'Medtechs can\'t do therapy on themselves (CP:R p.229).')}</span>
      </div>
    </SheetPanel>
  );
}

// history

const HISTORY_PAGE = 5;

/** Humanity events (installs, removals, therapy), newest first. */
export function HistoryPanel({ detail }: { detail: CharacterDetail }) {
  const events = (useCharacterEvents(detail.id).data ?? []).filter((e) => e.kind === 'Humanity');
  const [page, setPage] = useState(0);
  const shown = paginate(events, page, HISTORY_PAGE);
  return (
    <SheetPanel title={t('Histórico', 'History')} icon={<History size={16} />}>
      {events.length === 0 && <p className={`${base.small} ${base.muted}`}>{t('Nenhuma instalação ou terapia registrada.', 'No installs or therapy recorded.')}</p>}
      <ul className={styles.history}>
        {shown.rows.map((e) => (
          <li key={e.id}>
            <span className={`${styles.historyDot} ${e.amount != null && e.amount > 0 ? styles.historyDotUp : ''}`} aria-hidden />
            <span className={styles.historyDate}>{formatDate(e.createdAt)}</span>
            <span className={styles.historyText}>{tv(e.description)}</span>
          </li>
        ))}
      </ul>
      {events.length > HISTORY_PAGE && (
        <Pager page={shown.page} pages={shown.pages} rows={shown.rows.length} total={events.length} noun={t('eventos', 'events')} onPage={setPage} size={HISTORY_PAGE} />
      )}
    </SheetPanel>
  );
}

// remove

export function RemoveImplantDialog({ detail, line, run, busy, onClose, onDone }: {
  detail: CharacterDetail; line: CyberLine; run: Run; busy: boolean; onClose: () => void; onDone: () => void;
}) {
  const [keep, setKeep] = useState(true);
  const children = detail.report.cyberware.filter((c) => c.parentInstanceId === line.instanceId);
  return (
    <Modal open onClose={onClose} title={t(`Remover ${tv(line.name)}`, `Remove ${tv(line.name)}`)}>
      <div className={base.panel} style={{ clipPath: 'none', border: 'none' }}>
        {children.length > 0 && <p className={base.small}>{t('Sai junto: ', 'Comes out too: ')}{children.map((c) => tv(c.name)).join(', ')}.</p>}
        <p className={base.small}>{t('A Humanidade perdida não volta com a remoção: só o teto da terapia sobe (CP:R p.230).', 'Lost Humanity doesn\'t come back on removal: only the therapy cap goes up (CP:R p.230).')}</p>
        <Checkbox checked={keep} onChange={setKeep} label={t('Guardar as peças no inventário', 'Keep the pieces in the inventory')} />
        <div className={base.row}>
          <Button variant="danger" data-autofocus disabled={busy} onClick={async () => {
            if (await run('cyberware/remove', { instanceId: line.instanceId, keep }, t(`${line.name} removido.`, `${line.name} removed.`))) onDone();
          }}>{t('Remover', 'Remove')}</Button>
          <Button variant="ghost" onClick={onClose}>{t('Cancelar', 'Cancel')}</Button>
        </div>
      </div>
    </Modal>
  );
}
