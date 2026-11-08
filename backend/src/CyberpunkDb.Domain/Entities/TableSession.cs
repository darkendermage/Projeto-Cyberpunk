namespace CyberpunkDb.Domain.Entities;

// Exactly one session is open (EndedAt null). "New session" closes it and opens the next.
public class TableSession
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

// GM dice color, music... by key
public class TableSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
