using System.Text.Json.Serialization;
using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NightMarkets;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ItemCondition { Normal, Damaged, Destroyed, BeyondRepair }

/// <summary>Why a listing is in the market.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ListingOrigin
{
    /// <summary>A d100 row of CP:R p.338–339.</summary>
    CoreTable,
    /// <summary>The Foundational piece an Option needs (CP:R p.338).</summary>
    Foundational,
    /// <summary>A Black Chrome listing with this market's booth letter (BC p.7).</summary>
    BlackChrome,
    /// <summary>Stock the preset always brings (BC+, the Interface RED vendors).</summary>
    Preset,
    /// <summary>Items of other enabled books for a rolled kind of goods (complementary).</summary>
    Related,
    GmChoice,
    Midnight,
    Manual,
    /// <summary>A Night Market Index row the catalog doesn't have.</summary>
    Index,
    Admission,
}

/// <summary>
/// The whole saved market (<c>NightMarkets.StateJson</c>). Prices are stored as they were when the listing was
/// made; the condition discount and the Haggle deals are applied on every read and purchase.
/// </summary>
public sealed record MarketState
{
    public string PresetId { get; init; } = "core";
    public string Letter { get; init; } = "N";
    public MarketKind Kind { get; init; }
    public MarketOrganizer Organizer { get; init; } = new();
    public int Seed { get; init; }
    public List<string> Books { get; init; } = [];
    public MarketOptions Options { get; init; } = new();
    public List<RolledGoods> Goods { get; init; } = [];
    public List<MarketStall> Stalls { get; init; } = [];
    public List<MarketListing> Listings { get; init; } = [];
    public List<OpenSlot> OpenSlots { get; init; } = [];
    public List<MarketSale> Sales { get; init; } = [];
    public List<MarketEvent> Events { get; init; } = [];
    public int Visitors { get; init; }
    public int NextId { get; init; } = 1;
    public string? Notes { get; init; }
}

public sealed record MarketOrganizer
{
    public string Name { get; init; } = "Fixer";
    public string? Role { get; init; }
    public int? OperatorRank { get; init; }
    /// <summary>COOL + Trading skill base, the seller side of a Haggle.</summary>
    public int? Trading { get; init; }
}

public sealed record MarketOptions
{
    /// <summary>Chance (0–100) that a listing is Damaged: second-hand goods (complementary).</summary>
    public int DamagedPercent { get; init; }
    /// <summary>Damaged goods are sold at full price, the condition hidden from the buyers (IR5 p.5).</summary>
    public bool Scam { get; init; }
    public bool IncludeRelated { get; init; }
    /// <summary>Black Chrome booth letter whose listings join a market that isn't one of the six (BC p.7).</summary>
    public string? BlackChromeLetter { get; init; }
}

/// <summary>A kind of goods in the market: the d6 that chose it and the d100 results of its column.</summary>
public sealed record RolledGoods(string Id, string Name, int? Roll, int Lines, List<TableRoll> Rolls);

public sealed record TableRoll(int Roll, int Band, string Label);

/// <summary>A spot of the market. <see cref="Code"/> is the booth "@#" of BC p.7 (letter of the market + number).</summary>
public sealed record MarketStall(string Code, int Number, string Label);

public sealed record MarketListing
{
    public int Id { get; init; }
    public string Stall { get; init; } = "";
    public int? ItemId { get; init; }
    public string? Slug { get; init; }
    public string Name { get; init; } = "";
    public string? CategoryKey { get; init; }
    public int BasePrice { get; init; }
    public PriceCategory BaseCategory { get; init; }
    public WeaponQuality? Quality { get; init; }
    public ItemCondition Condition { get; init; }
    /// <summary>The seller hides the condition and asks the full price.</summary>
    public bool ConditionHidden { get; init; }
    /// <summary>Null: not tracked (the generator lists kinds of things, not the stock; CP:R p.338).</summary>
    public int? Stock { get; init; }
    public bool ForSale { get; init; } = true;
    public ListingOrigin Origin { get; init; }
    /// <summary>The d100 row or pool that brought the listing.</summary>
    public string? Group { get; init; }
    public string? GoodsId { get; init; }
    public string? Source { get; init; }
    public string? Note { get; init; }
}

/// <summary>A row "of GM's choice" (or a Midnight Market item) still waiting for the GM to pick the item.</summary>
public sealed record OpenSlot(int Id, string Stall, string Label, ItemQuery Query, ListingOrigin Origin, string? GoodsId);

public sealed record MarketSale
{
    public int Id { get; init; }
    public DateTime At { get; init; }
    public int? ListingId { get; init; }
    public string Name { get; init; } = "";
    public string? Buyer { get; init; }
    public int Quantity { get; init; }
    /// <summary>Units handed over (one more with the "buy 5, get 1" deal).</summary>
    public int Delivered { get; init; }
    public int UnitPrice { get; init; }
    public int Total { get; init; }
    public int PaidNow { get; init; }
    public int DueLater { get; init; }
    public string? DealId { get; init; }
    public string? Haggle { get; init; }
    /// <summary>"sale", "prize" (Door Prize) or "box" (Mystery Box).</summary>
    public string Kind { get; init; } = "sale";
    public bool Undone { get; init; }
}

public sealed record MarketEvent(DateTime At, string Kind, string Title, string? Detail = null);
