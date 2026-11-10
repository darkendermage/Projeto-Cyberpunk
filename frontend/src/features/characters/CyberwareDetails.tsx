// Item details of the cyberware tab: an installed implant (replace, remove, refund) or a clinic / inventory item
// with the install preview and validation.
import { useState, type ReactNode } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Glasses, Repeat, ShieldCheck, Trash2 } from 'lucide-react';
import { characterApi } from '../../api/characters';
import type { CharacterDetail, CharacterRules, CyberLine, InstallRequestBody, StoreItem } from '../../api/characterTypes';
import { useItemDetail } from '../../api/hooks';
import { ItemArt } from '../../components/art/ItemArt';
import { Button, Checkbox, Select, Spinner } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { CYBER_TYPE_LABEL, wearableAccepts } from './anatomy';
import { RefundButton, SheetPanel, StateTag } from './CharacterUi';
import type { Run } from './CombatPanels';
import base from './Characters.module.css';
import styles from './Cyberware.module.css';
import { t, tv } from '../../i18n';

export type Selection =
  | { kind: 'catalog'; item: StoreItem }
  | { kind: 'inventory'; instanceId: string; itemId: number; name: string }
  | { kind: 'installed'; instanceId: string }
  | null;

interface DetailsProps {
  detail: CharacterDetail; rules: CharacterRules; selection: Selection; run: Run; busy: boolean; creating: boolean;
  side: 'Left' | 'Right' | null; replacing: string | null; onReplace: (id: string | null) => void;
  onRemove: (line: CyberLine) => void; onDone: () => void;
}

export function DetailsPanel({ detail, rules, selection, run, busy, creating, side, replacing, onReplace, onRemove, onDone }: DetailsProps) {
  if (!selection) {
    return (
      <SheetPanel title={t('Detalhes do item', 'Item details')} className={styles.fill}>
        <p className={`${base.small} ${base.muted}`}>{t('Escolha um implante instalado ou um item da clínica para ver efeitos, requisitos e validar a instalação.', 'Pick an installed implant or a clinic item to see effects and requirements and validate the install.')}</p>
      </SheetPanel>
    );
  }
  if (selection.kind === 'installed') {
    const line = detail.report.cyberware.find((c) => c.instanceId === selection.instanceId);
    if (!line) return null;
    return <InstalledDetails detail={detail} line={line} run={run} busy={busy} creating={creating} onDone={onDone}
      replacing={replacing} onReplace={onReplace} onRemove={onRemove} />;
  }
  const itemId = selection.kind === 'catalog' ? selection.item.id : selection.itemId;
  return <InstallDetails key={`${selection.kind}-${itemId}`} detail={detail} rules={rules} selection={selection} run={run} busy={busy}
    replacing={replacing} side={side} onDone={() => { onReplace(null); onDone(); }} />;
}

/** Picture, name and tags on top of the details, then the catalog description. */
function ItemHeader({ itemId, name, tags }: { itemId: number; name: string; tags: ReactNode }) {
  const item = useItemDetail(itemId).data?.item;
  const effect = item?.attributes.effect as string | undefined;
  return (
    <div className={styles.itemHeader}>
      <div className={styles.itemArt}>{item ? <ItemArt item={item} size="detail" /> : <Spinner />}</div>
      <div className={styles.itemHeaderText}>
        <span className={styles.detailTitle}>{tv(name)}</span>
        <div className={styles.itemTags}>{tags}</div>
        {item?.description && <p className={`${base.small} ${styles.clamp}`} title={tv(item.description)}>{tv(item.description)}</p>}
        {effect && effect !== item?.description && <p className={base.small}><strong>{t('Efeito:', 'Effect:')}</strong> {effect}</p>}
        {item && <span className={base.source}>{tv(item.source?.name)}{item.page ? ` p.${item.page}` : ''}</span>}
      </div>
    </div>
  );
}

function InstallDetails({ detail, rules, selection, run, busy, replacing, side, onDone }: {
  detail: CharacterDetail; rules: CharacterRules; selection: Exclude<Selection, null | { kind: 'installed' }>;
  run: Run; busy: boolean; replacing: string | null; side: 'Left' | 'Right' | null; onDone: () => void;
}) {
  const item = selection.kind === 'catalog' ? selection.item : null;
  const slug = item?.slug ?? detail.report.inventory.find((i) => i.instanceId === (selection as { instanceId: string }).instanceId)?.slug ?? '';
  const name = item?.name ?? (selection as { name: string }).name;
  const c = rules.cyberware;
  const meatPossible = c.meatArmOptions.includes(slug) || c.meatLegOptions.includes(slug);
  const anyLimb = c.anyLimbOptions.includes(slug);
  const limbs = detail.report.cyberware.filter((l) => l.foundational && l.type === 'Cyberlimb');
  const [meatLimb, setMeatLimb] = useState(false);
  const [host, setHost] = useState('');
  const [ack, setAck] = useState(false);
  const itemId = item?.id ?? (selection as { itemId: number }).itemId;
  const fetched = useItemDetail(itemId).data?.item;
  const cyberType = item?.cyberType ?? (fetched?.attributes.cyberwareType as string | undefined);
  const accepts = wearableAccepts(cyberType);
  const targets = accepts ? detail.report.wearables.filter((w) => w.accepts === accepts) : [];
  const [target, setTarget] = useState('');
  const wearable = targets.find((w) => w.instanceId === target) ?? null;

  const body: InstallRequestBody = {
    itemId: selection.kind === 'catalog' ? selection.item.id : null,
    fromInventory: selection.kind === 'inventory' ? selection.instanceId : null,
    meatLimb, hostInstanceId: anyLimb && host ? host : null, acknowledge: ack,
    replacing: replacing && selection.kind === 'catalog' ? replacing : null,
    side,
  };
  const preview = useQuery({
    queryKey: ['cyber-preview', detail.id, detail.version, body],
    queryFn: ({ signal }) => characterApi.previewInstall(detail.id, body, signal),
    placeholderData: keepPreviousData,
    enabled: !wearable,
  });
  const p = preview.data;
  const replacedName = replacing ? detail.report.cyberware.find((l) => l.instanceId === replacing)?.name : null;
  const needsAck = (p?.textRequirements.length ?? 0) > 0;

  const putInWearable = async () => {
    const ok = await run('wearables/install', {
      wearableInstanceId: wearable!.instanceId,
      itemId: selection.kind === 'catalog' ? selection.item.id : null,
      optionInstanceId: selection.kind === 'inventory' ? selection.instanceId : null,
    }, t(`${name} posto em ${wearable!.name}.`, `${name} put in ${wearable!.name}.`));
    if (ok) onDone();
  };

  const install = async () => {
    const ok = replacing && selection.kind === 'catalog'
      ? await run('cyberware/replace', { instanceId: replacing, itemId: selection.item.id, meatLimb, hostInstanceId: body.hostInstanceId, acknowledge: ack, side },
        t(`${replacedName} substituído por ${name}.`, `${replacedName} replaced by ${name}.`))
      : await run('cyberware/install', { ...body }, t(`${name} instalado.`, `${name} installed.`));
    if (ok) onDone();
  };

  return (
    <SheetPanel title={t('Detalhes e validação', 'Details and validation')} className={styles.fill}
      actions={selection.kind === 'inventory' && <span className={base.tag}>{t('do inventário', 'from the inventory')}</span>}>
      <ItemHeader itemId={itemId} name={name} tags={item && (
        <>
          <span className={base.tag}>{CYBER_TYPE_LABEL[item.cyberType ?? ''] ?? item.cyberType}</span>
          <span className={styles.price}>{formatEb(item.price)} eb</span>
          {item.summary && <span className={base.small}>{tv(item.summary)}</span>}
        </>
      )} />
      {targets.length > 0 && !replacing && (
        <label className={base.tableField}>
          <span className={base.idLabel}>{t('Onde instalar', 'Where to install')}</span>
          <Select value={target} onChange={(e) => setTarget(e.target.value)} aria-label={t('Destino', 'Destination')}>
            <option value="">{t('No corpo', 'In the body')}</option>
            {targets.map((w) => (
              <option key={w.instanceId} value={w.instanceId}>
                {tv(w.name)} ({w.slotsUsed}/{w.slots} slots{w.worn ? '' : t(', guardado', ', stored')})
              </option>
            ))}
          </Select>
        </label>
      )}
      {!wearable && meatPossible && <Checkbox checked={meatLimb} onChange={setMeatLimb} label={t('Instalar num membro de carne (única peça do membro, CP:R p.115)', 'Install in a meat limb (the limb\'s only piece, CP:R p.115)')} />}
      {!wearable && anyLimb && (
        <label className={base.tableField}>
          <span className={base.idLabel}>{t('Membro (Cyberarm ou Cyberleg)', 'Limb (Cyberarm or Cyberleg)')}</span>
          <Select value={host} onChange={(e) => setHost(e.target.value)} aria-label={t('Membro hospedeiro', 'Host limb')}>
            <option value="">{t('Primeiro disponível', 'First available')}</option>
            {limbs.map((l) => <option key={l.instanceId} value={l.instanceId}>{tv(l.name)} ({l.side === 'Right' ? t('direito', 'right') : t('esquerdo', 'left')})</option>)}
          </Select>
        </label>
      )}

      {wearable ? (
        <div className={base.column} style={{ gap: 6 }}>
          <span className={base.idLabel}>{t('No vestível', 'In the wearable')}</span>
          <p className={base.small}>
            {name} {t('vai em ', 'goes in ')}<strong>{tv(wearable.name)}</strong>: {wearable.slotsUsed}/{wearable.slots} {t('slots em uso', 'slots in use')}
            {wearable.cyberfingers > 0 ? ` · Cyberfingers ${wearable.cyberfingersUsed}/${wearable.cyberfingers}` : ''}{t('. Nada é implantado: sem Humanity Loss, e só funciona com o item vestido.', '. Nothing is implanted: no Humanity Loss, and it only works with the item worn.')}
          </p>
          {selection.kind === 'catalog' && <span className={base.small}>{t('Custo: ', 'Cost: ')}{formatEb(selection.item.price)} {t('(o mesmo de instalar uma vez num Cybereye ou Cyberarm).', '(the same as installing it once in a Cybereye or Cyberarm).')}</span>}
          {wearable.requirement && <span className={`${base.small} ${base.amber}`}>{t('Requisito: ', 'Requirement: ')}{tv(wearable.requirement)}</span>}
          <span className={base.source}>{wearable.source}</span>
          <Button variant="primary" className={styles.validateButton} icon={<Glasses size={18} />} disabled={busy} onClick={putInWearable}>
            {t('Pôr em ', 'Put in ')}{tv(wearable.name)}
          </Button>
        </div>
      ) : (
        <div className={base.column} style={{ gap: 8 }}>
          <span className={base.idLabel}>{t('O que será instalado', 'What will be installed')}</span>
          {!p ? <Spinner /> : p.error && p.pieces.length === 0 ? <p className={`${base.small} ${base.red}`}>{p.error}</p> : (
            <table className={base.table}>
              <thead><tr><th>{t('Peça', 'Piece')}</th><th>{t('Onde', 'Where')}</th><th className={base.num}>HL</th><th className={base.num}>{t('Preço', 'Price')}</th></tr></thead>
              <tbody>
                {p.pieces.map((x) => (
                  <tr key={x.instanceId}>
                    <td>{tv(x.name)}</td>
                    <td className={base.small}>{x.host ?? (x.meatLimb ? t('membro de carne', 'meat limb') : x.region)}{x.side ? t(` · ${x.side === 'Right' ? 'dir.' : 'esq.'}`, ` · ${x.side === 'Right' ? 'R' : 'L'}`) : ''}</td>
                    <td className={base.num}>{x.humanityLossDice ? `${x.humanityLossDice}` : x.humanityLoss}</td>
                    <td className={base.total}>{formatEb(x.price)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {p && p.textRequirements.map((r) => <span key={r} className={`${base.small} ${base.amber}`}>{t('Requisito: ', 'Requirement: ')}{r}</span>)}
          {needsAck && <Checkbox checked={ack} onChange={setAck} label={t('O personagem cumpre esses requisitos', 'The character meets these requirements')} />}
          {p && (
            <dl className={base.kv}>
              <dt>{t('Custo', 'Cost')}</dt><dd>{formatEb(p.price)}</dd>
              <dt>{p.pool === 'Balance' ? t('Saldo', 'Balance') : p.pool === 'CreationFashion' ? t('Orçamento de moda', 'Fashion budget') : t('Orçamento geral', 'General budget')}</dt>
              <dd>{formatEb(p.available)} → {formatEb(p.availableAfter)}</dd>
              <dt>{t('Humanidade', 'Humanity')}</dt>
              <dd>{p.humanityNow} → {p.humanityLossMin === p.humanityLossMax ? p.humanityNow - p.humanityLossMax : `${p.humanityNow - p.humanityLossMax}–${p.humanityNow - p.humanityLossMin}`}</dd>
              <dt>{t('EMP (pior caso)', 'EMP (worst case)')}</dt><dd>{p.empNow} → {p.empAfterWorst}</dd>
              <dt>{t('Teto da terapia', 'Therapy cap')}</dt><dd>{p.therapyCapAfter}</dd>
            </dl>
          )}
          {p?.humanityLossRolled && <span className={base.source}>{t('Fora da criação a Humanity Loss é rolada na instalação (CP:R p.110).', 'Outside creation, Humanity Loss is rolled on install (CP:R p.110).')}</span>}
          {p?.warnings.map((w) => <span key={w} className={`${base.small} ${base.amber}`}>{w}</span>)}
          {replacedName && <StateTag tone="warn">{t('Substitui ', 'Replaces ')}{replacedName}</StateTag>}
          <Button variant="primary" className={styles.validateButton} icon={<ShieldCheck size={18} />}
            disabled={busy || !p || !!p.error || (needsAck && !ack)} onClick={install}>
            {replacedName ? t('Validar e substituir', 'Validate and replace') : t('Validar e instalar', 'Validate and install')}
          </Button>
        </div>
      )}
    </SheetPanel>
  );
}

function InstalledDetails({ detail, line, run, busy, creating, onDone, replacing, onReplace, onRemove }: {
  detail: CharacterDetail; line: CyberLine; run: Run; busy: boolean; creating: boolean; onDone: () => void;
  replacing: string | null; onReplace: (id: string | null) => void; onRemove: (line: CyberLine) => void;
}) {
  const piece = detail.sheet.cyberware.find((c) => c.instanceId === line.instanceId);
  const parent = line.parentInstanceId ? detail.report.cyberware.find((c) => c.instanceId === line.parentInstanceId) : null;
  const children = detail.report.cyberware.filter((c) => c.parentInstanceId === line.instanceId);
  // Refunding a host also refunds what was bought and installed in it.
  const refund = [line.instanceId, ...children.map((c) => c.instanceId)]
    .reduce((sum, id) => sum + (detail.sheet.cyberware.find((c) => c.instanceId === id)?.pricePaid ?? 0), 0);
  const typeLabel = CYBER_TYPE_LABEL[line.type ?? ''] ?? tv(line.type);

  return (
    <SheetPanel title={t('Detalhes do item', 'Item details')} className={styles.fill}>
      <ItemHeader itemId={line.itemId} name={line.name} tags={(
        <>
          <StateTag tone="ok">{t('Instalado', 'Installed')}</StateTag>
          {typeLabel && <span className={base.tag}>{typeLabel}</span>}
          <span className={base.tag}>HL {line.humanityLossApplied}</span>
          {line.side && <span className={base.tag}>{line.side === 'Right' ? t('lado direito', 'right side') : t('lado esquerdo', 'left side')}</span>}
          {line.meatLimb && <span className={base.tag}>{t('membro de carne', 'meat limb')}</span>}
        </>
      )} />
      <dl className={base.kv}>
        <dt>{t('Humanity Loss aplicada', 'Humanity Loss applied')}</dt><dd>{line.humanityLossApplied}{piece?.humanityLossRolled ? t(` (rolado ${line.humanityLossDice})`, ` (rolled ${line.humanityLossDice})`) : t(' (pré-definida)', ' (preset)')}</dd>
        {line.slotsProvided != null && <><dt>Option Slots</dt><dd>{line.slotsUsed}/{line.slotsProvided}</dd></>}
        {parent && <><dt>{t('Instalado em', 'Installed in')}</dt><dd>{tv(parent.name)}</dd></>}
        {children.length > 0 && <><dt>{t('Opções', 'Options')}</dt><dd>{children.map((c) => tv(c.name)).join(', ')}</dd></>}
        <dt>{t('Pago', 'Paid')}</dt><dd>{formatEb(piece?.pricePaid ?? 0)}</dd>
        {line.currentSp != null && <><dt>{t('SP atual', 'Current SP')}</dt><dd>{line.currentSp}</dd></>}
      </dl>
      {line.requirements && <span className={`${base.small} ${base.amber}`}>{t('Requisito: ', 'Requirement: ')}{tv(line.requirements)}</span>}
      {line.coveredBy && <span className={`${base.small} ${base.amber}`}>{t('Inacessível enquanto a luva estiver vestida: ', 'Inaccessible while the glove is worn: ')}{line.coveredBy}</span>}
      <div className={styles.detailActions}>
        {piece?.origin === 'Purchase' && (creating || piece.paidFrom === 'Balance') && (
          <RefundButton name={tv(line.name)} amount={refund} confirm={!creating} disabled={busy}
            onRefund={async () => { if (await run('purchase/undo', { instanceId: line.instanceId }, t('Compra reembolsada.', 'Purchase refunded.'))) onDone(); }} />
        )}
        {creating ? (
          piece?.origin !== 'Purchase' && <span className={`${base.small} ${base.muted}`}>{t('Peça do pacote inicial: muda depois de concluir a criação.', 'Starting package piece: change it after completing creation.')}</span>
        ) : (
          <>
            <Button icon={<Repeat size={15} />} active={replacing === line.instanceId}
              onClick={() => onReplace(replacing === line.instanceId ? null : line.instanceId)}>
              {replacing === line.instanceId ? t('Escolha o substituto na clínica', 'Pick the replacement in the clinic') : t('Substituir', 'Replace')}
            </Button>
            <Button variant="danger" icon={<Trash2 size={15} />} disabled={busy} onClick={() => onRemove(line)}>{t('Remover', 'Remove')}</Button>
          </>
        )}
      </div>
    </SheetPanel>
  );
}
