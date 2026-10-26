import { useState } from 'react';
import { Link } from 'react-router';
import { Star } from 'lucide-react';
import { useFavorites } from '../../api/hooks';
import { useSchemas } from '../../api/useSchemas';
import { EmptyState, ErrorState, Spinner } from '../../components/ui/ui';
import { ItemCard } from '../catalog/ItemCard';
import { ItemDetailModal } from '../catalog/ItemDetailModal';
import catalogStyles from '../catalog/CatalogPage.module.css';
import page from '../../styles/Page.module.css';
import { t } from '../../i18n';

export function FavoritesPage() {
  const favorites = useFavorites();
  const schemas = useSchemas(favorites.data?.map((i) => i.category) ?? []);
  const [openId, setOpenId] = useState<number | null>(null);

  return (
    <div className={page.page}>
      <header className={page.header}>
        <h1 className="hud-title" data-glitch={t('Favoritos', 'Favorites')}>{t('Favoritos', 'Favorites')}</h1>
        <p className={page.lead}>{t('Itens marcados com a estrela, de qualquer categoria.', 'Items marked with the star, from any category.')}</p>
      </header>

      {favorites.isLoading && <Spinner />}
      {favorites.isError && <ErrorState error={favorites.error} onRetry={() => favorites.refetch()} />}
      {favorites.data?.length === 0 && (
        <EmptyState title={t('Nenhum favorito ainda', 'No favorites yet')} icon={<Star size={28} />}
          action={<Link to="/catalog/weapons" className={page.link}>{t('Explorar o catálogo', 'Explore the catalog')}</Link>}>
          {t('Use a estrela nos cards ou na ficha de um item para guardá-lo aqui.', 'Use the star on the cards or on an item\'s page to keep it here.')}
        </EmptyState>
      )}
      {favorites.data && favorites.data.length > 0 && (
        <div className={catalogStyles.grid}>
          {favorites.data.map((item) => (
            <ItemCard key={item.id} item={item} schema={schemas.get(item.category)} onOpen={(i) => setOpenId(i.id)} />
          ))}
        </div>
      )}
      <ItemDetailModal itemId={openId} onClose={() => setOpenId(null)} />
    </div>
  );
}
