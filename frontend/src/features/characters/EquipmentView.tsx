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

  const buy = async (input: PurchaseInput) => { await run('purchase', { ...input }, input.received ? 'Item recebido.' : 'Compra registrada.'); };

  return (
    <SheetPanel title="Dinheiro e compras" icon={<Wallet size={16} />}
      actions={(
        <>
          <Button icon={<ShoppingBag size={14} />} disabled={!finalized} title={finalized ? 'Comprar com o saldo' : 'Compras da criação ficam no passo Equipamento'}
            onClick={() => setStore('buy')}>Comprar</Button>
          <Button icon={<Gift size={14} />} disabled={!finalized}
            title={finalized ? 'Troca, presente ou saque: entra sem pagar' : 'Itens sem custo só entram depois da criação'}
            onClick={() => setStore('receive')}>Receber</Button>
        </>
      )}>
      <div className={styles.bigNumbers}>
        <strong>{formatEb(detail.sheet.wallet.balance)}</strong>
        <span className={styles.muted}>eurodólares</span>
      </div>
      {finalized && (
        <div className={styles.row}>
          <input className={`${styles.input} ${styles.inputSmall}`} style={{ width: 90 }} type="number" value={amount} placeholder="± eb"
            aria-label="Valor do ajuste" onChange={(e) => setAmount(e.target.value)} />
          <input className={`${styles.input} ${styles.grow}`} value={reason} placeholder="Motivo (pagamento, despesa…)" aria-label="Motivo"
            onChange={(e) => setReason(e.target.value)} />
          <Button disabled={busy || !Number(amount) || !reason.trim()} onClick={async () => {
            if (await run('balance', { amount: Number(amount), reason }, 'Saldo ajustado.')) { setAmount(''); setReason(''); }
          }}>Lançar</Button>
        </div>
      )}

      <div className={styles.column} style={{ gap: 4 }}>
        <span className={styles.idLabel}>Movimentações</span>
        {entries.length === 0 && <span className={`${styles.small} ${styles.muted}`}>Nenhuma movimentação.</span>}
        <ul className={styles.list}>
          {entries.map((e) => (
            <li key={e.id} className={styles.listItem}>
              <span className={`${styles.small} ${styles.grow}`}>{e.description}{e.isManual && <span className={styles.tag}>manual</span>}
                <span className={styles.source}> · {formatDate(e.createdAt)}</span></span>
              {e.amount != null && e.amount !== 0 && <span className={`${styles.mono} ${e.amount > 0 ? styles.green : styles.red}`}>{signed(e.amount)}</span>}
            </li>
          ))}
        </ul>
      </div>

      <Modal open={store !== null} onClose={() => setStore(null)} title={store === 'receive' ? 'Receber itens sem pagar' : 'Comprar itens'} wide>
        <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
          <p className={styles.small}>
            {store === 'receive'
              ? 'Itens trocados com outros jogadores, presentes e saques entram sem custo e ficam no histórico com a origem. Cyberware recebido vai para o inventário; instalar continua custando Humanity.'
              : `Saldo: ${formatEb(detail.sheet.wallet.balance)}. Compras saem do saldo e o servidor recusa as que passam dele.`}
            {' '}Nada é equipado automaticamente.
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
      <SheetPanel title="Inventário" icon={<Package size={16} />}>
        {gear.length === 0 ? <p className={styles.muted}>Nenhum equipamento.</p> : <InventoryTable items={gear} {...table} />}
      </SheetPanel>
      <SheetPanel title="Moda" icon={<Shirt size={16} />}>
        {fashion.length === 0
          ? <p className={`${styles.small} ${styles.muted}`}>Roupas e Fashionware do personagem aparecem aqui.</p>
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
      <thead><tr><th>Item</th><th className={styles.num}>Qtd</th><th className={styles.num}>Pago</th><th>Estado</th><th /></tr></thead>
      <tbody>
        {items.map((i) => {
          const armor = i.sp != null;
          return (
            <tr key={i.instanceId}>
              <td>
                {i.name}
                {i.caliber && <span className={styles.muted}> ({i.caliber})</span>}
              </td>
              <td className={styles.num}>{i.rounds ?? i.quantity}</td>
              <td className={styles.num}>
                {RECEIVED_LABEL[i.origin] ? <span className={styles.tag} title={i.notes ?? undefined}>{RECEIVED_LABEL[i.origin]}</span> : i.pricePaid ? formatEb(i.pricePaid) : '—'}
              </td>
              <td>
                <Select style={{ minWidth: 118 }} value={i.state === 'Equipped' && i.worn ? `Equipped:${i.worn}` : i.state} disabled={busy} aria-label={`Estado de ${i.name}`}
                  onChange={(e) => {
                    const [state, worn] = e.target.value.split(':');
                    void run('equip', { instanceId: i.instanceId, state, worn: worn ?? null });
                  }}>
                  {armor ? (
                    <>
                      <option value="Equipped:Body">No corpo</option>
                      <option value="Equipped:Head">Na cabeça</option>
                    </>
                  ) : <option value="Equipped">{ITEM_STATE_LABEL.Equipped}</option>}
                  <option value="Carried">{ITEM_STATE_LABEL.Carried}</option>
                  <option value="Stored">{ITEM_STATE_LABEL.Stored}</option>
                </Select>
              </td>
              <td>{finalized && <IconButton label={`Vender ou descartar ${i.name}`} onClick={() => onRemove(i)}><Trash2 size={14} /></IconButton>}</td>
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
    <Modal open onClose={onClose} title={`Vender ou descartar: ${item.name}`}>
      <div className={styles.panel} style={{ clipPath: 'none', border: 'none' }}>
        <div className={styles.formGrid}>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>Quantidade (de {held})</span>
            <NumberInput className={styles.input} min={1} max={held} value={quantity} data-autofocus
              onChange={(v) => setQuantity(v ?? 1)} />
          </label>
          <label className={styles.tableField}>
            <span className={styles.idLabel}>Valor recebido (eb)</span>
            <input className={styles.input} type="number" min={0} value={sale} placeholder="0 = descartar" onChange={(e) => setSale(e.target.value)} />
          </label>
        </div>
        <input className={styles.input} value={reason} placeholder={selling ? 'Para quem e por quê (obrigatório)' : 'Motivo (opcional)'}
          aria-label="Motivo" onChange={(e) => setReason(e.target.value)} />
        <p className={styles.source}>O Core não fixa preço de revenda (CP:R p.385): o valor combinado na mesa fica registrado como lançamento manual.</p>
        <div className={styles.row}>
          <Button variant={selling ? 'primary' : 'danger'} disabled={busy || (selling && !reason.trim())} onClick={async () => {
            if (await run('inventory/remove', { instanceId: item.instanceId, quantity, saleAmount: selling ? Number(sale) : null, reason: reason || null },
              selling ? 'Venda registrada.' : 'Item removido.')) onClose();
          }}>{selling ? `Vender por ${formatEb(Number(sale))}` : 'Descartar'}</Button>
          <Button variant="ghost" onClick={onClose}>Cancelar</Button>
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
    <SheetPanel title="Upgrades das armas" icon={<Crosshair size={16} />}>
      {weapons.length === 0 ? (
        <p className={`${styles.small} ${styles.muted}`}>Armas de fogo não exóticas têm 3 Attachment Slots (CP:R p.95). Compre a arma e o acessório para montá-los aqui.</p>
      ) : (
        <ul className={styles.list}>
          {weapons.map((w) => (
            <li key={w.instanceId} className={styles.listItem} style={{ flexDirection: 'column', alignItems: 'stretch', gap: 4 }}>
              <span className={styles.row}>
                <strong className={styles.grow}>{w.name}</strong>
                <span className={styles.small}>{w.attachments.length}/{w.attachmentSlots} anexos</span>
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
    <SheetPanel title="Vestíveis com slots" icon={<Glasses size={16} />}>
      {wearables.length === 0 ? (
        <p className={`${styles.small} ${styles.muted}`}>
          Smart Glasses, Smart Lens, Battleglove, Smart Gloves e Smart Ears do inventário aparecem aqui. Para pôr uma Option, escolha-a na
          clínica da aba Cyberware e use "Onde instalar": não precisa de Cybereye nem de Cyberarm.
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
        <strong className={styles.grow}>{w.name}</strong>
        <StateTag tone={w.worn ? 'ok' : 'warn'}>{w.worn ? 'vestido' : 'guardado'}</StateTag>
        <span className={styles.small}>{w.slotsUsed}/{w.slots} slots{w.cyberfingers > 0 ? ` · dedos ${w.cyberfingersUsed}/${w.cyberfingers}` : ''}</span>
      </span>
      {!w.worn && <span className={`${styles.small} ${styles.muted}`}>As Options só funcionam com o item equipado (veja o Inventário).</span>}
      {w.coversArm && (
        <label className={styles.tableField}>
          <span className={styles.idLabel}>Mão</span>
          <Select value={w.side ?? ''} disabled={busy} aria-label={`Mão da ${w.name}`}
            onChange={(e) => run('wearables/side', { wearableInstanceId: w.instanceId, side: e.target.value || null })}>
            <option value="">—</option>
            <option value="Left">Esquerda</option>
            <option value="Right">Direita</option>
          </Select>
        </label>
      )}
      {w.builtIn.length > 0 && <span className={styles.small}>Já vem com: {w.builtIn.join(', ')}</span>}
      {w.options.map((o) => (
        <span key={o.instanceId} className={styles.row}>
          <span className={`${styles.grow} ${styles.small}`}>{o.name} <span className={styles.source}>{o.finger ? 'Cyberfinger' : `${o.slots} slot(s)`}</span></span>
          <Button variant="ghost" icon={<X size={13} />} disabled={busy} aria-label={`Retirar ${o.name}`}
            onClick={() => run('wearables/remove', { wearableInstanceId: w.instanceId, optionInstanceId: o.instanceId }, `${o.name} retirado.`)} />
        </span>
      ))}
      {w.covered.length > 0 && <span className={`${styles.small} ${styles.amber}`}>Inacessíveis sob a luva: {w.covered.join(', ')}</span>}
      {w.requirement && <span className={`${styles.small} ${styles.amber}`}>Requisito: {w.requirement}</span>}
      {w.warnings.map((x) => <span key={x} className={`${styles.small} ${styles.amber}`}>{x}</span>)}
      <span className={styles.source}>{w.source}</span>
    </li>
  );
}
