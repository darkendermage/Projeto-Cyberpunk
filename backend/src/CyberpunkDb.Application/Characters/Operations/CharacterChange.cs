using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Characters.Operations;

// saved in the same transaction as the sheet
public sealed record PendingEvent(
    CharacterEventKind Kind, string Action, string Description, int? Amount, string? Before, string? After, bool IsManual, string? Payload = null);

// Either finishes (sheet and events saved together) or throws before saving anything.
public sealed class CharacterChange(CharacterSheet sheet, NpcCatalog catalog, DateTime now)
{
    public CharacterSheet Sheet { get; set; } = sheet;
    public NpcCatalog Catalog { get; } = catalog;
    public DateTime Now { get; } = now;
    public List<PendingEvent> Events { get; } = [];
    // e.g. "roll a Critical Injury"
    public List<string> Notices { get; } = [];

    public void Log(CharacterEventKind kind, string action, string description,
        int? amount = null, string? before = null, string? after = null, bool manual = false, string? payload = null) =>
        Events.Add(new PendingEvent(kind, action, description, amount, before, after, manual, payload));

    public CatalogItem Item(int id) =>
        Catalog.Find(id) ?? throw new NotFoundException(L.T($"Item {id} não encontrado no catálogo.", $"Item {id} not found in the catalog."));

    // 400, nothing saved
    public static RequestValidationException Refuse(string field, string message) => RequestValidationException.For(field, message);

    public string NewId(string prefix)
    {
        var used = Sheet.Inventory.Select(i => i.InstanceId)
            .Concat(Sheet.Cyberware.Select(c => c.InstanceId))
            .Concat(Sheet.Injuries.Select(i => i.Id))
            .Concat(Sheet.Modifiers.Select(m => m.Id))
            .ToHashSet(StringComparer.Ordinal);
        for (var n = used.Count + 1; ; n++)
        {
            var id = $"{prefix}{n}";
            if (!used.Contains(id)) return id;
        }
    }
}
