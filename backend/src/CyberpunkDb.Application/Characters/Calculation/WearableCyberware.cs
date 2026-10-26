using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Calculation;

/// <summary>A wearable host as shown on the sheet: its slots, the options in it and what it covers.</summary>
public sealed record WearableLine(
    string InstanceId, int ItemId, string Name, WearableAccepts Accepts, int Slots, int SlotsUsed, int Cyberfingers, int CyberfingersUsed,
    bool Worn, BodySide? Side, bool CoversArm, IReadOnlyList<WearableOptionLine> Options, IReadOnlyList<string> BuiltIn,
    IReadOnlyList<string> Covered, string? Requirement, IReadOnlyList<string> Warnings, string Source);

public sealed record WearableOptionLine(string InstanceId, int ItemId, string Name, int Slots, bool Finger);

/// <summary>
/// Cyberware options held by worn items (Smart Glasses, Smart Lens, Battleglove, Smart Gloves, Smart Ears, the
/// MechaMan helmet). They live in the inventory, cost no Humanity (<c>wearableHumanityLoss</c>) and only work while
/// the host is equipped; a glove worn over an arm switches off the options of that arm.
/// </summary>
public static class WearableCyberware
{
    public static WearableHost? Host(CharacterRuleBook rules, NpcCatalog catalog, InventoryEntry entry) =>
        catalog.Find(entry.ItemId) is { } item ? rules.FindWearable(item.Slug) : null;

    public static bool IsFinger(CharacterRuleBook rules, CatalogItem item) =>
        item.Cyber?.Type == CyberwareType.Cyberfinger || rules.Document.Cyberware.FingerOptions.Value.Contains(item.Slug, StringComparer.OrdinalIgnoreCase);

    /// <summary>Why an option can't go in this host, or null. Slots and duplicates are checked by the caller.</summary>
    public static string? Incompatible(CharacterRuleBook rules, WearableHost host, string hostName, CatalogItem option, NpcCatalog catalog)
    {
        if (option.Cyber is not { } c) return L.T($"{option.Name} não é cyberware.", $"{option.Name} isn't cyberware.");
        if (c.Foundational) return L.T($"{option.Name} é uma peça-base, não uma Option.", $"{option.Name} is a foundational piece, not an Option.");
        var requires = c.RequiresItemId is { } r ? catalog.Find(r)?.Slug : null;
        var fits = host.Accepts switch
        {
            WearableAccepts.Cybereye => c.Type == CyberwareType.Cyberoptics,
            WearableAccepts.Cyberaudio => c.Type == CyberwareType.Cyberaudio,
            _ => c.Type is CyberwareType.LimbOption or CyberwareType.Cyberfinger && requires is not "cyberleg",
        };
        var kind = host.Accepts switch
        {
            WearableAccepts.Cybereye => "Cybereye Options",
            WearableAccepts.Cyberaudio => "Cyberaudio Options",
            _ => L.T("Cyberarm ou Cyberlimb Options", "Cyberarm or Cyberlimb Options"),
        };
        if (!fits) return L.T($"{hostName} só recebe {kind} ({CharacterRuleBook.Page(host.Source)}).", $"{hostName} only takes {kind} ({CharacterRuleBook.Page(host.Source)}).");
        if (c.Type == CyberwareType.Cyberfinger && host.Cyberfingers == 0)
            return L.T($"{hostName} não recebe Cyberfingers ({CharacterRuleBook.Page(host.Source)}).", $"{hostName} doesn't take Cyberfingers ({CharacterRuleBook.Page(host.Source)}).");
        if (c.RequiresPair && host.NoPaired)
            return L.T($"{option.Name} precisa de par e não pode ir em {hostName} ({CharacterRuleBook.Page(host.Source)}).", $"{option.Name} needs a pair and can't go in {hostName} ({CharacterRuleBook.Page(host.Source)}).");
        return null;
    }

    /// <summary>Option Slots taken in the host: options and, when they use them, the built-in ones. Cyberfingers apart.</summary>
    public static (int Slots, int Fingers) Used(CharacterRuleBook rules, NpcCatalog catalog, WearableHost host, IEnumerable<CatalogItem> options)
    {
        var mechanics = new NpcMechanics(rules.Shared);
        var list = options.ToList();
        var fingers = list.Count(o => o.Cyber?.Type == CyberwareType.Cyberfinger && host.Cyberfingers > 0);
        var slots = list.Where(o => !(o.Cyber?.Type == CyberwareType.Cyberfinger && host.Cyberfingers > 0)).Sum(o => mechanics.SlotsRequired(o).Slots);
        if (host.BuiltInUsesSlots)
            slots += host.BuiltIn.Select(catalog.Find).OfType<CatalogItem>().Sum(o => mechanics.SlotsRequired(o).Slots);
        return (slots, fingers);
    }

    public static IEnumerable<CatalogItem> OptionItems(CharacterSheet sheet, NpcCatalog catalog, InventoryEntry host) =>
        (host.Wearable?.Options ?? [])
            .Select(id => sheet.Inventory.FirstOrDefault(e => e.InstanceId == id))
            .Select(e => e is null ? null : catalog.Find(e.ItemId))
            .OfType<CatalogItem>();

    /// <summary>Implanted pieces switched off by a glove worn over their arm, with the glove that covers them.</summary>
    public static Dictionary<string, string> Covered(CharacterRuleBook rules, CharacterSheet sheet, NpcCatalog catalog)
    {
        var covered = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in sheet.Inventory.Where(e => e.State == ItemState.Equipped && e.Wearable?.Side is not null))
        {
            if (Host(rules, catalog, entry) is not { CoversArm: true } host) continue;
            var side = entry.Wearable!.Side;
            var glove = catalog.Find(entry.ItemId)!.Name;
            foreach (var piece in sheet.Cyberware.Where(c => c.Side == side))
            {
                var item = catalog.Find(piece.ItemId);
                if (item is null || item.Cyber?.Foundational == true) continue;
                var onArm = piece.MeatLimb
                    ? CyberwareAnatomy.LimbOf(item, true) == CyberwareAnatomy.Limb.Arm
                    : Root(sheet, piece) is { } root && catalog.Find(root.ItemId) is { } r && CyberwareAnatomy.LimbOf(r, false) == CyberwareAnatomy.Limb.Arm;
                if (!onArm || host.FingersStillWork && IsFinger(rules, item)) continue;
                covered.TryAdd(piece.InstanceId, glove);
            }
        }
        return covered;
    }

    /// <summary>Everything that works right now: implants not covered by a glove, plus the options of worn hosts.</summary>
    public static List<CatalogItem> Working(CharacterRuleBook rules, CharacterSheet sheet, NpcCatalog catalog)
    {
        var covered = Covered(rules, sheet, catalog);
        var working = sheet.Cyberware.Where(c => !covered.ContainsKey(c.InstanceId))
            .Select(c => catalog.Find(c.ItemId)).OfType<CatalogItem>().ToList();
        foreach (var entry in sheet.Inventory.Where(e => e.State == ItemState.Equipped))
        {
            if (Host(rules, catalog, entry) is not { } host) continue;
            working.AddRange(host.BuiltIn.Select(catalog.Find).OfType<CatalogItem>());
            working.AddRange(OptionItems(sheet, catalog, entry));
        }
        return working;
    }

    public static List<WearableLine> Lines(CharacterRuleBook rules, CharacterSheet sheet, NpcCatalog catalog)
    {
        var covered = Covered(rules, sheet, catalog);
        var mechanics = new NpcMechanics(rules.Shared);
        var lines = new List<WearableLine>();
        foreach (var entry in sheet.Inventory)
        {
            if (Host(rules, catalog, entry) is not { } host) continue;
            var item = catalog.Find(entry.ItemId)!;
            var options = (entry.Wearable?.Options ?? [])
                .Select(id => sheet.Inventory.FirstOrDefault(e => e.InstanceId == id))
                .OfType<InventoryEntry>()
                .Select(e => (Entry: e, Item: catalog.Find(e.ItemId)))
                .Where(x => x.Item is not null)
                .Select(x => new WearableOptionLine(x.Entry.InstanceId, x.Item!.Id, x.Item.Name, mechanics.SlotsRequired(x.Item).Slots,
                    x.Item.Cyber?.Type == CyberwareType.Cyberfinger && host.Cyberfingers > 0))
                .ToList();
            var (slots, fingers) = Used(rules, catalog, host, OptionItems(sheet, catalog, entry));
            var worn = entry.State == ItemState.Equipped;
            var warnings = new List<string>();
            if (worn && host.MaxWorn is { } max
                && sheet.Inventory.Count(e => e.State == ItemState.Equipped && e.ItemId == entry.ItemId) > max)
                warnings.Add(L.T($"Só dá para usar {max} de cada vez ({CharacterRuleBook.Page(host.Source)}).", $"Only {max} can be used at a time ({CharacterRuleBook.Page(host.Source)})."));
            if (worn && host.CoversArm && entry.Wearable?.Side is null)
                warnings.Add(L.T("Escolha a mão em que a luva está: as Options daquele braço ficam inacessíveis.", "Pick the hand the glove is on: that arm's Options become inaccessible."));
            var coveredHere = worn
                ? covered.Where(c => c.Value == item.Name && sheet.Cyberware.FirstOrDefault(p => p.InstanceId == c.Key)?.Side == entry.Wearable?.Side)
                    .Select(c => catalog.Find(sheet.Cyberware.First(p => p.InstanceId == c.Key).ItemId)?.Name ?? "?").ToList()
                : [];
            lines.Add(new WearableLine(entry.InstanceId, item.Id, item.Name, host.Accepts, host.Slots, slots, host.Cyberfingers, fingers,
                worn, entry.Wearable?.Side, host.CoversArm, options,
                host.BuiltIn.Select(s => catalog.Find(s)?.Name ?? s).ToList(), coveredHere, host.Requirement, warnings,
                CharacterRuleBook.Page(host.Source)));
        }
        return lines;
    }

    private static CharacterCyberware? Root(CharacterSheet sheet, CharacterCyberware piece)
    {
        var current = piece;
        for (var guard = 0; current.ParentInstanceId is { } parent && guard < 10; guard++)
            current = sheet.Cyberware.FirstOrDefault(c => c.InstanceId == parent) ?? current;
        return current == piece ? null : current;
    }
}
