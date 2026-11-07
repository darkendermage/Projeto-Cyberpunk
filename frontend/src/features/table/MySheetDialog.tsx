import { useState } from 'react';
import { Modal } from '../../components/ui/ui';
import { t } from '../../i18n';
import { EmbeddedCharacter, type CharacterTab } from '../characters/CharacterPage';

/**
 * "Minha ficha": the player's own character with every screen of the app (Character, Lifepath, Gear, Cyberware),
 * editable through the tunnel. The server opens only this character to the seat's key; what changes here reaches the
 * table at once (the sheet's broadcast).
 */
export function MySheetDialog({ characterId, open, onClose }: { characterId: number; open: boolean; onClose: () => void }) {
  const [tab, setTab] = useState<CharacterTab>('ficha');
  return (
    <Modal open={open} onClose={onClose} title={t('Minha ficha', 'My sheet')} wide>
      {open && <EmbeddedCharacter id={characterId} tab={tab} onTab={setTab} player />}
    </Modal>
  );
}
