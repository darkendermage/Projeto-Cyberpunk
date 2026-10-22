using System.Text.Json;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.NightMarkets.Rules;

/// <summary>A row of the Night Market Index (v1.1). <see cref="Slug"/> is set when the item is in the catalog.</summary>
public sealed record IndexEntry(
    string Section, string? Group, string Name, string Cost, int? PriceEb, PriceCategory? PriceCategory,
    IndexSource[] Sources, string? Slug);

public sealed record IndexSource(string Book, int? Page);

/// <summary>
/// The loaded Night Market rules, the Night Market Index and the Black Chrome booth codes. Singleton. Weapon quality
/// prices come from the shared NPC rules (CP:R p.342).
/// </summary>
public sealed class NightMarketRuleBook
{
    private const string RulesResource = "CyberpunkDb.Application.NightMarkets.Rules.night-market-rules.json";
    private const string IndexResource = "CyberpunkDb.Application.NightMarkets.night-market-index.json";
    private const string BoothsResource = "CyberpunkDb.Application.NightMarkets.black-chrome-booths.json";

    private readonly Dictionary<string, GoodsType> _goods;
    private readonly Dictionary<string, MarketPreset> _presets;
    private readonly Dictionary<string, HaggleDeal> _deals;

    public NightMarketRuleBook(NightMarketRulesDocument document, IReadOnlyList<IndexEntry> index,
        IReadOnlyDictionary<string, string[]> booths, NpcRuleBook shared)
    {
        Document = document;
        Index = index;
        Booths = booths;
        Shared = shared;
        _goods = document.Generator.Goods.Value.ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);
        _presets = document.Presets.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        _deals = document.Operator.Deals.Value.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
        Ladder = document.PriceLadder.Value.OrderBy(t => t.Category).ToList();
    }

    public NightMarketRulesDocument Document { get; }
    public IReadOnlyList<IndexEntry> Index { get; }
    /// <summary>Catalog slug → booth codes ("C3") of the Black Chrome listings.</summary>
    public IReadOnlyDictionary<string, string[]> Booths { get; }
    public NpcRuleBook Shared { get; }
    public IReadOnlyList<PriceTier> Ladder { get; }
    public IReadOnlyList<GoodsType> Goods => Document.Generator.Goods.Value;
    public IReadOnlyList<MarketPreset> Presets => Document.Presets;

    public static NightMarketRuleBook LoadEmbedded(NpcRuleBook shared)
    {
        var assembly = typeof(NightMarketRuleBook).Assembly;
        T Read<T>(string name)
        {
            using var stream = assembly.GetManifestResourceStream(name)
                               ?? throw new InvalidOperationException($"Recurso {name} não encontrado.");
            return JsonSerializer.Deserialize<T>(stream, NightMarketRulesDocument.JsonOptions)
                   ?? throw new InvalidOperationException($"{name} vazio.");
        }

        return new NightMarketRuleBook(
            Read<NightMarketRulesDocument>(RulesResource),
            Read<List<IndexEntry>>(IndexResource),
            Read<Dictionary<string, string[]>>(BoothsResource),
            shared);
    }

    public GoodsType Good(string id) => _goods.TryGetValue(id, out var g)
        ? g
        : throw RequestValidationException.For("goods", $"Tipo de mercadoria '{id}' não existe.");

    public GoodsType? GoodByRoll(int roll) => Goods.FirstOrDefault(g => g.Roll == roll);

    public MarketPreset Preset(string? id) => _presets.TryGetValue(id ?? "core", out var p)
        ? p
        : throw RequestValidationException.For("presetId", $"Mercado '{id}' não existe.");

    public HaggleDeal? Deal(string? id) => id is null ? null : _deals.GetValueOrDefault(id);

    public int PriceOf(PriceCategory category) => Ladder.First(t => t.Category == category).Price;

    /// <summary>The category of a price that has none in the catalog: the highest tier whose market price it reaches.</summary>
    public PriceCategory CategoryOf(int price) =>
        Ladder.LastOrDefault(t => price >= t.Price)?.Category ?? PriceCategory.Cheap;

    /// <summary>Moves a category down by <paramref name="steps"/>, never below the floor (IR5 p.5; floor complementary).</summary>
    public PriceCategory Lower(PriceCategory category, int steps)
    {
        var floor = Document.Condition.Floor.Value;
        var target = (int)category - steps;
        return target < (int)floor ? floor : (PriceCategory)target;
    }

    /// <summary>The highest category a Fixer of this Rank always sources (CP:R p.160–161). Null without a Rank.</summary>
    public PriceCategory? Reach(int? rank) => rank is { } r and > 0
        ? Document.Operator.Reach.Value.FirstOrDefault(t => r >= t.MinRank && r <= t.MaxRank)?.Category ?? PriceCategory.SuperLuxury
        : null;

    /// <summary>Weapon price for a quality (CP:R p.342). Null when the standard price has no Poor/Excellent row.</summary>
    public int? QualityPrice(int standardPrice, WeaponQuality quality)
    {
        var row = Shared.Document.WeaponQualityPricing.Rows.FirstOrDefault(r => r.Standard == standardPrice);
        return row is null ? null : quality switch
        {
            WeaponQuality.Poor => row.Poor,
            WeaponQuality.Excellent => row.Excellent,
            _ => row.Standard,
        };
    }
}
