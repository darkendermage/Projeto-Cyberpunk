namespace CyberpunkDb.Domain.Entities;

/// <summary>Image of a saved NPC, kept in the database like a character's portrait. Deleted with the NPC.</summary>
public class NpcPortrait
{
    public int NpcId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Image the GM gave a named NPC of the books. The compendium itself is embedded in the app, so the image is keyed
/// by the compendium id.
/// </summary>
public class CompendiumPortrait
{
    public string CompendiumId { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
