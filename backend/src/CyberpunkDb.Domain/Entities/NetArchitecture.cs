using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

// Everything lives in StateJson; columns are for the list. Cost is recomputed on read.
public class NetArchitecture
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NetDifficulty Difficulty { get; set; }
    public int Floors { get; set; }
    public string LocationId { get; set; } = string.Empty;
    // optimistic concurrency, bumped on every save
    public int Version { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
