using System.Text.Json.Serialization;
using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NightMarkets;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ItemCondition { Normal, Damaged, Destroyed, BeyondRepair }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ListingOrigin
{
    // d100 row, CP:R p.338-339
    CoreTable,
    // CP:R p.338
    Foundational,
    // BC p.7
    BlackChrome,
    // BC+, the Interface RED vendors
    Preset,
    // complementary
    Related,
    GmChoice,
    Midnight,
    Manual,
    // Night Market Index row missing from the catalog
    Index,
    Admission,
}

// Prices are stored as they were when listed; condition discount and Haggle deals apply on every read.
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
    // COOL + Trading, the seller side of a Haggle
    public int? Trading { get; init; }
}

public sealed record MarketOptions
{
    // 0-100, second-hand goods (complementary)
    public int DamagedPercent { get; init; }
    // damaged goods sold at full price, condition hidden (IR5 p.5)
    public bool Scam { get; init; }
    public bool IncludeRelated { get; init; }
    // for markets outside the six (BC p.7)
    public string? BlackChromeLetter { get; init; }
}

public sealed record RolledGoods(string Id, string Name, int? Roll, int Lines, List<TableRoll> Rolls);

public sealed record TableRoll(int Roll, int Band, string Label);

// Code is the "@#" booth of BC p.7
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
    public bool ConditionHidden { get; init; }
    // null = not tracked, the generator lists kinds of things (CP:R p.338)
    public int? Stock { get; init; }
    public bool ForSale { get; init; } = true;
    public ListingOrigin Origin { get; init; }
    public string? Group { get; init; }
    public string? GoodsId { get; init; }
    public string? Source { get; init; }
    public string? Note { get; init; }
}

// "GM's choice" row (or Midnight Market) still waiting for the GM
public sealed record OpenSlot(int Id, string Stall, string Label, ItemQuery Query, ListingOrigin Origin, string? GoodsId);

public sealed record MarketSale
{
    public int Id { get; init; }
    public DateTime At { get; init; }
    public int? ListingId { get; init; }
    public string Name { get; init; } = "";
    public string? Buyer { get; init; }
    public int Quantity { get; init; }
    // +1 with "buy 5, get 1"
    public int Delivered { get; init; }
    public int UnitPrice { get; init; }
    public int Total { get; init; }
    public int PaidNow { get; init; }
    public int DueLater { get; init; }
    public string? DealId { get; init; }
    public string? Haggle { get; init; }
    // "sale", "prize" (Door Prize) or "box" (Mystery Box)
    public string Kind { get; init; } = "sale";
    public bool Undone { get; init; }
}

public sealed record MarketEvent(DateTime At, string Kind, string Title, string? Detail = null);
