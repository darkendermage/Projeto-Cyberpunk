import { problemMessage, useCharacterCommand } from '../../api/characters';
import type { CharacterDetail, CharacterRules } from '../../api/characterTypes';
import { useToast } from '../../state/ToastContext';
import type { Run } from './CombatPanels';
import { LifepathHero } from './LifepathHero';
import { LifepathOverview } from './LifepathOverview';
import { RollMissingButton } from './LifepathPanels';
import type { SheetEditor } from './useSheetEditor';
import lp from './Lifepath.module.css';

interface Props { detail: CharacterDetail; rules: CharacterRules; editor: SheetEditor }

export function LifepathView({ detail, rules, editor }: Props) {
  const toast = useToast();
  const command = useCharacterCommand(detail.id);
  const run: Run = async (path, body = {}, success) => {
    await editor.flush();
    try {
      const result = await command.mutateAsync({ path, body });
      if (success) toast.notify(success);
      return result;
    } catch (error) {
      toast.notify(problemMessage(error), 'error');
      return null;
    }
  };

  return (
    <div className={lp.page}>
      <LifepathHero detail={detail} rules={rules} sheet={editor.draft} actions={<RollMissingButton rules={rules} editor={editor} />} />
      <LifepathOverview detail={detail} rules={rules} editor={editor} run={run} busy={command.isPending} />
    </div>
  );
}
