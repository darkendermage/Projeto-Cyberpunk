using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

/// <summary>
/// A saved Night Market. Stalls, listings, sales and events live in <see cref="StateJson"/>; the columns below are
/// denormalized for listing. Prices after condition and Haggle are recalculated on every read.
/// </summary>
public class NightMarket
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PresetId { get; set; } = string.Empty;
    public string Letter { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public int? OperatorRank { get; set; }
    public NightMarketStatus Status { get; set; }
    /// <summary>Optimistic concurrency: incremented on every save; a stale write is rejected.</summary>
    public int Version { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
