using System.Text.Json.Serialization;
using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NightMarkets;

/// <summary>
/// Builds a market. Empty fields fall back to the preset: its organizer, its kinds of goods (rolled, suggested or
/// none) and every book of the catalog (the generic Core market uses only the Core Rulebook).
/// </summary>
public sealed record GenerateMarketRequest
{
    public string? Name { get; init; }
    public string? PresetId { get; init; }
    public int? Seed { get; init; }
    public List<string>? Books { get; init; }
    /// <summary>Kinds of goods chosen by the GM. Wins over <see cref="RollGoods"/> and the preset suggestion.</summary>
    public List<string>? Goods { get; init; }
    /// <summary>Roll the kinds of goods with the Core d6 even when the preset suggests some.</summary>
    public bool RollGoods { get; init; }
    /// <summary>Lines per kind of goods typed by the GM (physical dice); missing ones are rolled with 1d10.</summary>
    public Dictionary<string, int>? Lines { get; init; }
    public MarketOrganizer? Organizer { get; init; }
    public MarketOptions? Options { get; init; }
    /// <summary>Add a Midnight Market (Fixer of Rank 9 or more; CP:R p.161).</summary>
    public bool Midnight { get; init; }
}

public sealed record UpdateMarketRequest(int Version, string Name, string? Notes, NightMarketStatus Status);

public sealed record MarketVersionRequest(int Version);

public sealed record AddListingRequest(
    int Version, int? ItemId = null, int? IndexRow = null, string? Name = null, int? Price = null, string? Stall = null,
    ItemCondition Condition = ItemCondition.Normal, bool ConditionHidden = false, int? Stock = null,
    WeaponQuality? Quality = null, string? Note = null);

public sealed record UpdateListingRequest(
    int Version, string? Stall, ItemCondition Condition, bool ConditionHidden, int? Stock, bool ForSale, string? Note);

public sealed record SetPriceRequest(int Version, int Price);

public sealed record FillSlotRequest(int Version, int? ItemId, bool Draw);

/// <summary>One side of a Haggle: COOL + Trading (the skill base) and the Operator Rank (0 when not a Fixer).</summary>
public sealed record HaggleSide(int Trading, int Rank);

/// <summary>Rolls may be typed (physical dice): the d10, then the extra d10 of a natural 10 or 1. Missing dice are rolled.</summary>
public sealed record HaggleRequest(HaggleSide Buyer, HaggleSide Seller, int[]? BuyerRolls = null, int[]? SellerRolls = null);

public sealed record DealOption(string Id, string Label, string Text, int MinRank, bool Applies, string? Reason);

public sealed record HaggleResult(
    int BuyerTotal, int SellerTotal, int[] BuyerRolls, int[] SellerRolls, bool Success, string Summary,
    IReadOnlyList<DealOption> Deals);

public sealed record PurchaseRequest(
    int Version, int ListingId, int Quantity = 1, string? Buyer = null, HaggleRequest? Haggle = null, string? DealId = null);

public sealed record DoorPrizeRequest(int Version, string? Winner = null, int? Roll = null);

public sealed record MysteryBoxRequest(int Version, string? Recipient = null, int? BoxRoll = null, int? ContentRoll = null);

public sealed record MysteryBoxRollRequest(int? BoxRoll = null, int? ContentRoll = null);

public sealed record VisitorsRequest(int Version, int Count);

/// <summary>How the seller deals with the condition: tells, keeps quiet, or disguises the damage (Forgery).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConditionDisclosure { Disclosed, Silent, Disguised }

/// <summary>
/// Price a Fixer pays for an item the Edgerunners sell (IR5 p.5; Haggle of CP:R p.160–161). In <see cref="Haggle"/>,
/// the Buyer side is whoever haggles (the selling Edgerunner) and the Seller side is the Fixer buying.
/// </summary>
public sealed record SellQuoteRequest(
    int? ItemId = null, int? Price = null, ItemCondition Condition = ItemCondition.Normal,
    ConditionDisclosure Disclosure = ConditionDisclosure.Disclosed, int? FixerRank = null, HaggleRequest? Haggle = null,
    string? DealId = null);

public sealed record SellQuote(
    string? Name, int MarketPrice, PriceCategory MarketCategory, int Offer, PriceCategory OfferCategory,
    bool FixerNoticesDamage, IReadOnlyList<string> Notes, HaggleResult? Haggle);

public sealed record PrizeResult(int? Roll, string Label, int Quantity, int? ItemId, string? ItemName, int? ExtraItemId, string? ExtraItemName, bool Loaded, WeaponQuality? Quality);

public sealed record MysteryBoxResult(int BoxRoll, string Box, int ContentRoll, PrizeResult Prize, string Source);

public sealed record DoorPrizeResult(int Roll, int Of, PrizeResult Prize, IReadOnlyList<PrizeResult> Bonus);

public sealed record NightMarketSummaryDto(
    int Id, string Name, string PresetId, string PresetName, string Letter, string Organizer, int? OperatorRank,
    NightMarketStatus Status, int Listings, int Sales, int Revenue, DateTime UpdatedAt);

public sealed record ListingDto(
    int Id, string Stall, int? ItemId, string? Slug, string Name, string? CategoryKey, string? Manufacturer, string? Description,
    string? Book, int? Page, int BasePrice, PriceCategory BaseCategory, int Price, PriceCategory PriceCategory, int Value,
    PriceCategory ValueCategory, WeaponQuality? Quality, ItemCondition Condition, bool ConditionHidden, int? Stock, bool ForSale,
    ListingOrigin Origin, string? Group, string? GoodsId, string? Source, string? Note, IReadOnlyList<string> Warnings);

public sealed record StallDto(string Code, int Number, string Label, int Listings);

public sealed record MarketPresetDto(
    string Id, string Name, MarketKind Kind, string Letter, PresetOrganizer Organizer, string? Summary, string? Frequency,
    string? Location, string? Sells, string? Invite, string? Rules, GoodsMode Goods, IReadOnlyList<string> SuggestedGoods,
    IReadOnlyList<MarketFeature> Features, Admission? Admission, bool ForSale, string Source);

public sealed record NightMarketDetailDto(
    int Id, string Name, int Version, NightMarketStatus Status, string? Notes, DateTime CreatedAt, DateTime UpdatedAt,
    MarketPresetDto Preset, MarketOrganizer Organizer, PriceCategory? OrganizerReach, int Seed, IReadOnlyList<string> Books,
    MarketOptions Options, IReadOnlyList<RolledGoods> Goods, IReadOnlyList<StallDto> Stalls, IReadOnlyList<ListingDto> Listings,
    IReadOnlyList<OpenSlot> OpenSlots, IReadOnlyList<MarketSale> Sales, IReadOnlyList<MarketEvent> Events, int Visitors,
    IReadOnlyList<string> Notices);

public sealed record BookOption(string Name, int Items);

public sealed record NightMarketRulesDto(
    NightMarketRulesDocument Rules, IReadOnlyList<MarketPresetDto> Presets, IReadOnlyList<BookOption> Books,
    IReadOnlyList<IndexSectionDto> IndexSections);

public sealed record IndexSectionDto(string Id, string Label, int Rows);

public sealed record IndexQuery(
    string? Q = null, string? Section = null, PriceCategory? Category = null, bool? Catalogued = null, int Page = 1, int PageSize = 50);

public sealed record IndexRowDto(
    int Row, string Section, string? Group, string Name, string Cost, int? PriceEb, PriceCategory? PriceCategory,
    IReadOnlyList<IndexSource> Sources, int? ItemId, string? Slug);

public sealed record IndexPageDto(int Total, int Page, int PageSize, IReadOnlyList<IndexRowDto> Rows);

/// <summary>A market event answered with the updated market and what came out of it.</summary>
public sealed record MarketEventResult<T>(NightMarketDetailDto Market, T Result);
