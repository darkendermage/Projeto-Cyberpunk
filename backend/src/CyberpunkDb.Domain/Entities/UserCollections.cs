namespace CyberpunkDb.Domain.Entities;

public class Favorite
{
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A named equipment list (loadout, shopping list, character inventory...).</summary>
public class GearList
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<GearListEntry> Entries { get; set; } = [];
}

public class GearListEntry
{
    public int Id { get; set; }
    public int GearListId { get; set; }
    public int ItemId { get; set; }
    public Item? Item { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Note { get; set; }
    public DateTime AddedAt { get; set; }
}
