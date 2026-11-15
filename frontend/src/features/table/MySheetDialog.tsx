import { useMemo, useState } from 'react';
import { Modal } from '../../components/ui/ui';
import { t } from '../../i18n';
import { EmbeddedCharacter, type CharacterTab } from '../characters/CharacterPage';
import { SheetRollContext, skillFormula, type SheetRoll } from '../characters/sheetRoll';

// "Minha ficha": the full character screens, editable through the tunnel. The server only opens this
// character to the seat key, and changes reach the table right away.
export function MySheetDialog({ characterId, open, onClose, title, player = true, onRoll }: {
  characterId: number; open: boolean; onClose: () => void;
  /** The GM opens any PC's sheet here too: the character's name, and the GM's full screen. */
  title?: string; player?: boolean;
  /** A clicked skill rolls a Check in the table chat. */
  onRoll?: (formula: string, label: string, luck: number, reroll: boolean) => Promise<unknown>;
}) {
  const [tab, setTab] = useState<CharacterTab>('ficha');
  const roller = useMemo<SheetRoll | null>(() => (onRoll
    ? { rollSkill: async (label, total, luck = 0, reroll = false) => { await onRoll(skillFormula(total), label, luck, reroll); } }
    : null), [onRoll]);
  return (
    <Modal open={open} onClose={onClose} title={title ?? t('Minha ficha', 'My sheet')} full>
      <SheetRollContext.Provider value={roller}>
        {open && <EmbeddedCharacter id={characterId} tab={tab} onTab={setTab} player={player} />}
      </SheetRollContext.Provider>
    </Modal>
  );
}
