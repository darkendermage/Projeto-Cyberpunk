import clsx from 'clsx';
import { Copy, FolderOpen, Trash2 } from 'lucide-react';
import type { NpcRules, NpcSheet, NpcSummary, NpcValidationReport } from '../../api/types';
import { Badge, IconButton } from '../../components/ui/ui';
import { LEVEL_LABEL, STAT_KEYS } from './npcDraft';
import styles from './EnemyGenerator.module.css';

const QUALITY_SHORT = { Poor: 'PQ ', Standard: '', Excellent: 'EQ ' } as const;

/** Final sheet in the Danger Gal Dossier stat block layout (DGD p.7): STATs, weapons, armor, Skill Bases, gear, cyberware. */
export function SheetPreview({ rules, sheet, report }: { rules: NpcRules; sheet: NpcSheet; report: NpcValidationReport }) {
  const d = report.derived;
  const profile = rules.profiles.find((p) => p.id === sheet.profileId);
  const weapons = report.gearLines.filter((l) => l.category === 'Weapon');
  const armor = report.gearLines.filter((l) => l.worn);
  const other = report.gearLines.filter((l) => l.category !== 'Weapon' && !l.worn);
  const bases = report.skillLines.filter((l) => l.level > 0).sort((a, b) => a.name.localeCompare(b.name));
  const roots = report.cyberLines.filter((l) => !l.parentInstanceId);
  const optionsOf = (id: string) => report.cyberLines.filter((l) => l.parentInstanceId === id).map((l) => l.name);

  return (
    <article className={styles.sheet} aria-label="Pré-visualização da ficha">
      <div className={styles.sheetName}>{sheet.name || 'Sem nome'}</div>
      <div className={styles.sheetConcept}>{sheet.concept ?? profile?.label ?? '—'}</div>
      <div className={styles.sheetRow}>
        <span><b>Level</b> {sheet.hardened ? 'Hardened ' : ''}{LEVEL_LABEL[sheet.level]}</span>
        <span><b>Role</b> {sheet.role ? `${sheet.role} ${sheet.roleRank}` : '—'}</span>
        <span><b>HP</b> {d.hp}</span>
        <span><b>Seriously Wounded</b> {d.seriouslyWounded}</span>
        <span><b>Death Save</b> {d.deathSave}</span>
      </div>
      <div className={styles.sheetStats}>
        {STAT_KEYS.map((k) => {
          const base = sheet.stats[k] ?? 0;
          const armored = d.effectiveStats[k];
          return (
            <div key={k}>
              <span>{k}</span>
              <strong>{base}</strong>
              {armored !== undefined && armored !== base && <em> ({armored})</em>}
            </div>
          );
        })}
      </div>
      <div className={styles.sheetSection}>
        <h4>Armas</h4>
        {weapons.length === 0 ? 'Brawling' : weapons.map((w) => (
          <div key={w.index}>{QUALITY_SHORT[w.quality ?? 'Standard']}{w.name}{w.summary ? ` · ${w.summary}` : ''}</div>
        ))}
      </div>
      <div className={styles.sheetSection}>
        <h4>Armadura</h4>
        {armor.length === 0 && d.spBody === 0 ? 'Nenhuma' : (
          <>
            {armor.map((a) => <div key={a.index}>{a.worn === 'Body' ? 'Corpo' : 'Cabeça'}: {a.name} {a.summary ? `(${a.summary})` : ''}</div>)}
            <div>SP corpo {d.spBody} · cabeça {d.spHead}</div>
          </>
        )}
      </div>
      <div className={styles.sheetSection}>
        <h4>Skill Bases</h4>
        {bases.map((s) => `${s.name}${s.specialization ? ` (${s.specialization})` : ''} ${s.base}`).join(' · ')}
      </div>
      {other.length > 0 && (
        <div className={styles.sheetSection}>
          <h4>Equipamento</h4>
          {other.map((g) => `${g.name}${g.quantity > 1 ? ` x${g.quantity}` : ''}`).join(' · ')}
        </div>
      )}
      <div className={styles.sheetSection}>
        <h4>Cyberware</h4>
        {roots.length === 0 ? 'Nenhum' : roots.map((r) => {
          const opts = optionsOf(r.instanceId);
          return `${r.name}${opts.length ? ` com ${opts.join(', ')}` : ''}`;
        }).join(' · ')}
      </div>
      {sheet.exceptions.length > 0 && (
        <div className={styles.sheetSection}>
          <h4>Exceções do Mestre</h4>
          {sheet.exceptions.map((e) => <div key={e.key}>{e.justification}</div>)}
        </div>
      )}
    </article>
  );
}

const STATUS_LABEL = { Draft: 'Rascunho', Valid: 'Válido', ValidWithExceptions: 'Com exceções' } as const;
const STATUS_TONE = { Draft: 'red', Valid: 'cyan', ValidWithExceptions: 'amber' } as const;

export function NpcLibrary({
  npcs, currentId, onOpen, onDuplicate, onDelete,
}: {
  npcs: NpcSummary[];
  currentId: number | null;
  onOpen: (id: number) => void;
  onDuplicate: (id: number) => void;
  onDelete: (npc: NpcSummary) => void;
}) {
  if (npcs.length === 0) return <p className={styles.muted}>Nenhum NPC salvo ainda.</p>;
  return (
    <div className={styles.library}>
      {npcs.map((n) => (
        <div key={n.id} className={clsx(styles.libraryItem, n.id === currentId && styles.libraryItemActive)}>
          <button type="button" className={styles.libraryOpen} onClick={() => onOpen(n.id)}>
            {n.name} <Badge tone={STATUS_TONE[n.status]}>{STATUS_LABEL[n.status]}</Badge>
            <span>{n.hardened ? 'Hardened ' : ''}{LEVEL_LABEL[n.level]}{n.concept ? ` · ${n.concept}` : ''}</span>
          </button>
          <div className={styles.inline}>
            <IconButton label="Abrir" onClick={() => onOpen(n.id)}><FolderOpen size={14} /></IconButton>
            <IconButton label="Duplicar" onClick={() => onDuplicate(n.id)}><Copy size={14} /></IconButton>
            <IconButton label="Excluir" onClick={() => onDelete(n)}><Trash2 size={14} /></IconButton>
          </div>
        </div>
      ))}
    </div>
  );
}
