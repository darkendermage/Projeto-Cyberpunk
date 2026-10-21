using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

/// <summary>
/// A player character. The whole sheet (STATs, skills, inventory, cyberware, combat state, lifepath, wallet) lives
/// in <see cref="SheetJson"/>; the columns below are denormalized for listing. Derived values (HP, Humanity,
/// Skill Bases...) are never stored: they are recalculated from the sheet on every read.
/// </summary>
public class Character
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Handle { get; set; }
    public string? Role { get; set; }
    public CreationMethod CreationMethod { get; set; }
    public CharacterStatus Status { get; set; }
    /// <summary>Optimistic concurrency: incremented on every save; a stale write is rejected.</summary>
    public int Version { get; set; }
    public string SheetJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CharacterEvent> Events { get; set; } = [];
    public CharacterPortrait? Portrait { get; set; }
}

/// <summary>
/// Append-only history of one character: money movements, Humanity changes, combat actions, injuries and
/// progression. Deleted together with the character; duplicating a character does not copy it.
/// </summary>
public class CharacterEvent
{
    public long Id { get; set; }
    public int CharacterId { get; set; }
    public CharacterEventKind Kind { get; set; }
    /// <summary>Machine-readable action ("purchase", "install-cyberware", "damage", "ablation"...).</summary>
    public string Action { get; set; } = string.Empty;
    /// <summary>Signed amount when the event moves a number (eb, Humanity, HP, SP).</summary>
    public int? Amount { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>Entered by hand (manual balance adjustment, GM ruling), not produced by a rules operation.</summary>
    public bool IsManual { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Portrait image of a character, kept in the database so it travels with backups and both providers.</summary>
public class CharacterPortrait
{
    public int CharacterId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
