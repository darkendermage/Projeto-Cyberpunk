import { useState } from 'react';
import { Modal } from '../../components/ui/ui';
import { t } from '../../i18n';
import { EmbeddedCharacter, type CharacterTab } from '../characters/CharacterPage';

// "Minha ficha": the full character screens, editable through the tunnel. The server only opens this
// character to the seat key, and changes reach the table right away.
export function MySheetDialog({ characterId, open, onClose, title, player = true }: {
  characterId: number; open: boolean; onClose: () => void;
  /** The GM opens any PC's sheet here too: the character's name, and the GM's full screen. */
  title?: string; player?: boolean;
}) {
  const [tab, setTab] = useState<CharacterTab>('ficha');
  return (
    <Modal open={open} onClose={onClose} title={title ?? t('Minha ficha', 'My sheet')} wide>
      {open && <EmbeddedCharacter id={characterId} tab={tab} onTab={setTab} player={player} />}
    </Modal>
  );
}
