using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

// Full sheet in SheetJson; the columns are for listing and filtering.
public class Npc
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Concept { get; set; }
    public NpcLevel Level { get; set; }
    public bool Hardened { get; set; }
    public string? ProfileId { get; set; }
    public NpcStatus Status { get; set; }
    public string SheetJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
