import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useLocation, useNavigationType, useSearchParams } from 'react-router';
import { Skull, UserRound } from 'lucide-react';
import { useNpcCompendium, type NpcSummary } from '../../api/npcCompendium';
import { npcImageApi, npcImageKeys } from '../../api/npcImages';
import { Button, EmptyState, ErrorState, Field, Select, Spinner } from '../../components/ui/ui';
import page from '../../styles/Page.module.css';
import { StateTag } from '../characters/CharacterUi';
import styles from '../characters/Characters.module.css';
import { t } from '../../i18n';
import { filtersFromSearch, filtersToSearch, filterNpcs, npcBooks, npcRoles, type NpcFilters } from './filterNpcs';

export function NpcCompendiumPage() {
  const list = useNpcCompendium();
  const images = useQuery({ queryKey: npcImageKeys.compendium, queryFn: npcImageApi.compendiumStamps });
  const [params, setParams] = useSearchParams();
  const filters = useMemo(() => filtersFromSearch(params), [params]);
  // The box keeps its own text: the router updates the URL in a transition, too late for a controlled input. Only a
  // navigation from outside (the menu link, the browser's buttons) resets it; typing replaces the history entry.
  const [q, setQ] = useState(filters.q);
  const location = useLocation();
  const navigationType = useNavigationType();
  const [seenKey, setSeenKey] = useState(location.key);
  if (location.key !== seenKey) {
    setSeenKey(location.key);
    if (navigationType !== 'REPLACE') setQ(filters.q);
  }
  const set = (patch: Partial<NpcFilters>) => setParams(filtersToSearch({ ...filters, ...patch }), { replace: true });
  const clear = () => {
    setQ('');
    setParams({}, { replace: true });
  };

  const books = useMemo(() => npcBooks(list.data ?? []), [list.data]);
  const roles = useMemo(() => npcRoles(list.data ?? []), [list.data]);
  const shown = useMemo(() => filterNpcs(list.data ?? [], filters), [list.data, filters]);

  return (
    <div className={page.page}>
      <header>
        <h1 className="hud-title">{t('Compêndio de NPCs', 'NPC Compendium')}</h1>
        <p className={page.lead}>
          {t('Os NPCs nomeados dos livros, com a ficha oficial quando existe. Abra um para ver o bloco de estatísticas.', 'The named NPCs of the books, with the official stat block when there is one. Open one to see its stats.')}
        </p>
      </header>

      <div className={styles.formGrid}>
        <Field label={t('Buscar', 'Search')} htmlFor="npc-q">
          <input id="npc-q" className={styles.input} type="search" value={q}
            onChange={(e) => {
              setQ(e.target.value);
              set({ q: e.target.value });
            }}
            placeholder={t('Nome ou apelido', 'Name or alias')} />
        </Field>
        <Field label={t('Livro', 'Book')} htmlFor="npc-book">
          <Select id="npc-book" value={filters.book} onChange={(e) => set({ book: e.target.value })}>
            <option value="">{t('Todos', 'All')}</option>
            {books.map((b) => <option key={b} value={b}>{b}</option>)}
          </Select>
        </Field>
        <Field label="Role" htmlFor="npc-role">
          <Select id="npc-role" value={filters.role} onChange={(e) => set({ role: e.target.value })}>
            <option value="">{t('Todas', 'All')}</option>
            {roles.map((r) => <option key={r} value={r}>{r}</option>)}
          </Select>
        </Field>
        <Field label={t('Ficha', 'Sheet')} htmlFor="npc-sheet">
          <Select id="npc-sheet" value={filters.sheet} onChange={(e) => set({ sheet: e.target.value as NpcFilters['sheet'] })}>
            <option value="all">{t('Todos', 'All')}</option>
            <option value="sheet">{t('Com ficha oficial', 'With official sheet')}</option>
            <option value="profile">{t('Só perfil', 'Profile only')}</option>
          </Select>
        </Field>
      </div>

      {list.isLoading && <Spinner />}
      {list.error && <ErrorState error={list.error} onRetry={() => list.refetch()} />}
      {list.data && shown.length === 0 && (
        <EmptyState title={t('Nenhum NPC encontrado', 'No NPCs found')}
          action={<Button onClick={clear}>{t('Limpar filtros', 'Clear filters')}</Button>}>
          {t('Nenhum NPC bate com a busca e os filtros atuais.', 'No NPC matches the current search and filters.')}
        </EmptyState>
      )}
      {shown.length > 0 && (
        <div className={styles.cards}>
          {shown.map((n) => <NpcCard key={n.id} npc={n} image={images.data?.[n.id]} listSearch={location.search} />)}
        </div>
      )}
    </div>
  );
}

function NpcCard({ npc: n, image, listSearch }: { npc: NpcSummary; image?: string; listSearch: string }) {
  const isSheet = n.kind === 'Sheet';
  return (
    <Link to={`/npcs/${n.id}`} state={{ listSearch }} className={styles.card}>
      {image
        ? <img className={styles.avatar} src={npcImageApi.compendiumUrl(n.id, image)} alt="" loading="lazy" />
        : <div className={styles.avatar}>{isSheet ? <Skull size={34} /> : <UserRound size={34} />}</div>}
      <div className={styles.cardBody}>
        <span className={styles.cardName}>{n.name}</span>
        <div className={styles.cardMeta}>
          <span>{n.role ?? '—'}{n.roleRank ? ` · Rank ${n.roleRank}` : ''}</span>
          {n.rep != null && <span className={styles.muted}>Rep {n.rep}</span>}
        </div>
        {n.affiliation && <div className={styles.cardMeta}><span className={styles.muted}>{n.affiliation}</span></div>}
        <div className={styles.cardMeta}>
          {!isSheet && <StateTag tone="warn">{t('sem ficha oficial', 'no official sheet')}</StateTag>}
          <span className={styles.source}>{n.sources.map((s) => `${s.book} p.${s.page}`).join(' · ')}</span>
        </div>
      </div>
    </Link>
  );
}
