using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

// Everything lives in StateJson; columns are for the list. Condition/Haggle prices are recomputed on read.
public class NightMarket
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PresetId { get; set; } = string.Empty;
    public string Letter { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public int? OperatorRank { get; set; }
    public NightMarketStatus Status { get; set; }
    // optimistic concurrency, bumped on every save
    public int Version { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
