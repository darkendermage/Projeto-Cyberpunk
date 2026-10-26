import { useState } from 'react';
import { Crosshair, Gift, Glasses, Package, Shirt, ShoppingBag, Trash2, Wallet, X } from 'lucide-react';
import { problemMessage, useCharacterCommand, useCharacterEvents } from '../../api/characters';
import type { CharacterDetail, CharacterRules, InventoryLine, WearableLine } from '../../api/characterTypes';
import { Button, IconButton, Modal, NumberInput, Select } from '../../components/ui/ui';
import { formatEb } from '../../lib/format';
import { useToast } from '../../state/ToastContext';
import { formatDate, ITEM_STATE_LABEL, signed } from './characterLabels';
import { SheetPanel, StateTag } from './CharacterUi';
import { Attachments, type Run } from './CombatPanels';
import { StorePicker, type PurchaseInput } from './StorePicker';
import type { SheetEditor } from './useSheetEditor';
import styles from './Characters.module.css';
import { t, tv } from '../../i18n';

interface Props { detail: CharacterDetail; rules: CharacterRules; editor: SheetEditor }

/**
 * Everything the character owns: money and purchases, the inventory with what is worn, weapon attachments and the
 * worn items with cyberware Option Slots. Firing, reloading and armor ablation stay on the Personagem tab.
 */
export function EquipmentView({ detail, editor }: Props) {
  const toast = useToast();
  const command = useCharacterCommand(detail.id);
  const run: Run = async (path, body = {}, success) => {
    await editor.flush();
    try {
      const result = await command.mutateAsync({ path, body });
      result.notices.forEach((n) => toast.notify(n, 'info'));
      if (success) toast.notify(success);
      return result;
    } catch (error) {
      toast.notify(problemMessage(error), 'error');
      return null;
    }
  };
  const busy = command.isPending;

  return (
    <div className={styles.lifepath}>
      <div className={styles.column}>
        <WalletPanel detail={detail} run={run} busy={busy} />
      </div>
      <div className={styles.column}>
        <InventoryPanels detail={detail} run={run} busy={busy} />
      </div>
      <div className={styles.column}>
        <AttachmentsPanel detail={detail} run={run} busy={busy} />
        <WearablesPanel detail={detail} run={run} busy={busy} />
      </div>
    </div>
  );
}

// ------------------------------------------------------------ money: balance, store, ledger

type StoreMode = 'buy' | 'receive';

function WalletPanel({ detail, run, busy }: { detail: CharacterDetail; run: Run; busy: boolean }) {
  const [store, setStore] = useState<StoreMode | null>(null);
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const ledger = useCharacterEvents(detail.id);
  const finalized = detail.sheet.creation.finalized;
  const entries = (ledger.data ?? []).filter((e) => e.kind === 'Ledger').slice(0, 20);

  const buy = async (input: PurchaseInput) => { await run('purchase', { ...input }, input.received ? t('Item recebido.', 'Item received.') : t('Compra registrada.', 'Purchase recorded.')); };

  return (
    <SheetPanel title={t('Dinheiro e compras', 'Money and purchases')} icon={<Wallet size={16} />}
      actions={(
        <>
          <Button icon={<ShoppingBag size={14} />} disabled={!finalized} title={finalized ? t('Comprar com o saldo', 'Buy with the balance') : t('Compras da criação ficam no passo Equipamento', 'Creation purchases are in the Gear step')}
            onClick={() => setStore('buy')}>{t('Comprar', 'Buy')}</Button>
          <Button icon={<Gift size={14} />} disabled={!finalized}
            title={finalized ? t('Troca, presente ou saque: entra sem pagar', 'Trade, gift or loot: comes in without paying') : t('Itens sem custo só entram depois da criação', 'Free items only come in after creation')}
            onClick={() => setStore('receive')}>{t('Receber', 'Receive')}</Button>
        </>
      )}>
      <div className={styles.bigNumbers}>
        <strong>{formatEb(detail.sheet.wallet.balance)}</strong>
        <span className={styles.muted}>{t('eurodólares', 'eurodollars')}</span>
      </div>
      {finalized && (
        <div className={styles.row}>
          <input className={`${styles.input} ${styles.inputSmall}`} style={{ width: 90 }} type="number" value={amount} placeholder="± eb"
            aria-label={t('Valor do ajuste', 'Adjustment amount')} onChange={(e) => setAmount(e.target.value)} />
          <input className={`${styles.input} ${styles.grow}`} value={reason} placeholder={t('Motivo (pagamento, despesa…)', 'Reason (payment, expense…)')} aria-label={t('Motivo', 'Reason')}
            onChange={(e) => setReason(e.target.value)} />
          <Button disabled={busy || !Number(amount) || !reason.trim()} onClick={async () => {
            if (await run('balance', { amount: Number(amount), reason }, t('Saldo ajustado.', 'Balance adjusted.'))) { setAmount(''); setReason(''); }
          }}>{t('Lançar', 'Record')}</Button>
        </div>
      )}

      <div className={styles.column} style={{ gap: 4 }}>
        <span className={styles.idLabel}>{t('Movimentações', 'Transactions')}</span>
        {entries.length === 0 && <span className={`${styles.small} ${styles.muted}`}>{t('Nenhuma movimentação.', 'No transactions.')}</span>}
        <ul className={styles.list}>
          {entries.map((e) => (
            <li key={e.id} className={styles.listItem}>
              <span className={`${styles.small} ${styles.grow}`}>{tv(e.description)}{e.isManual && <span className={styles.tag}>manual</span>}
                <span className={styles.source}> · {formatDate(e.createdAt)}</span></span>
              {e.amount != null && e.amount !== 0 && <span className={`${styles.mono} ${e.amount > 0 ? styles.green : styles.red}`}>{signed(e.amount)}</span>}
            </li>
          ))}
        </ul>
      </div>

      <Modal open={store !== null} onClose={() => setStore(null)} title={store === 'receive' ? t('Receber itens sem pagar', 'Receive items without paying') : t('Comprar itens', 'Buy items')} wide>
        <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
          <p className={styles.small}>
            {store === 'receive'
              ? t('Itens trocados com outros jogadores, presentes e saques entram sem custo e ficam no histórico com a origem. Cyberware recebido vai para o inventário; instalar continua custando Humanity.', 'Items traded with other players, gifts and loot come in free and stay in the history with their origin. Received cyberware goes to the inventory; installing it still costs Humanity.')
              : t(`Saldo: ${formatEb(detail.sheet.wallet.balance)}. Compras saem do saldo e o servidor recusa as que passam dele.`, `Balance: ${formatEb(detail.sheet.wallet.balance)}. Purchases come out of the balance and the server refuses those that go over it.`)}
            {' '}{t('Nada é equipado automaticamente.', 'Nothing is equipped automatically.')}
          </p>
          <StorePicker busy={busy} creationBudgets={false} onBuy={buy} creating={false} receiving={store === 'receive'}
            available={{ balance: detail.sheet.wallet.balance }} />
        </div>
      </Modal>
    </SheetPanel>
  );
}

// ------------------------------------------------------------ inventory

const RECEIVED_LABEL: Partial<Record<InventoryLine['origin'], string>> = { Trade: 'troca', Gift: 'presente', Loot: 'saque' };

/** Gear and fashion (clothing and Fashionware, CP:R p.111) in separate panels, so the outfit doesn't bury the arsenal. */
function InventoryPanels({ detail, run, busy }: { detail: CharacterDetail; run: Run; busy: boolean }) {
  const [removing, setRemoving] = useState<InventoryLine | null>(null);
  const finalized = detail.sheet.creation.finalized;
  const gear = detail.report.inventory.filter((i) => !i.fashion);
  const fashion = detail.report.inventory.filter((i) => i.fashion);
  const table = { run, busy, finalized, onRemove: setRemoving };
  return (
    <>
      <SheetPanel title={t('Inventário', 'Inventory')} icon={<Package size={16} />}>
        {gear.length === 0 ? <p className={styles.muted}>{t('Nenhum equipamento.', 'No gear.')}</p> : <InventoryTable items={gear} {...table} />}
      </SheetPanel>
      <SheetPanel title={t('Moda', 'Fashion')} icon={<Shirt size={16} />}>
        {fashion.length === 0
          ? <p className={`${styles.small} ${styles.muted}`}>{t('Roupas e Fashionware do personagem aparecem aqui.', 'The character\'s clothes and Fashionware show up here.')}</p>
          : <InventoryTable items={fashion} {...table} />}
      </SheetPanel>
      {removing && <RemoveItemDialog item={removing} onClose={() => setRemoving(null)} run={run} busy={busy} />}
    </>
  );
}

function InventoryTable({ items, run, busy, finalized, onRemove }: {
  items: InventoryLine[]; run: Run; busy: boolean; finalized: boolean; onRemove: (item: InventoryLine) => void;
}) {
  return (
    <table className={styles.table}>
      <thead><tr><th>Item</th><th className={styles.num}>{t('Qtd', 'Qty')}</th><th className={styles.num}>{t('Pago', 'Paid')}</th><th>{t('Estado', 'State')}</th><th /></tr></thead>
      <tbody>
        {items.map((i) => {
          const armor = i.sp != null;
          return (
            <tr key={i.instanceId}>
              <td>
                {tv(i.name)}
                {i.caliber && <span className={styles.muted}> ({i.caliber})</span>}
              </td>
              <td className={styles.num}>{i.rounds ?? i.quantity}</td>
              <td className={styles.num}>
                {RECEIVED_LABEL[i.origin] ? <span className={styles.tag} title={tv(i.notes) ?? undefined}>{RECEIVED_LABEL[i.origin]}</span> : i.pricePaid ? formatEb(i.pricePaid) : '—'}
              </td>
              <td>
                <Select style={{ minWidth: 118 }} value={i.state === 'Equipped' && i.worn ? `Equipped:${i.worn}` : i.state} disabled={busy} aria-label={t(`Estado de ${tv(i.name)}`, `State of ${tv(i.name)}`)}
                  onChange={(e) => {
                    const [state, worn] = e.target.value.split(':');
                    void run('equip', { instanceId: i.instanceId, state, worn: worn ?? null });
                  }}>
                  {armor ? (
                    <>
                      <option value="Equipped:Body">{t('No corpo', 'On the body')}</option>
                      <option value="Equipped:Head">{t('Na cabeça', 'On the head')}</option>
                    </>
                  ) : <option value="Equipped">{ITEM_STATE_LABEL.Equipped}</option>}
                  <option value="Carried">{ITEM_STATE_LABEL.Carried}</option>
                  <option value="Stored">{ITEM_STATE_LABEL.Stored}</option>
                </Select>
              </td>
              <td>{finalized && <IconButton label={t(`Vender ou descartar ${tv(i.name)}`, `Sell or discard ${tv(i.name)}`)} onClick={() => onRemove(i)}><Trash2 size={14} /></IconButton>}</td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

function RemoveItemDialog({ item, onClose, run, busy }: { item: InventoryLine; onClose: () => void; run: Run; busy: boolean }) {
  const held = item.rounds ?? item.quantity;
  const [quantity, setQuantity] = useState(held);
  const [sale, setSale] = useState('');
  const [reason, setReason] = useState('');
  const selling = Number(sale) > 0;
  return (
    <Modal open onClose={onClose} title={t(`Vender ou descartar: ${tv(item.name)}`, `Sell or discard: ${tv(item.name)}`)}>
      <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
        <div className={styles.formGrid}>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>{t('Quantidade (de ', 'Quantity (of ')}{held})</span>
            <NumberInput className={styles.input} min={1} max={held} value={quantity} data-autofocus
              onChange={(v) => setQuantity(v ?? 1)} />
          </label>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>{t('Valor recebido (eb)', 'Amount received (eb)')}</span>
            <input className={styles.input} type="number" min={0} value={sale} placeholder={t('0 = descartar', '0 = discard')} onChange={(e) => setSale(e.target.value)} />
          </label>
        </div>
        <input className={styles.input} value={reason} placeholder={selling ? t('Para quem e por quê (obrigatório)', 'To whom and why (required)') : t('Motivo (opcional)', 'Reason (optional)')}
          aria-label={t('Motivo', 'Reason')} onChange={(e) => setReason(e.target.value)} />
        <p className={styles.source}>{t('O Core não fixa preço de revenda (CP:R p.385): o valor combinado na mesa fica registrado como lançamento manual.', 'The Core sets no resale price (CP:R p.385): the amount agreed at the table is recorded as a manual entry.')}</p>
        <div className={styles.row}>
          <Button variant={selling ? 'primary' : 'danger'} disabled={busy || (selling && !reason.trim())} onClick={async () => {
            if (await run('inventory/remove', { instanceId: item.instanceId, quantity, saleAmount: selling ? Number(sale) : null, reason: reason || null },
              selling ? t('Venda registrada.', 'Sale recorded.') : t('Item removido.', 'Item removed.'))) onClose();
          }}>{selling ? t(`Vender por ${formatEb(Number(sale))}`, `Sell for ${formatEb(Number(sale))}`) : t('Descartar', 'Discard')}</Button>
          <Button variant="ghost" onClick={onClose}>{t('Cancelar', 'Cancel')}</Button>
        </div>
      </div>
    </Modal>
  );
}

// ------------------------------------------------------------ weapon attachments

/** Attachments of each weapon (CP:R p.95); firing and reloading stay on the Personagem tab. */
function AttachmentsPanel({ detail, run, busy }: { detail: CharacterDetail; run: Run; busy: boolean }) {
  const weapons = detail.report.weapons.filter((w) => w.attachmentSlots > 0 || w.attachments.length > 0);
  return (
    <SheetPanel title={t('Upgrades das armas', 'Weapon attachments')} icon={<Crosshair size={16} />}>
      {weapons.length === 0 ? (
        <p className={`${styles.small} ${styles.muted}`}>{t('Armas de fogo não exóticas têm 3 Attachment Slots (CP:R p.95). Compre a arma e o acessório para montá-los aqui.', 'Non-exotic firearms have 3 Attachment Slots (CP:R p.95). Buy the weapon and the attachment to mount them here.')}</p>
      ) : (
        <ul className={styles.list}>
          {weapons.map((w) => (
            <li key={w.instanceId} className={styles.listItem} style={{ flexDirection: 'column', alignItems: 'stretch', gap: 4 }}>
              <span className={styles.row}>
                <strong className={styles.grow}>{tv(w.name)}</strong>
                <span className={styles.small}>{w.attachments.length}/{w.attachmentSlots} {t('anexos', 'attachments')}</span>
              </span>
              <Attachments detail={detail} weaponId={w.instanceId} slots={w.attachmentSlots} run={run} busy={busy} />
            </li>
          ))}
        </ul>
      )}
    </SheetPanel>
  );
}

// ------------------------------------------------------------ wearable hosts

/**
 * Worn items with Option Slots: Smart Glasses, Smart Lens, Battleglove, Smart Gloves, Smart Ears. Options go in from
 * the Cyberware clinic ("Onde instalar"); they cost no Humanity and work only while the item is worn.
 */
function WearablesPanel({ detail, run, busy }: { detail: CharacterDetail; run: Run; busy: boolean }) {
  const wearables = detail.report.wearables;
  return (
    <SheetPanel title={t('Vestíveis com slots', 'Wearables with slots')} icon={<Glasses size={16} />}>
      {wearables.length === 0 ? (
        <p className={`${styles.small} ${styles.muted}`}>
          {t('Smart Glasses, Smart Lens, Battleglove, Smart Gloves e Smart Ears do inventário aparecem aqui. Para pôr uma Option, escolha-a na clínica da aba Cyberware e use "Onde instalar": não precisa de Cybereye nem de Cyberarm.', 'Smart Glasses, Smart Lens, Battleglove, Smart Gloves and Smart Ears from the inventory show up here. To add an Option, pick it in the clinic on the Cyberware tab and use "Where to install": no Cybereye or Cyberarm needed.')}
        </p>
      ) : (
        <ul className={styles.list}>
          {wearables.map((w) => <WearableItem key={w.instanceId} w={w} run={run} busy={busy} />)}
        </ul>
      )}
    </SheetPanel>
  );
}

function WearableItem({ w, run, busy }: { w: WearableLine; run: Run; busy: boolean }) {
  return (
    <li className={styles.listItem} style={{ flexDirection: 'column', alignItems: 'stretch', gap: 4 }}>
      <span className={styles.row}>
        <strong className={styles.grow}>{tv(w.name)}</strong>
        <StateTag tone={w.worn ? 'ok' : 'warn'}>{w.worn ? t('vestido', 'worn') : t('guardado', 'stored')}</StateTag>
        <span className={styles.small}>{w.slotsUsed}/{w.slots} slots{w.cyberfingers > 0 ? t(` · dedos ${w.cyberfingersUsed}/${w.cyberfingers}`, ` · fingers ${w.cyberfingersUsed}/${w.cyberfingers}`) : ''}</span>
      </span>
      {!w.worn && <span className={`${styles.small} ${styles.muted}`}>{t('As Options só funcionam com o item equipado (veja o Inventário).', 'Options only work with the item equipped (see the Inventory).')}</span>}
      {w.coversArm && (
        <label className={styles.tableField}>
          <span className={styles.idLabel}>{t('Mão', 'Hand')}</span>
          <Select value={w.side ?? ''} disabled={busy} aria-label={t(`Mão da ${tv(w.name)}`, `${tv(w.name)} hand`)}
            onChange={(e) => run('wearables/side', { wearableInstanceId: w.instanceId, side: e.target.value || null })}>
            <option value="">—</option>
            <option value="Left">{t('Esquerda', 'Left')}</option>
            <option value="Right">{t('Direita', 'Right')}</option>
          </Select>
        </label>
      )}
      {w.builtIn.length > 0 && <span className={styles.small}>{t('Já vem com: ', 'Comes with: ')}{w.builtIn.join(', ')}</span>}
      {w.options.map((o) => (
        <span key={o.instanceId} className={styles.row}>
          <span className={`${styles.grow} ${styles.small}`}>{tv(o.name)} <span className={styles.source}>{o.finger ? 'Cyberfinger' : `${o.slots} slot(s)`}</span></span>
          <Button variant="ghost" icon={<X size={13} />} disabled={busy} aria-label={t(`Retirar ${tv(o.name)}`, `Take out ${tv(o.name)}`)}
            onClick={() => run('wearables/remove', { wearableInstanceId: w.instanceId, optionInstanceId: o.instanceId }, t(`${o.name} retirado.`, `${o.name} taken out.`))} />
        </span>
      ))}
      {w.covered.length > 0 && <span className={`${styles.small} ${styles.amber}`}>{t('Inacessíveis sob a luva: ', 'Inaccessible under the glove: ')}{w.covered.join(', ')}</span>}
      {w.requirement && <span className={`${styles.small} ${styles.amber}`}>{t('Requisito: ', 'Requirement: ')}{tv(w.requirement)}</span>}
      {w.warnings.map((x) => <span key={x} className={`${styles.small} ${styles.amber}`}>{x}</span>)}
      <span className={styles.source}>{w.source}</span>
    </li>
  );
}
