using System.Text.Json;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.NightMarkets;
using CyberpunkDb.Application.NightMarkets.Generation;
using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Application.NightMarkets.Trading;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Tests.Unit.Npcs;

namespace CyberpunkDb.Tests.Unit.NightMarkets;

/// <summary>The real rule book and the real seed catalog, read straight from the import file (no database).</summary>
internal static class MarketTestData
{
    private static readonly Dictionary<string, string> CategoryKeys = new()
    {
        ["weapons"] = "weapons", ["ammunition"] = "ammunition", ["weapon-attachments"] = "weapon-attachments", ["gear"] = "gear",
        ["armor"] = "armor", ["cyberware"] = "cyberware", ["netrunning"] = "netrunning", ["drugs"] = "drugs", ["vehicles"] = "vehicles",
        ["vehicle-upgrades"] = "vehicle-upgrades", ["hq-upgrades"] = "hq-upgrades",
    };

    private static readonly Dictionary<string, ItemCategory> Categories = new()
    {
        ["weapons"] = ItemCategory.Weapon, ["ammunition"] = ItemCategory.Ammunition, ["weapon-attachments"] = ItemCategory.WeaponAttachment,
        ["gear"] = ItemCategory.Gear, ["armor"] = ItemCategory.Armor, ["cyberware"] = ItemCategory.Cyberware,
        ["netrunning"] = ItemCategory.Netrunning, ["drugs"] = ItemCategory.Drug, ["vehicles"] = ItemCategory.Vehicle,
        ["vehicle-upgrades"] = ItemCategory.VehicleUpgrade, ["hq-upgrades"] = ItemCategory.HqUpgrade,
    };

    /// <summary>The attribute that holds each category's own type in the import format.</summary>
    private static readonly string[] TypeKeys =
        ["cyberwareType", "weaponClass", "armorType", "gearType", "netType", "classification", "attachmentType", "vehicleType", "kind", "upgradeType"];

    // Declared after the lookup tables: static fields initialize in textual order.
    public static readonly NightMarketRuleBook Rules = NightMarketRuleBook.LoadEmbedded(NpcTestData.Rules);
    public static readonly MarketPricing Pricing = new(Rules);
    public static readonly NightMarketGenerator Generator = new(Rules);
    public static readonly MarketCatalog Catalog = LoadCatalog();
    public static readonly DateTime Now = new(2045, 6, 5, 22, 0, 0, DateTimeKind.Utc);

    public static MarketChange Change(MarketState state) => new(state, Catalog, Now);

    public static MarketState Generate(GenerateMarketRequest request) => Generator.Generate(request, Catalog, Now);

    public static MarketItem Item(string slug) => Catalog.Find(slug) ?? throw new InvalidOperationException($"{slug} não está no seed.");

    private static MarketCatalog LoadCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Seed", "01-compendium.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var rows = doc.RootElement.EnumerateArray().ToList();
        var ids = rows.Select((r, i) => (Slug: r.GetProperty("slug").GetString()!, Id: i + 1)).ToDictionary(x => x.Slug, x => x.Id);
        return new MarketCatalog(rows.Select((r, i) =>
        {
            var attrs = r.TryGetProperty("attributes", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;
            string? Attr(string key) => attrs.ValueKind == JsonValueKind.Object && attrs.TryGetProperty(key, out var v)
                ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.True => "true", JsonValueKind.False => "false", _ => null }
                : null;
            var category = r.GetProperty("category").GetString()!;
            return new MarketItem(
                i + 1, r.GetProperty("slug").GetString()!, r.GetProperty("name").GetString()!, Categories[category], CategoryKeys[category],
                r.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null,
                r.TryGetProperty("priceCategory", out var pc) && pc.ValueKind == JsonValueKind.String ? Enum.Parse<PriceCategory>(pc.GetString()!) : null,
                null, r.TryGetProperty("source", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null,
                r.TryGetProperty("page", out var pg) && pg.ValueKind == JsonValueKind.Number ? pg.GetInt32() : null,
                null, null,
                TypeKeys.Select(Attr).FirstOrDefault(t => t is not null),
                Attr("clothingStyle"), Attr("exotic") == "true", Attr("foundational") == "true",
                Attr("quality") is { } q ? Enum.Parse<WeaponQuality>(q) : null,
                Attr("requiresItem") is { } req && ids.TryGetValue(req, out var reqId) ? reqId : null);
        }));
    }
}

/// <summary>Dice that return the given results in order, cycling.</summary>
internal sealed class FixedDice(params int[] rolls) : IDiceRoller
{
    private int _i;
    public int Roll(int sides) => rolls[_i++ % rolls.Length];
}
