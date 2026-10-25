using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.NetArchitectures;

/// <summary>A Black ICE from the catalog with the stats a GM needs at the table (CP:R p.204-205).</summary>
public sealed record IceInfo(
    int Id, string Slug, string Name, int? Price, PriceCategory? PriceCategory, ProgramClass? Class,
    int? Per, int? Spd, int? Atk, int? Def, int? Rez, string? Effect, string? Book, int? Page);

/// <summary>Snapshot of the catalog's Black ICE, keyed by slug.</summary>
public sealed class NetIceCatalog(IEnumerable<IceInfo> ice)
{
    private readonly Dictionary<string, IceInfo> _bySlug = ice.ToDictionary(i => i.Slug, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IceInfo> All => _bySlug.Values;
    public IceInfo? Find(string? slug) => slug is null ? null : _bySlug.GetValueOrDefault(slug);
}

public sealed class NetIceCatalogCache
{
    private readonly object _gate = new();
    private (string Key, NetIceCatalog Catalog)? _entry;

    public NetIceCatalog? TryGet(string key)
    {
        lock (_gate) return _entry is { } e && e.Key == key ? e.Catalog : null;
    }

    public void Set(string key, NetIceCatalog catalog)
    {
        lock (_gate) _entry = (key, catalog);
    }
}

public sealed class NetIceCatalogProvider(IAppDbContext db, NetIceCatalogCache cache)
{
    public async Task<NetIceCatalog> GetAsync(CancellationToken ct)
    {
        var ice = db.Set<NetrunningItem>().Where(n => n.NetType == NetItemType.BlackIce);
        var count = await ice.CountAsync(ct);
        var last = await ice.MaxAsync(i => (DateTime?)i.UpdatedAt, ct);
        var key = $"{count}:{last?.Ticks ?? 0}";
        if (cache.TryGet(key) is { } cached) return cached;

        var books = await db.SourceBooks.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var rows = await ice.AsNoTracking()
            .Select(n => new { n.Id, n.Slug, n.Name, n.PriceEb, n.PriceCategory, n.ProgramClass, n.Perception, n.Speed, n.Attack, n.Defense, n.Rez, n.Effect, n.SourceBookId, n.Page })
            .ToListAsync(ct);
        var catalog = new NetIceCatalog(rows.Select(n => new IceInfo(n.Id, n.Slug, n.Name, n.PriceEb, n.PriceCategory, n.ProgramClass,
            n.Perception, n.Speed, n.Attack, n.Defense, n.Rez, n.Effect,
            n.SourceBookId is { } b ? books.GetValueOrDefault(b) : null, n.Page)));
        cache.Set(key, catalog);
        return catalog;
    }
}
