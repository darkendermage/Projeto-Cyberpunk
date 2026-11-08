namespace CyberpunkDb.Domain.Entities;

// Everything lives in StateJson. Only one scene is Active at a time (the one players see).
public class Scene
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? EncounterId { get; set; }
    public bool Active { get; set; }
    public int SortOrder { get; set; }
    // only for settings edits; token moves are last-write-wins
    public int Version { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
