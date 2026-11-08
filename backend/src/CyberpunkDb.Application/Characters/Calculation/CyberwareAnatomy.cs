using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Calculation;

public sealed record CyberRegionReport(
    string Key,
    string Label,
    BodySide? Side,
    // null for pools and meat limbs
    string? FoundationInstanceId,
    string? FoundationName,
    // 1 for a meat limb (CP:R p.115)
    int? SlotsTotal,
    int SlotsUsed,
    bool MeatLimb,
    IReadOnlyList<string> Pieces,
    string? Source);

// Region by root foundation or standalone category, plus the side of eyes/arms/legs. Follows the book's
// categories (CP:R p.110-117), not biology.
public static class CyberwareAnatomy
{
    public static readonly (string Key, string Label)[] Regions =
    [
        ("neural", "Neuralware"),
        ("eye-left", L.T("Olho esquerdo", "Left eye")), ("eye-right", L.T("Olho direito", "Right eye")),
        ("audio", "Cyberaudio"),
        ("arm-left", L.T("Braço esquerdo", "Left arm")), ("arm-right", L.T("Braço direito", "Right arm")),
        ("leg-left", L.T("Perna esquerda", "Left leg")), ("leg-right", L.T("Perna direita", "Right leg")),
        ("internal", "Internal Cyberware"), ("external", "External Cyberware"), ("fashion", "Fashionware"),
        ("borgware", "Borgware"),
    ];

    public enum Limb { Eye, Arm, Leg }

    public static Limb? LimbOf(CatalogItem item, bool meatLimb)
    {
        if (item.Cyber is not { } c) return null;
        if (c.Foundational && c.Type == CyberwareType.Cyberoptics) return Limb.Eye;
        if (c.Foundational && c.Type == CyberwareType.Cyberlimb) return c.Location == BodyLocation.Leg ? Limb.Leg : Limb.Arm;
        if (meatLimb) return c.Location == BodyLocation.Leg ? Limb.Leg : Limb.Arm;
        return null;
    }

    // preferred side if free, else left, then right; null when both are taken
    public static BodySide? FreeSide(IEnumerable<CharacterCyberware> installed, NpcCatalog catalog, Limb limb, BodySide? preferred = null)
    {
        var taken = installed
            .Where(c => c.Side is not null && catalog.Find(c.ItemId) is { } i && LimbOf(i, c.MeatLimb) == limb)
            .Select(c => c.Side!.Value)
            .ToHashSet();
        if (preferred is { } p && !taken.Contains(p)) return p;
        return !taken.Contains(BodySide.Left) ? BodySide.Left : !taken.Contains(BodySide.Right) ? BodySide.Right : null;
    }

    public static string RegionOf(CharacterCyberware piece, IReadOnlyList<CharacterCyberware> all, NpcCatalog catalog)
    {
        var root = piece;
        for (var guard = 0; root.ParentInstanceId is { } parent && guard < 8; guard++)
            root = all.FirstOrDefault(c => c.InstanceId == parent) ?? root;
        var item = catalog.Find(root.ItemId);
        var side = root.Side == BodySide.Right ? "right" : "left";
        if (item is null) return "internal";
        var limb = LimbOf(item, root.MeatLimb);
        if (limb == Limb.Eye) return $"eye-{side}";
        if (limb == Limb.Arm) return $"arm-{side}";
        if (limb == Limb.Leg) return $"leg-{side}";
        return item.Cyber?.Type switch
        {
            CyberwareType.Neuralware => "neural",
            CyberwareType.Cyberoptics => $"eye-{side}",
            CyberwareType.Cyberaudio => "audio",
            CyberwareType.ExternalBody => "external",
            CyberwareType.Fashionware => "fashion",
            CyberwareType.Borgware => "borgware",
            CyberwareType.Cyberlimb or CyberwareType.LimbOption or CyberwareType.Cyberfinger => item.Cyber.Location == BodyLocation.Leg ? $"leg-{side}" : $"arm-{side}",
            _ => "internal",
        };
    }

    public static List<CyberRegionReport> Describe(CharacterSheet sheet, NpcCatalog catalog, NpcMechanics mechanics)
    {
        var all = sheet.Cyberware;
        var byRegion = all.GroupBy(c => RegionOf(c, all, catalog)).ToDictionary(g => g.Key, g => g.ToList());
        var pools = mechanics.Rules.Document.Cyberware.Pools.ToDictionary(p => p.Key);
        var result = new List<CyberRegionReport>();
        foreach (var (key, label) in Regions)
        {
            var pieces = byRegion.GetValueOrDefault(key) ?? [];
            var side = key.EndsWith("-left") ? BodySide.Left : key.EndsWith("-right") ? BodySide.Right : (BodySide?)null;
            var foundation = pieces.FirstOrDefault(p => p.ParentInstanceId is null && catalog.Find(p.ItemId)?.Cyber?.Foundational == true);
            var foundationItem = foundation is null ? null : catalog.Find(foundation.ItemId);
            int? total;
            int used;
            string? source;
            var meat = pieces.Any(p => p.MeatLimb);
            if (foundation is not null)
            {
                total = mechanics.SlotsProvided(foundationItem!) ?? 0;
                used = all.Where(c => c.ParentInstanceId == foundation.InstanceId).Sum(c => catalog.Find(c.ItemId) is { } i ? mechanics.SlotsRequired(i).Slots : 1);
                source = foundationItem!.SourceBook is { } b ? $"{b}{(foundationItem.Page is { } pg ? $" p.{pg}" : "")}" : null;
            }
            else if (key is "internal" or "external" or "fashion" && pools.TryGetValue(key == "fashion" ? "fashionware" : key, out var pool))
            {
                total = mechanics.PoolSlots(pool);
                used = pieces.Where(p => p.ParentInstanceId is null).Sum(p => catalog.Find(p.ItemId) is { } i ? mechanics.SlotsRequired(i).Slots : 1);
                source = pool.Source;
            }
            else if (meat)
            {
                total = 1;
                used = pieces.Count(p => p.MeatLimb);
                source = "CP:R p.115";
            }
            else
            {
                total = null;
                used = 0;
                source = null;
            }
            result.Add(new CyberRegionReport(key, label, side, foundation?.InstanceId, foundationItem?.Name, total, used, meat,
                pieces.Select(p => p.InstanceId).ToList(), source));
        }
        return result;
    }
}
