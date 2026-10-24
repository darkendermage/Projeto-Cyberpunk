import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router';
import { PAGE_SIZES } from '../../api/hooks';
import { type Criterion, decodeCriterion, encodeCriterion } from '../../lib/filters';
import { readStorage, writeStorage } from '../../lib/hooks';

export type ViewMode = 'table' | 'grid';

export interface CatalogStatePatch {
  q?: string;
  criteria?: Criterion[];
  sort?: string | null;
  dir?: 'asc' | 'desc';
  view?: ViewMode;
  item?: number | null;
  page?: number;
  per?: number;
}

const PREFS_KEY = 'ncdb.catalog';

interface Prefs {
  view: ViewMode | null;
  per: number;
}

const prefs = () => readStorage<Prefs>(PREFS_KEY, { view: null, per: PAGE_SIZES[0] });

/** Phones get cards by default; desktops get the table. An explicit choice is remembered. */
function defaultView(): ViewMode {
  const stored = prefs().view;
  if (stored === 'table' || stored === 'grid') return stored;
  return typeof window !== 'undefined' && window.matchMedia('(max-width: 720px)').matches ? 'grid' : 'table';
}

/**
 * Catalog state lives in the URL (?q=&f=&sort=&dir=&view=&page=&per=&item=) so filtered views can be
 * bookmarked, shared and navigated with the browser history.
 */
export function useCatalogState() {
  const [params, setParams] = useSearchParams();
  const raw = params.toString();

  const state = useMemo(() => {
    const p = new URLSearchParams(raw);
    const criteria = p.getAll('f').map(decodeCriterion).filter((c): c is Criterion => c !== null);
    const item = Number(p.get('item'));
    const page = Number(p.get('page'));
    const per = Number(p.get('per')) || prefs().per;
    const viewParam = p.get('view');
    const view: ViewMode = viewParam === 'grid' ? 'grid' : viewParam === 'table' || viewParam === 'list' ? 'table' : defaultView();
    return {
      q: p.get('q') ?? '',
      criteria,
      sort: p.get('sort') ?? undefined,
      dir: (p.get('dir') === 'desc' ? 'desc' : 'asc') as 'asc' | 'desc',
      view,
      page: Number.isInteger(page) && page > 0 ? page : 1,
      per: PAGE_SIZES.includes(per) ? per : PAGE_SIZES[0],
      itemId: Number.isInteger(item) && item > 0 ? item : null,
    };
  }, [raw]);

  const update = useCallback((patch: CatalogStatePatch, options: { push?: boolean } = {}) => {
    setParams((current) => {
      const next = new URLSearchParams(current);
      // Any change to what is being searched starts again from the first page.
      const resetsPage = patch.q !== undefined || patch.criteria !== undefined || patch.sort !== undefined ||
        patch.dir !== undefined || patch.per !== undefined;

      if (patch.q !== undefined) {
        if (patch.q.trim()) next.set('q', patch.q);
        else next.delete('q');
      }
      if (patch.criteria !== undefined) {
        next.delete('f');
        for (const c of patch.criteria) next.append('f', encodeCriterion(c));
      }
      if (patch.sort !== undefined) {
        if (patch.sort) next.set('sort', patch.sort);
        else next.delete('sort');
      }
      if (patch.dir !== undefined) {
        if (patch.dir === 'desc') next.set('dir', 'desc');
        else next.delete('dir');
      }
      if (patch.view !== undefined) {
        next.set('view', patch.view);
        writeStorage(PREFS_KEY, { ...prefs(), view: patch.view });
      }
      if (patch.per !== undefined) {
        next.set('per', String(patch.per));
        writeStorage(PREFS_KEY, { ...prefs(), per: patch.per });
      }
      if (patch.page !== undefined && patch.page > 1) next.set('page', String(patch.page));
      else if (patch.page !== undefined || resetsPage) next.delete('page');
      if (patch.item !== undefined) {
        if (patch.item) next.set('item', String(patch.item));
        else next.delete('item');
      }
      return next;
    }, { replace: !options.push });
  }, [setParams]);

  return { ...state, update };
}
