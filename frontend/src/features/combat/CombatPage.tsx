import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { useMutation } from '@tanstack/react-query';
import { Crosshair, Plus } from 'lucide-react';
import { problemMessage } from '../../api/characters';
import { encounterApi, useEncounterRules, useEncounters } from '../../api/encounters';
import { Badge, Button, EmptyState, ErrorState, Spinner } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import page from '../../styles/Page.module.css';
import { EncounterView } from './EncounterView';
import styles from './Combat.module.css';
import { t, tv } from '../../i18n';

export function CombatPage() {
  const rules = useEncounterRules();
  const { id } = useParams();
  if (rules.isLoading) return <div className={page.page}><Spinner /></div>;
  if (rules.error || !rules.data) return <div className={page.page}><ErrorState error={rules.error} onRetry={() => rules.refetch()} /></div>;
  return id ? <EncounterView id={Number(id)} rules={rules.data} /> : <Library />;
}

function Library() {
  const toast = useToast();
  const navigate = useNavigate();
  const list = useEncounters();
  const [name, setName] = useState('');
  const create = useMutation({
    mutationFn: () => encounterApi.create(name.trim() || null),
    onSuccess: (e) => navigate(`/combat/${e.id}`),
    onError: (error) => toast.notify(problemMessage(error), 'error'),
  });

  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1>Friday Night Firefight</h1>
        <p className={page.lead}>
          {t('A mesa de combate do Core (p.168–188): PCs e inimigos na mesma fila de iniciativa, ataques resolvidos com a DV do alcance, Autofire, tiro mirado, corpo a corpo com metade da armadura, cobertura com HP, Critical Injuries e Death Saves. O dano nos PCs vai direto para a ficha deles.', 'The Core combat table (p.168–188): PCs and enemies in the same Initiative Queue, attacks resolved against the range DV, Autofire, aimed shots, melee against half armor, cover with HP, Critical Injuries and Death Saves. Damage to PCs goes straight to their sheets.')}
        </p>
      </header>

      <section className={styles.panel}>
        <h2 className={styles.panelTitle}>{t('Novo combate', 'New fight')}</h2>
        <div className={styles.row}>
          <input aria-label={t('Nome do combate', 'Fight name')} value={name} maxLength={80} onChange={(e) => setName(e.target.value)} placeholder={t('Ex.: Emboscada no Totentanz', 'E.g. Ambush at the Totentanz')} />
          <Button variant="primary" icon={create.isPending ? <Spinner /> : <Plus size={15} />} disabled={create.isPending} onClick={() => create.mutate()}>
            {t('Abrir mesa', 'Open table')}
          </Button>
        </div>
      </section>

      <section className={page.section}>
        <h2>{t('Combates salvos', 'Saved fights')}</h2>
        {list.isLoading ? <Spinner /> : (list.data ?? []).length === 0 ? (
          <EmptyState title={t('Nenhum combate ainda', 'No fights yet')} icon={<Crosshair size={30} />}>{t('Abra uma mesa acima.', 'Open a table above.')}</EmptyState>
        ) : (
          <div className={styles.library}>
            {list.data!.map((e) => (
              <Link key={e.id} to={`/combat/${e.id}`} className={styles.card}>
                <h3>{tv(e.name)}</h3>
                <span className={styles.muted}>{e.allies} {t('aliado(s) · ', 'ally(ies) · ')}{e.enemies} {t('inimigo(s)', 'enemy(ies)')}{e.down > 0 ? t(` · ${e.down} fora de combate`, ` · ${e.down} out of the fight`) : ''}</span>
                <span className={styles.row}>
                  <Badge tone={e.status === 'Active' ? 'red' : 'default'}>{e.status === 'Active' ? t(`Rodada ${e.round}`, `Round ${e.round}`) : t('Encerrado', 'Ended')}</Badge>
                </span>
              </Link>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
