using System.Text.Json;
using System.Text.Json.Serialization;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NightMarkets.Rules;

// night-market-rules.json. Same Ruled<T> convention as the NPC and character rules.
public sealed record NightMarketRulesDocument
{
    public Ruled<PriceTier[]> PriceLadder { get; init; } = new();
    public Ruled<PriceCategory> WithoutFixer { get; init; } = new();
    public GeneratorRules Generator { get; init; } = new();
    public OperatorRules Operator { get; init; } = new();
    public ConditionRules Condition { get; init; } = new();
    public MidnightMarketRules MidnightMarket { get; init; } = new();
    public MysteryBoxRules MysteryBoxes { get; init; } = new();
    public DoorPrizeRules DoorPrize { get; init; } = new();
    public Ruled<string> FixerAvailability { get; init; } = new();
    public BlackChromeRules BlackChrome { get; init; } = new();
    public MarketPreset[] Presets { get; init; } = [];

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
}

public sealed record PriceTier(PriceCategory Category, int Price);

public sealed record GeneratorRules
{
    public Ruled<int> GoodsRolls { get; init; } = new();
    public Ruled<string> LinesPerType { get; init; } = new();
    public Ruled<int> BandSize { get; init; } = new();
    public Ruled<bool> AllQualities { get; init; } = new();
    public Ruled<string> NotTotalInventory { get; init; } = new();
    public Ruled<bool> Foundational { get; init; } = new();
    public Ruled<string> CyberwareInstall { get; init; } = new();
    public Ruled<bool> GmChoiceDraw { get; init; } = new();
    public Ruled<GoodsType[]> Goods { get; init; } = new();
    public Ruled<bool> RelatedBooks { get; init; } = new();
}

// CP:R p.338
public sealed record GoodsType
{
    public string Id { get; init; } = "";
    public int Roll { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    // used for items from other books
    public ItemQuery[] Related { get; init; } = [];
    public TableEntry[] Table { get; init; } = [];
}

// catalog items, a missing item, or a catalog slice
public sealed record TableEntry
{
    public string Label { get; init; } = "";
    public string[]? Slugs { get; init; }
    public LocalItem? Local { get; init; }
    public ItemQuery? Query { get; init; }
    // "GM's choice"
    public bool GmChoice { get; init; }
    public string? Note { get; init; }
}

// food, alcohol...
public sealed record LocalItem(string Name, int Price, string? Category = null);

// empty fields don't filter
public sealed record ItemQuery
{
    public string? Category { get; init; }
    public string[]? ExceptCategories { get; init; }
    // cyberware type, weapon class, armor type...
    public string[]? Types { get; init; }
    public string[]? ExceptTypes { get; init; }
    public string[]? Styles { get; init; }
    public PriceFilter? Price { get; init; }
    public bool? Exotic { get; init; }
    public bool? Foundational { get; init; }
    public BookFilter[]? Books { get; init; }
    public string[]? Slugs { get; init; }
}

public sealed record PriceFilter(int? Eq = null, int? Min = null, int? Max = null);

public sealed record BookFilter(string Book, int? MinPage = null, int? MaxPage = null);

public sealed record OperatorRules
{
    public Ruled<ReachTier[]> Reach { get; init; } = new();
    public Ruled<int> NightMarketRank { get; init; } = new();
    public Ruled<PriceCategory> NightMarketReach { get; init; } = new();
    public Ruled<int> MidnightMarketRank { get; init; } = new();
    public Ruled<string> HaggleFormula { get; init; } = new();
    public Ruled<string> HaggleTie { get; init; } = new();
    public Ruled<string> Criticals { get; init; } = new();
    public Ruled<int> DealsPerTransaction { get; init; } = new();
    public Ruled<HaggleDeal[]> Deals { get; init; } = new();
    public Ruled<string> Rounding { get; init; } = new();
}

public sealed record ReachTier(int MinRank, int MaxRank, PriceCategory Category);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DealKind { Discount, ExtraItem, SplitPayment, JobPay }

// CP:R p.160-161. A Fixer gets one deal of their Rank or lower.
public sealed record HaggleDeal
{
    public string Id { get; init; } = "";
    public int MinRank { get; init; }
    public DealKind Kind { get; init; }
    public int? Percent { get; init; }
    public int? MinQuantity { get; init; }
    public PriceCategory[]? Categories { get; init; }
    public string Label { get; init; } = "";
    public string Text { get; init; } = "";
}

public sealed record ConditionRules
{
    public Ruled<int> DamagedSteps { get; init; } = new();
    public Ruled<int> DestroyedSteps { get; init; } = new();
    public Ruled<int> DetectRank { get; init; } = new();
    public Ruled<int> LowValueDamaged { get; init; } = new();
    public Ruled<int> BeyondRepair { get; init; } = new();
    public Ruled<string> SellingIsGmCall { get; init; } = new();
    public Ruled<PriceCategory> Floor { get; init; } = new();
    public Ruled<string> CategoryPrice { get; init; } = new();
}

public sealed record MidnightMarketRules
{
    public Ruled<string> Items { get; init; } = new();
    public Ruled<DrawPool> DrawPool { get; init; } = new();
}

public sealed record DrawPool(PriceCategory MinCategory, bool OrExotic);

public sealed record MysteryBoxRules
{
    public Ruled<string> Redemption { get; init; } = new();
    public Ruled<int[]> PricePattern { get; init; } = new();
    public Ruled<MysteryBox[]> Boxes { get; init; } = new();
}

public sealed record MysteryBox(int Roll, string Name, PrizeEntry[] Contents);

// Mystery Box content, Door Prize slice or bonus
public sealed record PrizeEntry
{
    public int? Roll { get; init; }
    public string Label { get; init; } = "";
    public string? Slug { get; init; }
    // e.g. Road Flare with the Flare Gun
    public string? ExtraSlug { get; init; }
    public int Quantity { get; init; } = 1;
    public bool Loaded { get; init; }
    public WeaponQuality? Quality { get; init; }
    public int Weight { get; init; } = 1;
}

public sealed record DoorPrizeRules
{
    public Ruled<int> Arrival { get; init; } = new();
    public Ruled<PrizeEntry[]> Bonus { get; init; } = new();
    public Ruled<PrizeEntry> Example { get; init; } = new();
    public Ruled<PrizeEntry[]> Slices { get; init; } = new();
}

public sealed record BlackChromeRules
{
    public Ruled<string> Booths { get; init; } = new();
    public Ruled<string> ListingFee { get; init; } = new();
    public Ruled<string> BuyNow { get; init; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MarketKind { NightMarket, MidnightMarket, Shop, Armory }

// rolled (Core), suggested by the book, GM picks, or none
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GoodsMode { Roll, Suggested, Pick, None }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MarketFeature { DoorPrize, SecondHand, Midnight, MysteryBoxes }

// Core, the six Black Chrome ones, or an Interface RED vendor
public sealed record MarketPreset
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public MarketKind Kind { get; init; }
    // BC p.7, A-F are Black Chrome
    public string Letter { get; init; } = "N";
    public PresetOrganizer Organizer { get; init; } = new("Fixer", "Fixer");
    public string? Summary { get; init; }
    public string? Frequency { get; init; }
    public string? Location { get; init; }
    public string? Sells { get; init; }
    public string? Invite { get; init; }
    public string? Rules { get; init; }
    public GoodsMode Goods { get; init; }
    public string[] SuggestedGoods { get; init; } = [];
    public PresetStall[] Stalls { get; init; } = [];
    public PresetPool[] Pools { get; init; } = [];
    public MarketFeature[] Features { get; init; } = [];
    public Admission? Admission { get; init; }
    // false = lent, not sold (Toggle's Temple)
    public bool ForSale { get; init; } = true;
    public string Source { get; init; } = "";

    public bool IsBlackChrome => Letter.Length == 1 && Letter[0] is >= 'A' and <= 'F';
}

public sealed record PresetOrganizer(string Name, string Role, int? OperatorRank = null, int? Trading = null);

public sealed record PresetStall(int Number, string Label, string[] Goods);

public sealed record PresetPool(string Label, int Stall, ItemQuery Query, string Source, bool ForSale = true);

public sealed record Admission(string Label, int Price, string Source);
