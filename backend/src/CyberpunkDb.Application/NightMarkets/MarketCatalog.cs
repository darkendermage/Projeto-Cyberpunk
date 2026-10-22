using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog;
using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.NightMarkets;

/// <summary>
/// What the market needs to know of a catalog item. <see cref="Type"/> is the category's own type (cyberware type,
/// weapon class, armor type, gear type, NET item type, drug classification...), as named by the enum.
/// </summary>
public sealed record MarketItem(
    int Id, string Slug, string Name, ItemCategory Category, string CategoryKey, int? Price, PriceCategory? PriceCategory,
    string? PriceNote, string? Book, int? Page, string? Manufacturer, string? Description, string? Type = null,
    string? Style = null, bool Exotic = false, bool Foundational = false, WeaponQuality? Quality = null, int? RequiresItemId = null);

public sealed class MarketCatalog
{
    public MarketCatalog(IEnumerable<MarketItem> items)
    {
        Items = items.ToList();
        ById = Items.ToDictionary(i => i.Id);
        BySlug = Items.GroupBy(i => i.Slug, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<MarketItem> Items { get; }
    public IReadOnlyDictionary<int, MarketItem> ById { get; }
    public IReadOnlyDictionary<string, MarketItem> BySlug { get; }

    public MarketItem? Find(string? slug) => slug is null ? null : BySlug.GetValueOrDefault(slug);
    public MarketItem? Find(int? id) => id is { } v ? ById.GetValueOrDefault(v) : null;

    /// <summary>Items matching a query, restricted to the enabled books unless the query names its own books or slugs.</summary>
    public IEnumerable<MarketItem> Query(ItemQuery query, IReadOnlySet<string>? books = null)
    {
        if (query.Slugs is { Length: > 0 } slugs)
            return slugs.Select(s => Find(s)).OfType<MarketItem>();
        return Items.Where(i => Matches(i, query) && (query.Books is { Length: > 0 } || books is null || (i.Book is { } b && books.Contains(b))));
    }

    public static bool Matches(MarketItem i, ItemQuery q)
    {
        if (q.Category is { } c && !string.Equals(i.CategoryKey, c, StringComparison.OrdinalIgnoreCase)) return false;
        if (q.ExceptCategories is { } xc && xc.Contains(i.CategoryKey, StringComparer.OrdinalIgnoreCase)) return false;
        if (q.Types is { Length: > 0 } t && (i.Type is null || !t.Contains(i.Type, StringComparer.OrdinalIgnoreCase))) return false;
        if (q.ExceptTypes is { } xt && i.Type is not null && xt.Contains(i.Type, StringComparer.OrdinalIgnoreCase)) return false;
        if (q.Styles is { Length: > 0 } s && (i.Style is null || !s.Contains(i.Style, StringComparer.OrdinalIgnoreCase))) return false;
        if (q.Exotic is { } ex && i.Exotic != ex) return false;
        if (q.Foundational is { } f && i.Foundational != f) return false;
        if (q.Price is { } p)
        {
            if (i.Price is not { } price) return false;
            if (p.Eq is { } eq && price != eq) return false;
            if (p.Min is { } min && price < min) return false;
            if (p.Max is { } max && price > max) return false;
        }
        if (q.Books is { Length: > 0 } books && !books.Any(b =>
                string.Equals(i.Book, b.Book, StringComparison.OrdinalIgnoreCase)
                && (b.MinPage is null || i.Page >= b.MinPage) && (b.MaxPage is null || i.Page <= b.MaxPage)))
            return false;
        return true;
    }
}

/// <summary>Loads the market view of the catalog, cached until the catalog changes (item count or latest UpdatedAt).</summary>
public sealed class MarketCatalogProvider(IAppDbContext db, CategoryRegistry registry, MarketCatalogCache cache)
{
    public async Task<MarketCatalog> GetAsync(CancellationToken ct)
    {
        var count = await db.Items.CountAsync(ct);
        var last = await db.Items.MaxAsync(i => (DateTime?)i.UpdatedAt, ct);
        var key = $"{count}:{last?.Ticks ?? 0}";
        if (cache.TryGet(key) is { } cached) return cached;

        var catalog = await LoadAsync(ct);
        cache.Set(key, catalog);
        return catalog;
    }

    private async Task<MarketCatalog> LoadAsync(CancellationToken ct)
    {
        var books = await db.SourceBooks.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var makers = await db.Manufacturers.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var types = new Dictionary<int, (string? Type, string? Style, bool Exotic, bool Foundational, WeaponQuality? Quality, int? Requires)>();

        foreach (var w in await db.Set<Weapon>().AsNoTracking().Select(x => new { x.Id, x.WeaponClass, x.IsExotic, x.Quality }).ToListAsync(ct))
            types[w.Id] = (w.WeaponClass.ToString(), null, w.IsExotic, false, w.Quality, null);
        foreach (var a in await db.Set<ArmorItem>().AsNoTracking().Select(x => new { x.Id, x.ArmorType, x.ClothingStyle }).ToListAsync(ct))
            types[a.Id] = (a.ArmorType.ToString(), a.ClothingStyle?.ToString(), false, false, null, null);
        foreach (var c in await db.Set<CyberwareItem>().AsNoTracking()
                     .Select(x => new { x.Id, x.CyberwareType, x.IsFoundational, x.RequiresItemId }).ToListAsync(ct))
            types[c.Id] = (c.CyberwareType.ToString(), null, false, c.IsFoundational, null, c.RequiresItemId);
        foreach (var g in await db.Set<GearItem>().AsNoTracking().Select(x => new { x.Id, x.GearType }).ToListAsync(ct))
            types[g.Id] = (g.GearType.ToString(), null, false, false, null, null);
        foreach (var n in await db.Set<NetrunningItem>().AsNoTracking().Select(x => new { x.Id, x.NetType }).ToListAsync(ct))
            types[n.Id] = (n.NetType.ToString(), null, false, false, null, null);
        foreach (var m in await db.Set<Ammunition>().AsNoTracking().Select(x => new { x.Id, x.Kind }).ToListAsync(ct))
            types[m.Id] = (m.Kind.ToString(), null, false, false, null, null);
        foreach (var x in await db.Set<WeaponAttachment>().AsNoTracking().Select(x => new { x.Id, x.AttachmentType }).ToListAsync(ct))
            types[x.Id] = (x.AttachmentType.ToString(), null, false, false, null, null);
        foreach (var d in await db.Set<Drug>().AsNoTracking().Select(x => new { x.Id, x.Classification }).ToListAsync(ct))
            types[d.Id] = (d.Classification.ToString(), null, false, false, null, null);
        foreach (var v in await db.Set<Vehicle>().AsNoTracking().Select(x => new { x.Id, x.VehicleType }).ToListAsync(ct))
            types[v.Id] = (v.VehicleType.ToString(), null, false, false, null, null);
        foreach (var u in await db.Set<VehicleUpgrade>().AsNoTracking().Select(x => new { x.Id, x.UpgradeType }).ToListAsync(ct))
            types[u.Id] = (u.UpgradeType.ToString(), null, false, false, null, null);
        foreach (var h in await db.Set<HqUpgrade>().AsNoTracking().Select(x => new { x.Id, x.UpgradeType }).ToListAsync(ct))
            types[h.Id] = (h.UpgradeType.ToString(), null, false, false, null, null);

        var rows = await db.Items.AsNoTracking()
            .Select(i => new
            {
                i.Id, i.Slug, i.Name, i.Category, i.PriceEb, i.PriceCategory, i.PriceNote, i.SourceBookId, i.Page,
                i.ManufacturerId, i.Description,
            })
            .ToListAsync(ct);

        return new MarketCatalog(rows.Select(r =>
        {
            var t = types.GetValueOrDefault(r.Id);
            return new MarketItem(r.Id, r.Slug, r.Name, r.Category, registry.Get(r.Category).Key, r.PriceEb, r.PriceCategory, r.PriceNote,
                r.SourceBookId is { } b ? books.GetValueOrDefault(b) : null, r.Page,
                r.ManufacturerId is { } m ? makers.GetValueOrDefault(m) : null, r.Description,
                t.Type, t.Style, t.Exotic, t.Foundational, t.Quality, t.Requires);
        }));
    }
}

public sealed class MarketCatalogCache
{
    private readonly object _gate = new();
    private (string Key, MarketCatalog Catalog)? _entry;

    public MarketCatalog? TryGet(string key)
    {
        lock (_gate) return _entry is { } e && e.Key == key ? e.Catalog : null;
    }

    public void Set(string key, MarketCatalog catalog)
    {
        lock (_gate) _entry = (key, catalog);
    }
}
