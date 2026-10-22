using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NightMarkets.Generation;

/// <summary>
/// Adds stalls, listings and open slots to a market, shared by the generator and the later GM commands. Applies the
/// two placement rules of CP:R p.338: every Quality Rating of a rolled item is present, and an Option brings the
/// Foundational piece it needs.
/// </summary>
internal sealed class ListingBuilder(NightMarketRuleBook rules, MarketCatalog catalog, string letter, List<MarketStall> stalls,
    List<MarketListing> listings, List<OpenSlot> slots, int nextId)
{
    private static readonly WeaponQuality[] AllQualities = [WeaponQuality.Poor, WeaponQuality.Standard, WeaponQuality.Excellent];

    public int NextId { get; private set; } = nextId;
    public string Letter => letter;

    public MarketStall Stall(int number, string label)
    {
        var existing = stalls.FirstOrDefault(s => s.Number == number);
        if (existing is not null) return existing;
        var stall = new MarketStall($"{letter}{number}", number, label);
        stalls.Add(stall);
        stalls.Sort((a, b) => a.Number.CompareTo(b.Number));
        return stall;
    }

    public MarketStall NewStall(string label) => Stall(stalls.Count == 0 ? 1 : stalls.Max(s => s.Number) + 1, label);

    /// <summary>The stall with this label, or a new one after the last.</summary>
    public MarketStall NewStallFor(string label) =>
        stalls.FirstOrDefault(s => string.Equals(s.Label, label, StringComparison.OrdinalIgnoreCase)) ?? NewStall(label);

    /// <summary>Already listed: in that quality, or in any quality when <paramref name="quality"/> is null.</summary>
    public bool Has(int itemId, WeaponQuality? quality) =>
        listings.Any(l => l.ItemId == itemId && (quality is null || (l.Quality ?? WeaponQuality.Standard) == quality));

    /// <summary>
    /// Lists a catalog item. <paramref name="rolled"/> marks items that came from the generator tables, which bring
    /// every quality and their Foundational piece.
    /// </summary>
    public IReadOnlyList<MarketListing> Item(MarketItem item, MarketStall stall, ListingOrigin origin, string? group, string? goodsId,
        bool rolled, bool forSale = true, WeaponQuality? quality = null, string? note = null)
    {
        var added = new List<MarketListing>();
        WeaponQuality?[] qualities = quality is { } q ? [q] : rolled && HasQualities(item) ? [.. AllQualities] : [null];
        foreach (var each in qualities)
        {
            if (Has(item.Id, each)) continue;
            var price = Price(item, each);
            added.Add(Add(new MarketListing
            {
                Stall = stall.Code, ItemId = item.Id, Slug = item.Slug, Name = item.Name, CategoryKey = item.CategoryKey,
                BasePrice = price ?? 0,
                BaseCategory = price is { } p && each is not null and not WeaponQuality.Standard ? rules.CategoryOf(p) : item.PriceCategory ?? rules.CategoryOf(price ?? 0),
                Quality = each ?? (item.CategoryKey == "weapons" ? item.Quality : null),
                ForSale = forSale && price is not null,
                Origin = origin, Group = group, GoodsId = goodsId, Source = Source(item),
                Note = price is null ? $"Sem preço fixo no livro{(item.PriceNote is { } pn ? $": {pn}" : "")}. Ajuste o preço para vender." : note,
            }));
        }
        if (rolled && item.RequiresItemId is { } requires && catalog.Find(requires) is { } foundation && !Has(foundation.Id, null))
            added.AddRange(Item(foundation, stall, ListingOrigin.Foundational, $"Peça-base de {item.Name}", goodsId, rolled: true));
        return added;
    }

    public MarketListing Local(string name, int price, MarketStall stall, ListingOrigin origin, string? group, string? goodsId,
        string? categoryKey = null, string? source = null, string? note = null) =>
        Add(new MarketListing
        {
            Stall = stall.Code, Name = name, CategoryKey = categoryKey, BasePrice = price, BaseCategory = rules.CategoryOf(price),
            Origin = origin, Group = group, GoodsId = goodsId, Source = source, Note = note,
        });

    public OpenSlot Slot(MarketStall stall, string label, ItemQuery query, ListingOrigin origin, string? goodsId)
    {
        var slot = new OpenSlot(NextId++, stall.Code, label, query, origin, goodsId);
        slots.Add(slot);
        return slot;
    }

    private MarketListing Add(MarketListing listing)
    {
        var withId = listing with { Id = NextId++ };
        listings.Add(withId);
        return withId;
    }

    /// <summary>A standard, non-exotic weapon whose price has Poor and Excellent versions (CP:R p.342).</summary>
    private bool HasQualities(MarketItem item) =>
        item.CategoryKey == "weapons" && item is { Exotic: false, Quality: WeaponQuality.Standard, Price: { } p }
        && rules.QualityPrice(p, WeaponQuality.Poor) is not null;

    private int? Price(MarketItem item, WeaponQuality? quality) =>
        quality is { } q && q != WeaponQuality.Standard && item.Price is { } p ? rules.QualityPrice(p, q) ?? p : item.Price;

    private static string? Source(MarketItem item) => item.Book is null ? null : item.Page is { } page ? $"{item.Book} p.{page}" : item.Book;
}
