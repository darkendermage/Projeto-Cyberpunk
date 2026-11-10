// Body of the Caminho de Vida tab: quick profile on the left, the Lifepath itself in collapsible sections in the
// middle, relationships and effects on the right.
import type { ReactNode } from 'react';
import { BookOpen, ChevronDown, Drama, HeartCrack, Home, Target, UserRound, Users } from 'lucide-react';
import type { CharacterDetail, CharacterRules } from '../../api/characterTypes';
import type { Run } from './CombatPanels';
import { LIFEPATH_GROUPS, type LifepathGroupId } from './lifepath';
import { CompactRelationsPanel, RoleLifepathPanels, TableGroupFields } from './LifepathPanels';
import {
  ConditionPanel, EffectsPanel, HistoryField, HousingFields, NotesPanel, OriginsFields, QuickProfile, StylePanel,
} from './LifepathSections';
import { useSessionState } from './useSessionState';
import type { SheetEditor } from './useSheetEditor';
import lp from './Lifepath.module.css';
import { t, tv } from '../../i18n';

interface Props { detail: CharacterDetail; rules: CharacterRules; editor: SheetEditor; run: Run; busy: boolean }

const GROUP_ICON: Record<LifepathGroupId, ReactNode> = {
  personality: <Drama size={16} />, motivations: <Target size={16} />, family: <Users size={16} />,
};

export function LifepathOverview({ detail, rules, editor, run, busy }: Props) {
  const [open, setOpen] = useSessionState<string[]>('lifepath:open', []);
  const toggle = (id: string) => setOpen(open.includes(id) ? open.filter((x) => x !== id) : [...open, id]);
  // "Editar" on the quick profile: open the identity section and bring it into view.
  const editIdentity = () => {
    if (!open.includes('identity')) setOpen([...open, 'identity']);
    requestAnimationFrame(() => document.getElementById(accordionId('identity'))?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
  };
  const section = (id: string, icon: ReactNode, title: string, subtitle: string, source: string | null, body: ReactNode) => (
    <Accordion key={id} id={id} icon={icon} title={title} subtitle={subtitle} source={source} open={open.includes(id)} onToggle={() => toggle(id)}>
      {body}
    </Accordion>
  );
  const groupSource = rules.relations.source;

  return (
    <div className={lp.overview}>
      <div className={lp.column}>
        <QuickProfile detail={detail} rules={rules} editor={editor} onEdit={editIdentity} />
        <ConditionPanel detail={detail} />
        <StylePanel detail={detail} rules={rules} editor={editor} />
      </div>
      <div className={lp.column}>
        <RoleLifepathPanels rules={rules} editor={editor} />
        {section('identity', <UserRound size={16} />, t('Identidade e origens', 'Identity and origins'), t('Origem, idiomas e reputação', 'Origin, languages and reputation'), groupSource,
          <OriginsFields detail={detail} rules={rules} editor={editor} />)}
        {LIFEPATH_GROUPS.map((g) => section(g.id, GROUP_ICON[g.id], tv(g.title), g.subtitle, groupSource,
          <TableGroupFields rules={rules} editor={editor} group={g.id} />))}
        {section('history', <BookOpen size={16} />, t('Histórico pessoal', 'Personal history'), t('Eventos importantes antes de Night City', 'Key events before Night City'), null,
          <HistoryField editor={editor} />)}
        {section('housing', <Home size={16} />, t('Moradia e lifestyle', 'Housing and lifestyle'), t('Custo, aluguel e estilo de vida', 'Cost, rent and lifestyle'), detail.report.housing.source,
          <HousingFields detail={detail} rules={rules} editor={editor} run={run} busy={busy} />)}
      </div>
      <div className={lp.column}>
        <CompactRelationsPanel rules={rules} editor={editor} kinds={['Friend', 'Enemy', 'Ally', 'Contact']}
          title={t('Relações', 'Relationships')} icon={<Users size={16} />} />
        <CompactRelationsPanel rules={rules} editor={editor} kinds={['Lover']}
          title={t('Tragédias amorosas', 'Tragic love affairs')} icon={<HeartCrack size={16} />} />
        <EffectsPanel detail={detail} />
        <NotesPanel editor={editor} />
      </div>
    </div>
  );
}

const accordionId = (id: string) => `lp-accordion-${id}`;

function Accordion({ id, icon, title, subtitle, source, open, onToggle, children }: {
  id: string; icon: ReactNode; title: string; subtitle: string; source: string | null; open: boolean; onToggle: () => void; children: ReactNode;
}) {
  const bodyId = `lp-section-${id}`;
  return (
    <section id={accordionId(id)} className={lp.accordion} data-open={open || undefined}>
      <h3 className={lp.accordionHeading}>
        <button type="button" className={lp.accordionHead} aria-expanded={open} aria-controls={bodyId} onClick={onToggle}>
          <span className={lp.accordionIcon}>{icon}</span>
          <span className={lp.accordionTitle}>{title}</span>
          <span className={lp.accordionSubtitle}>{subtitle}</span>
          {source && <span className={lp.seal}>{source}</span>}
          <ChevronDown size={16} className={lp.accordionChevron} aria-hidden />
        </button>
      </h3>
      {open && <div id={bodyId} className={lp.accordionBody}>{children}</div>}
    </section>
  );
}
