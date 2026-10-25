using System.Text.Json;
using CyberpunkDb.Application.NetArchitectures;
using CyberpunkDb.Application.NetArchitectures.Generation;
using CyberpunkDb.Application.NetArchitectures.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Tests.Unit.NetArchitectures;

/// <summary>The real NET rule book and the Black ICE of the real seed catalog (no database).</summary>
internal static class NetTestData
{
    public static readonly NetArchitectureRuleBook Rules = NetArchitectureRuleBook.LoadEmbedded();
    public static readonly NetArchitectureGenerator Generator = new(Rules);
    public static readonly NetArchitecturePricing Pricing = new(Rules);
    public static readonly NetArchitectureOperations Operations = new(Rules, Generator);
    public static readonly NetArchitectureExport Export = new(Rules);
    public static readonly NetIceCatalog Ice = LoadIce();

    public static ArchitectureState Generate(GenerateArchitectureRequest request) => Generator.Generate(request);

    private static NetIceCatalog LoadIce()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Seed", "01-compendium.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var rows = doc.RootElement.EnumerateArray().Select((r, i) => (Row: r, Id: i + 1))
            .Where(x => x.Row.GetProperty("category").GetString() == "netrunning"
                        && x.Row.GetProperty("attributes").TryGetProperty("netType", out var t) && t.GetString() == "BlackIce");
        return new NetIceCatalog(rows.Select(x =>
        {
            var r = x.Row;
            var a = r.GetProperty("attributes");
            int? Int(string key) => a.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
            return new IceInfo(x.Id, r.GetProperty("slug").GetString()!, r.GetProperty("name").GetString()!,
                r.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null,
                r.TryGetProperty("priceCategory", out var pc) && Enum.TryParse<PriceCategory>(pc.GetString(), out var cat) ? cat : null,
                a.TryGetProperty("programClass", out var pcl) && Enum.TryParse<ProgramClass>(pcl.GetString(), out var cls) ? cls : null,
                Int("per"), Int("spd"), Int("atk"), Int("def"), Int("rez"),
                a.TryGetProperty("effect", out var e) ? e.GetString() : null, "Core Book",
                r.TryGetProperty("page", out var pg) && pg.ValueKind == JsonValueKind.Number ? pg.GetInt32() : null);
        }));
    }

    /// <summary>One floor of a hand-built Architecture.</summary>
    public static NetFloor Floor(string branch, int depth, params NetContent[] contents) => new()
    {
        Id = $"{branch}{depth}", BranchId = branch, Depth = depth, Table = depth <= 2 && branch == "A" ? "Lobby" : "Standard",
        Contents = contents.Select((c, i) => c with { Id = $"{branch}{depth}-{i + 1}" }).ToList(),
    };

    public static NetContent IceContent(string slug) => new() { Kind = NetContentKind.BlackIce, Slug = slug };
    public static NetContent Feature(NetContentKind kind, int dv, NodeFunction? function = null) => new() { Kind = kind, Dv = dv, Function = function };
}
