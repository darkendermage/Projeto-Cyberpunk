using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Npcs;

public sealed record WeaponInfo(
    WeaponClass Class, WeaponSkill Skill, WeaponQuality Quality, bool Exotic, int? MinBody,
    string? Damage, int? Rof, int Hands, bool Concealable, int? Magazine, IReadOnlyList<Caliber> Calibers,
    int? AutofireMultiplier = null);

public sealed record ArmorInfo(
    ArmorType Type, int? Sp, int RefPenalty, int DexPenalty, int MovePenalty, int? BodyValue, bool Implanted, string? Requirements);

/// <summary>Cyberware (Fashionware included) and Implanted Linear Frames: everything paid from the cyberware budget.</summary>
public sealed record CyberInfo(
    CyberwareType? Type, BodyLocation? Location, bool Foundational, int? SlotsProvided, int? SlotsRequired,
    int? RequiresItemId, bool RequiresPair, InstallLocation? Install, string? Requirements,
    int? HumanityLoss = null, string? HumanityLossDice = null)
{
    /// <summary>Counts against the Fashion budget of a Complete Package (CP:R p.105).</summary>
    public bool IsFashionware => Type == CyberwareType.Fashionware;
}

public sealed record AmmoInfo(AmmoKind Kind, AmmoEffect Effect, IReadOnlyList<Caliber> Calibers, int? PerPack);

public sealed record GearInfo(GearType Type, string? Subtype);

/// <summary>Weapon attachment, Rebuild or Tech Upgrade: where it fits and how many Attachment Slots it takes.</summary>
public sealed record AttachmentInfo(
    WeaponAttachmentType Type, int SlotsRequired, bool ExoticAllowed, IReadOnlyList<WeaponClass> Classes, string? Conditions);

/// <summary>Light, EF-free view of a catalog item, enough to build and validate an NPC.</summary>
public sealed record CatalogItem(
    int Id, string Slug, string Name, ItemCategory Category, int? Price, string? SourceBook, int? Page,
    WeaponInfo? Weapon = null, ArmorInfo? Armor = null, CyberInfo? Cyber = null, AmmoInfo? Ammo = null, GearInfo? Gear = null,
    AttachmentInfo? Attachment = null)
{
    /// <summary>Paid from the cyberware budget and listed in the sheet's cyberware section.</summary>
    public bool IsCyberwareBudget => Cyber is not null;
}

public sealed class NpcCatalog
{
    public NpcCatalog(IEnumerable<CatalogItem> items)
    {
        var list = items.ToList();
        ById = list.ToDictionary(i => i.Id);
        BySlug = list.GroupBy(i => i.Slug, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        Items = list;
    }

    public IReadOnlyList<CatalogItem> Items { get; }
    public IReadOnlyDictionary<int, CatalogItem> ById { get; }
    public IReadOnlyDictionary<string, CatalogItem> BySlug { get; }

    public CatalogItem? Find(int id) => ById.GetValueOrDefault(id);
    public CatalogItem? Find(string slug) => BySlug.GetValueOrDefault(slug);
}

/// <summary>
/// Loads the catalog snapshot used by the NPC services. The snapshot is cached and reloaded only when
/// the catalog changes (item count or latest UpdatedAt).
/// </summary>
public sealed class NpcCatalogProvider(IAppDbContext db, NpcCatalogCache cache)
{
    public async Task<NpcCatalog> GetAsync(CancellationToken ct)
    {
        var count = await db.Items.CountAsync(ct);
        var last = await db.Items.MaxAsync(i => (DateTime?)i.UpdatedAt, ct);
        var key = $"{count}:{last?.Ticks ?? 0}";
        if (cache.TryGet(key) is { } cached) return cached;

        var catalog = await LoadAsync(ct);
        cache.Set(key, catalog);
        return catalog;
    }

    private async Task<NpcCatalog> LoadAsync(CancellationToken ct)
    {
        var books = await db.SourceBooks.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        string? Book(int? id) => id is { } v ? books.GetValueOrDefault(v) : null;
        var items = new List<CatalogItem>();

        foreach (var w in await db.Set<Weapon>().AsNoTracking().Include(x => x.Calibers).ToListAsync(ct))
            items.Add(new CatalogItem(w.Id, w.Slug, w.Name, w.Category, w.PriceEb, Book(w.SourceBookId), w.Page,
                Weapon: new WeaponInfo(w.WeaponClass, w.Skill, w.Quality, w.IsExotic, w.MinBody, w.Damage, w.Rof, w.Hands,
                    w.Concealable, w.MagazineCapacity, w.Calibers.Select(c => c.Caliber).ToList(), w.AutofireMultiplier)));

        foreach (var a in await db.Set<ArmorItem>().AsNoTracking().ToListAsync(ct))
        {
            var implanted = a.ArmorType == ArmorType.LinearFrame && a.InstallLocation is not null;
            items.Add(new CatalogItem(a.Id, a.Slug, a.Name, a.Category, a.PriceEb, Book(a.SourceBookId), a.Page,
                Armor: new ArmorInfo(a.ArmorType, a.StoppingPower, a.RefPenalty ?? 0, a.DexPenalty ?? 0, a.MovePenalty ?? 0,
                    a.BodyValue, implanted, a.Requirements),
                Cyber: implanted
                    ? new CyberInfo(CyberwareType.Borgware, BodyLocation.FullBody, false, null, null, null, false, a.InstallLocation, a.Requirements,
                        a.HumanityLoss, a.HumanityLossDice)
                    : null));
        }

        foreach (var c in await db.Set<CyberwareItem>().AsNoTracking().ToListAsync(ct))
            items.Add(new CatalogItem(c.Id, c.Slug, c.Name, c.Category, c.PriceEb, Book(c.SourceBookId), c.Page,
                Cyber: new CyberInfo(c.CyberwareType, c.BodyLocation, c.IsFoundational, c.SlotsProvided, c.SlotsRequired,
                    c.RequiresItemId, c.RequiresPair, c.InstallLocation, c.Requirements, c.HumanityLoss, c.HumanityLossDice)));

        foreach (var m in await db.Set<Ammunition>().AsNoTracking().Include(x => x.Calibers).ToListAsync(ct))
            items.Add(new CatalogItem(m.Id, m.Slug, m.Name, m.Category, m.PriceEb, Book(m.SourceBookId), m.Page,
                Ammo: new AmmoInfo(m.Kind, m.EffectType, m.Calibers.Select(c => c.Caliber).ToList(), m.QuantityPerPack)));

        foreach (var g in await db.Set<GearItem>().AsNoTracking().ToListAsync(ct))
            items.Add(new CatalogItem(g.Id, g.Slug, g.Name, g.Category, g.PriceEb, Book(g.SourceBookId), g.Page,
                Gear: new GearInfo(g.GearType, g.Subtype)));

        foreach (var x in await db.Set<WeaponAttachment>().AsNoTracking().Include(x => x.CompatibleClasses).ToListAsync(ct))
            items.Add(new CatalogItem(x.Id, x.Slug, x.Name, x.Category, x.PriceEb, Book(x.SourceBookId), x.Page,
                Attachment: new AttachmentInfo(x.AttachmentType, x.SlotsRequired ?? 0, x.ExoticAllowed,
                    x.CompatibleClasses.Select(c => c.WeaponClass).ToList(), x.EligibleWeapons)));

        // Remaining categories (drugs, vehicles...) can be carried as plain gear.
        var known = new[] { ItemCategory.Weapon, ItemCategory.Armor, ItemCategory.Cyberware, ItemCategory.Ammunition, ItemCategory.Gear, ItemCategory.WeaponAttachment };
        foreach (var o in await db.Items.AsNoTracking().Where(i => !known.Contains(i.Category))
                     .Select(i => new { i.Id, i.Slug, i.Name, i.Category, i.PriceEb, i.SourceBookId, i.Page }).ToListAsync(ct))
            items.Add(new CatalogItem(o.Id, o.Slug, o.Name, o.Category, o.PriceEb, Book(o.SourceBookId), o.Page));

        return new NpcCatalog(items);
    }
}

public sealed class NpcCatalogCache
{
    private readonly object _gate = new();
    private (string Key, NpcCatalog Catalog)? _entry;

    public NpcCatalog? TryGet(string key)
    {
        lock (_gate) return _entry is { } e && e.Key == key ? e.Catalog : null;
    }

    public void Set(string key, NpcCatalog catalog)
    {
        lock (_gate) _entry = (key, catalog);
    }
}
