import { useState } from 'react';
import { useCharacters } from '../../api/characters';
import type { CombatSide, EncounterRules } from '../../api/encounterTypes';
import { useNpcRules, useNpcs } from '../../api/hooks';
import type { NpcLevel } from '../../api/types';
import { Button, Field, Modal, NumberInput, Segmented, Select, Switch } from '../../components/ui/ui';
import { LEVEL_LABEL } from '../enemies/npcDraft';
import type { Run } from './EncounterView';
import styles from './Combat.module.css';

type Source = 'pc' | 'npc' | 'generate' | 'generic';
const LEVELS: NpcLevel[] = ['Mook', 'Lieutenant', 'MiniBoss', 'Boss'];

/** Brings people to the table: a character sheet, a saved NPC, freshly generated enemies or quick generic ones. */
export function AddCombatantsDialog({ open, onClose, rules, busy, run }: { open: boolean; onClose: () => void; rules: EncounterRules; busy: boolean; run: Run }) {
  const characters = useCharacters();
  const npcs = useNpcs();
  const npcRules = useNpcRules();
  const g = rules.rules.generic.value;
  const [source, setSource] = useState<Source>('generate');
  const [side, setSide] = useState<CombatSide>('Enemy');
  const [count, setCount] = useState<number | null>(3);
  const [characterId, setCharacterId] = useState('');
  const [npcId, setNpcId] = useState('');
  const [level, setLevel] = useState<NpcLevel>('Mook');
  const [hardened, setHardened] = useState(false);
  const [gangId, setGangId] = useState('');
  const [profileId, setProfileId] = useState('');
  const [name, setName] = useState('Segurança');
  const [ref, setRef] = useState<number | null>(g.ref);
  const [hp, setHp] = useState<number | null>(g.hp);
  const [sp, setSp] = useState<number | null>(g.spBody);
  const [attack, setAttack] = useState<number | null>(g.attackBase);
  const [evasion, setEvasion] = useState<number | null>(g.evasionBase);
  const [damage, setDamage] = useState(g.damage);

  const pcs = (characters.data ?? []).filter((c) => c.status !== 'Creating');

  const add = () => {
    const body: Record<string, unknown> = { side, count: count ?? 1 };
    if (source === 'pc') Object.assign(body, { characterId: Number(characterId), side: 'Ally', count: 1 });
    if (source === 'npc') body.npcId = Number(npcId);
    if (source === 'generate') body.generate = { level, hardened, gangId: gangId || null, profileId: profileId || null };
    if (source === 'generic') body.generic = { name, ref, hp, spBody: sp, spHead: sp, attackBase: attack, evasionBase: evasion, damage };
    run('combatants', body, 'Combatentes na mesa.');
    onClose();
  };

  const ready = source === 'pc' ? !!characterId : source === 'npc' ? !!npcId : source === 'generic' ? !!name.trim() : true;

  return (
    <Modal open={open} onClose={onClose} title="Adicionar combatentes" wide>
      <div className={styles.dialog}>
        <Segmented<Source> ariaLabel="Origem" value={source} onChange={setSource} options={[
          { value: 'pc', label: 'Personagem' },
          { value: 'npc', label: 'NPC salvo' },
          { value: 'generate', label: 'Gerar inimigos' },
          { value: 'generic', label: 'Genérico rápido' },
        ]} />

        {source === 'pc' && (
          <Field label="Personagem (ficha concluída)" htmlFor="add-pc" hint="Dano, ferimentos e munição vão direto para a ficha.">
            <Select id="add-pc" value={characterId} onChange={(e) => setCharacterId(e.target.value)}>
              <option value="">Escolha…</option>
              {pcs.map((c) => <option key={c.id} value={c.id}>{c.handle || c.name}{c.role ? ` (${c.role})` : ''}</option>)}
            </Select>
          </Field>
        )}

        {source === 'npc' && (
          <Field label="NPC salvo" htmlFor="add-npc">
            <Select id="add-npc" value={npcId} onChange={(e) => setNpcId(e.target.value)}>
              <option value="">Escolha…</option>
              {(npcs.data ?? []).map((n) => <option key={n.id} value={n.id}>{n.name}{n.concept ? ` — ${n.concept}` : ''}</option>)}
            </Select>
          </Field>
        )}

        {source === 'generate' && (
          <div className={styles.formGrid}>
            <Field label="Categoria" htmlFor="add-level">
              <Select id="add-level" value={level} onChange={(e) => setLevel(e.target.value as NpcLevel)}>
                {LEVELS.map((l) => <option key={l} value={l}>{LEVEL_LABEL[l]}</option>)}
              </Select>
            </Field>
            <Field label="Gangue" htmlFor="add-gang">
              <Select id="add-gang" value={gangId} onChange={(e) => setGangId(e.target.value)}>
                <option value="">Nenhuma</option>
                {(npcRules.data?.gangs ?? []).map((x) => <option key={x.id} value={x.id}>{x.label}</option>)}
              </Select>
            </Field>
            <Field label="Perfil" htmlFor="add-profile">
              <Select id="add-profile" value={profileId} onChange={(e) => setProfileId(e.target.value)}>
                <option value="">Aleatório</option>
                {(npcRules.data?.profiles ?? []).map((x) => <option key={x.id} value={x.id}>{x.label}</option>)}
              </Select>
            </Field>
            <div className={styles.row}><Switch checked={hardened} onChange={setHardened} label="Hardened" /></div>
          </div>
        )}

        {source === 'generic' && (
          <div className={styles.formGrid}>
            <Field label="Nome" htmlFor="add-name"><input id="add-name" value={name} maxLength={50} onChange={(e) => setName(e.target.value)} /></Field>
            <Field label="REF" htmlFor="add-ref"><NumberInput id="add-ref" min={1} max={12} value={ref} onChange={setRef} /></Field>
            <Field label="HP" htmlFor="add-hp"><NumberInput id="add-hp" min={1} max={200} value={hp} onChange={setHp} /></Field>
            <Field label="SP (corpo e cabeça)" htmlFor="add-sp"><NumberInput id="add-sp" min={0} max={25} value={sp} onChange={setSp} /></Field>
            <Field label="Ataque (STAT + perícia)" htmlFor="add-atk"><NumberInput id="add-atk" min={0} max={30} value={attack} onChange={setAttack} /></Field>
            <Field label="Evasion (DEX + perícia)" htmlFor="add-ev"><NumberInput id="add-ev" min={0} max={30} value={evasion} onChange={setEvasion} /></Field>
            <Field label="Dano da arma" htmlFor="add-dmg" hint={`Pistola pesada: ${g.damage}`}><input id="add-dmg" value={damage} onChange={(e) => setDamage(e.target.value)} /></Field>
          </div>
        )}

        {source !== 'pc' && (
          <div className={styles.row}>
            <Segmented<CombatSide> ariaLabel="Lado" value={side} onChange={setSide} options={[{ value: 'Enemy', label: 'Inimigo' }, { value: 'Ally', label: 'Aliado' }]} />
            <Field label="Quantidade" htmlFor="add-count"><NumberInput id="add-count" min={1} max={12} value={count} onChange={setCount} /></Field>
          </div>
        )}
        <span className={styles.source}>{rules.rules.generic.note}</span>
        <div className={styles.row}>
          <Button variant="primary" disabled={busy || !ready} onClick={add}>Adicionar</Button>
          <Button variant="ghost" onClick={onClose}>Cancelar</Button>
        </div>
      </div>
    </Modal>
  );
}
