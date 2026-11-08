using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

// Everything lives in StateJson; the columns are for the list. PCs keep HP/armor/injuries on their sheets.
public class Encounter
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public EncounterStatus Status { get; set; }
    public int Round { get; set; }
    // optimistic concurrency, bumped on every save
    public int Version { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // null = never used on the table
    public int? SessionId { get; set; }
}
