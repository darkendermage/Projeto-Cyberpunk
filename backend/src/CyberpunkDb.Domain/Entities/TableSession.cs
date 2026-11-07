namespace CyberpunkDb.Domain.Entities;

/// <summary>
/// A game session of the virtual table: its chat history and the fights started or linked while it was open. Exactly
/// one session is open (<see cref="EndedAt"/> null); "New session" closes it with a name and opens the next one.
/// </summary>
public class TableSession
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

/// <summary>A setting of the virtual table kept across restarts (the GM's dice color, the music), by key.</summary>
public class TableSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
