using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

// The whole sheet lives in SheetJson; the other columns are just for the list. Derived values are never stored.
public class Character
{
    public int Id { get; set; }
    // stable identity across installs; goes in the exported file so a re-import updates instead of duplicating
    public Guid? Uid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Handle { get; set; }
    public string? Role { get; set; }
    public CreationMethod CreationMethod { get; set; }
    public CharacterStatus Status { get; set; }
    // optimistic concurrency, bumped on every save
    public int Version { get; set; }
    public string SheetJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CharacterEvent> Events { get; set; } = [];
    public CharacterPortrait? Portrait { get; set; }
}

// Append-only. Deleted with the character, not copied on duplicate.
public class CharacterEvent
{
    public long Id { get; set; }
    public int CharacterId { get; set; }
    public CharacterEventKind Kind { get; set; }
    // "purchase", "install-cyberware", "damage", "ablation"...
    public string Action { get; set; } = string.Empty;
    // signed: eb, Humanity, HP, SP
    public int? Amount { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string Description { get; set; } = string.Empty;
    // manual adjustment / GM ruling
    public bool IsManual { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

// in the DB so it goes along with backups, on both providers
public class CharacterPortrait
{
    public int CharacterId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
