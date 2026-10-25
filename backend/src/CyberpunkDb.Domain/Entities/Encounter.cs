using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

/// <summary>
/// A saved combat (Friday Night Firefight). Combatants, the Initiative Queue, cover and the log live in
/// <see cref="StateJson"/>; the columns below are denormalized for listing. Player characters keep their HP, armor and
/// injuries on their own sheets.
/// </summary>
public class Encounter
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public EncounterStatus Status { get; set; }
    public int Round { get; set; }
    /// <summary>Optimistic concurrency: incremented on every save; a stale write is rejected.</summary>
    public int Version { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
