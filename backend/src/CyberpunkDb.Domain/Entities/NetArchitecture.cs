using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

/// <summary>
/// A saved NET Architecture. Branches, floors, Demons and Watchers live in <see cref="StateJson"/>; the columns below
/// are denormalized for listing. The cost is recalculated on every read.
/// </summary>
public class NetArchitecture
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NetDifficulty Difficulty { get; set; }
    public int Floors { get; set; }
    public string LocationId { get; set; } = string.Empty;
    /// <summary>Optimistic concurrency: incremented on every save; a stale write is rejected.</summary>
    public int Version { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
