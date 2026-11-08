namespace CyberpunkDb.Domain.Entities;

// deleted with the NPC
public class NpcPortrait
{
    public int NpcId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}

// Keyed by compendium id since the compendium itself ships embedded in the app.
public class CompendiumPortrait
{
    public string CompendiumId { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
