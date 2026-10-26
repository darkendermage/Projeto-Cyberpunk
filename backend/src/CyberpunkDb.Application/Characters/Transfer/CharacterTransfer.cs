using System.Text.RegularExpressions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Transfer;

/// <summary>
/// A file that carries one character between installations: the stored sheet, its history and its portrait. Derived
/// values are never exported; the importing server recalculates them. The sheet points to catalog items by numeric
/// id, which can differ between databases (the catalog is editable), so the file also maps every id it uses to the
/// item's slug, and importing finds the items again by slug.
/// </summary>
public sealed record CharacterExportFile
{
    public const string FormatName = "night-city-character";
    public const int CurrentVersion = 1;

    public string Format { get; init; } = "";
    public int FormatVersion { get; init; }
    public DateTime ExportedAt { get; init; }
    public CharacterSheet? Sheet { get; init; }
    /// <summary>Catalog items referenced by the sheet, keyed by the id the sheet uses.</summary>
    public Dictionary<int, ExportedItem>? Items { get; init; }
    public List<ExportedEvent>? History { get; init; }
    public ExportedPortrait? Portrait { get; init; }
}

public sealed record ExportedItem(string Slug, string Name);

public sealed record ExportedEvent(
    CharacterEventKind Kind, string? Action, int? Amount, string? Before, string? After, string? Description, bool IsManual, DateTime CreatedAt);

/// <summary>Portrait image, base64-encoded.</summary>
public sealed record ExportedPortrait(string ContentType, string Data);

public static partial class CharacterTransfer
{
    /// <summary>History entries kept on import (the most recent ones).</summary>
    public const int MaxHistory = 5000;

    /// <summary>Slug and name of every catalog item the sheet points to, by the id it uses.</summary>
    public static Dictionary<int, ExportedItem> ReferencedItems(CharacterSheet sheet, NpcCatalog catalog) =>
        sheet.Inventory.Select(e => e.ItemId)
            .Concat(sheet.Cyberware.Select(c => c.ItemId))
            .Concat(sheet.Inventory.Select(e => e.Weapon?.LoadedAmmoItemId).OfType<int>())
            .Distinct()
            .Select(catalog.Find)
            .OfType<CatalogItem>()
            .ToDictionary(i => i.Id, i => new ExportedItem(i.Slug, i.Name));

    /// <summary>
    /// Points the sheet to this catalog's ids. An item this catalog doesn't have is left out, together with the
    /// cyberware options installed in it, rather than matched to some other item; each one is reported.
    /// </summary>
    public static (CharacterSheet Sheet, List<string> Missing) Rebind(CharacterSheet sheet, IReadOnlyDictionary<int, ExportedItem> items, NpcCatalog catalog)
    {
        var missing = new List<string>();
        int? Map(int id) => items.TryGetValue(id, out var item) && catalog.Find(item.Slug) is { } found ? found.Id : null;
        string Label(int id) => items.TryGetValue(id, out var item) ? item.Name : $"item #{id}";

        var inventory = new List<InventoryEntry>();
        foreach (var e in sheet.Inventory)
        {
            if (Map(e.ItemId) is { } id) inventory.Add(e with { ItemId = id });
            else missing.Add(Label(e.ItemId));
        }
        var owned = inventory.Select(e => e.InstanceId).ToHashSet();
        inventory = inventory.Select(e =>
        {
            if (e.Wearable is { } worn) e = e with { Wearable = worn with { Options = (worn.Options ?? []).Where(owned.Contains).ToList() } };
            if (e.Weapon is not { } w) return e;
            int? ammo = null;
            if (w.LoadedAmmoItemId is { } loaded)
            {
                ammo = Map(loaded);
                if (ammo is null) missing.Add(L.T($"{Label(loaded)} (munição carregada)", $"{Label(loaded)} (loaded ammo)"));
            }
            return e with { Weapon = w with { LoadedAmmoItemId = ammo, Attachments = (w.Attachments ?? []).Where(owned.Contains).ToList() } };
        }).ToList();

        var dropped = sheet.Cyberware.Where(c => Map(c.ItemId) is null).Select(c => c.InstanceId).ToHashSet();
        missing.AddRange(sheet.Cyberware.Where(c => dropped.Contains(c.InstanceId)).Select(c => Label(c.ItemId)));
        for (var grew = true; grew;)
        {
            var orphans = sheet.Cyberware.Where(c => !dropped.Contains(c.InstanceId) && c.ParentInstanceId is { } p && dropped.Contains(p)).ToList();
            foreach (var o in orphans)
            {
                dropped.Add(o.InstanceId);
                missing.Add(L.T($"{Label(o.ItemId)} (instalado numa peça que ficou de fora)", $"{Label(o.ItemId)} (installed in a piece that was left out)"));
            }
            grew = orphans.Count > 0;
        }
        var cyberware = sheet.Cyberware.Where(c => !dropped.Contains(c.InstanceId)).Select(c => c with { ItemId = Map(c.ItemId)!.Value }).ToList();

        return (sheet with
        {
            Inventory = inventory,
            Cyberware = cyberware,
            Style = sheet.Style with { OutfitItems = (sheet.Style.OutfitItems ?? []).Where(owned.Contains).ToList() },
        }, missing);
    }

    /// <summary>"Suzu-Neon.cyberpunk.json": name and handle without accents or symbols.</summary>
    public static string FileName(CharacterIdentity identity)
    {
        static string Part(string? s) => NonAlphanumeric().Replace(TextNormalizer.Normalize(s), "-").Trim('-');
        var name = string.Join("-", new[] { Part(identity.Name), Part(identity.Handle) }.Where(p => p.Length > 0));
        return $"{(name.Length == 0 ? "personagem" : name)}.cyberpunk.json";
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
