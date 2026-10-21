using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Operations;

/// <summary>An event produced by an operation, written in the same transaction as the sheet.</summary>
public sealed record PendingEvent(
    CharacterEventKind Kind, string Action, string Description, int? Amount, string? Before, string? After, bool IsManual);

/// <summary>
/// One operation on a character: the sheet being changed, the catalog snapshot it is checked against, and the
/// history entries it produces. Operations either finish (sheet and events saved together) or throw before
/// anything is persisted, so a failed validation never leaves a partial change.
/// </summary>
public sealed class CharacterChange(CharacterSheet sheet, NpcCatalog catalog, DateTime now)
{
    public CharacterSheet Sheet { get; set; } = sheet;
    public NpcCatalog Catalog { get; } = catalog;
    public DateTime Now { get; } = now;
    public List<PendingEvent> Events { get; } = [];
    /// <summary>Messages for the player about what the rules now require (e.g. "roll a Critical Injury").</summary>
    public List<string> Notices { get; } = [];

    public void Log(CharacterEventKind kind, string action, string description,
        int? amount = null, string? before = null, string? after = null, bool manual = false) =>
        Events.Add(new PendingEvent(kind, action, description, amount, before, after, manual));

    public CatalogItem Item(int id) =>
        Catalog.Find(id) ?? throw new NotFoundException($"Item {id} não encontrado no catálogo.");

    /// <summary>Refuses the operation with a field error (HTTP 400); nothing is saved.</summary>
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
