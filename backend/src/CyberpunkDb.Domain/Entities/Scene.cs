namespace CyberpunkDb.Domain.Entities;

/// <summary>
/// A map of the virtual table, prepared ahead and reused: background, grid, tokens and area templates live in
/// <see cref="StateJson"/>. One scene at a time is <see cref="Active"/>: the one the players see.
/// </summary>
public class Scene
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>The fight whose combatants the tokens stand for (Initiative, HP bars, the active turn).</summary>
    public int? EncounterId { get; set; }
    public bool Active { get; set; }
    public int SortOrder { get; set; }
    /// <summary>Optimistic concurrency for scene edits (map, grid); token moves are last-write-wins.</summary>
    public int Version { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
