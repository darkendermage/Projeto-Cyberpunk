import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { problemMessage } from '../../api/characters';
import { encounterApi, encounterKeys, useEncounters } from '../../api/encounters';
import type { CombatSide } from '../../api/encounterTypes';
import type { NpcSummary } from '../../api/npcCompendium';
import { Button, ErrorState, Field, Modal, NumberInput, Segmented, Select, Spinner } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import page from '../../styles/Page.module.css';
import combat from '../combat/Combat.module.css';
import { t } from '../../i18n';
import { addCompendiumNpc, openEncounters } from './openEncounters';

/** Puts a compendium NPC with an official sheet into one of the open encounters. */
export function AddToCombatDialog({ npc, open, onClose }: { npc: Pick<NpcSummary, 'id' | 'name'>; open: boolean; onClose: () => void }) {
  const encounters = useEncounters();
  const client = useQueryClient();
  const navigate = useNavigate();
  const toast = useToast();
  const [encounterId, setEncounterId] = useState('');
  const [side, setSide] = useState<CombatSide>('Enemy');
  const [count, setCount] = useState<number | null>(1);
  const [busy, setBusy] = useState(false);
  const list = openEncounters(encounters.data ?? []);

  const confirm = async () => {
    const id = Number(encounterId);
    setBusy(true);
    try {
      await addCompendiumNpc(encounterApi, id, npc.id, side, count ?? 1);
      client.invalidateQueries({ queryKey: encounterKeys.list, exact: true });
      client.invalidateQueries({ queryKey: encounterKeys.detail(id) });
      onClose();
      navigate(`/combat/${id}`);
    } catch (error) {
      toast.notify(problemMessage(error), 'error');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open={open} onClose={onClose} title={t('Adicionar ao combate', 'Add to combat')}>
      <div className={combat.dialog}>
        {!encounters.data ? (
          encounters.isLoading ? <Spinner /> : <ErrorState error={encounters.error} onRetry={() => encounters.refetch()} />
        ) : list.length === 0 ? (
          <p>{t('Nenhum combate aberto. ', 'No open encounter. ')}<Link to="/combat" className={page.link}>{t('Abrir a tela de combate', 'Open the combat screen')}</Link></p>
        ) : (
          <>
            <Field label={t('Combate', 'Encounter')} htmlFor="atc-encounter">
              <Select id="atc-encounter" value={encounterId} onChange={(e) => setEncounterId(e.target.value)}>
                <option value="">{t('Escolha…', 'Choose…')}</option>
                {list.map((e) => <option key={e.id} value={e.id}>{e.name}</option>)}
              </Select>
            </Field>
            <div className={combat.row}>
              <Segmented<CombatSide> ariaLabel={t('Lado', 'Side')} value={side} onChange={setSide} options={[{ value: 'Enemy', label: t('Inimigo', 'Enemy') }, { value: 'Ally', label: t('Aliado', 'Ally') }]} />
              <Field label={t('Quantidade', 'Quantity')} htmlFor="atc-count"><NumberInput id="atc-count" min={1} max={12} value={count} onChange={setCount} /></Field>
            </div>
            <div className={combat.row}>
              <Button variant="primary" disabled={busy || !encounterId} onClick={confirm}>{t('Adicionar', 'Add')}</Button>
              <Button variant="ghost" onClick={onClose}>{t('Cancelar', 'Cancel')}</Button>
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}
