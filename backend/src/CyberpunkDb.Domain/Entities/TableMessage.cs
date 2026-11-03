using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

/// <summary>A chat line or a dice roll of the virtual table, kept as the session history.</summary>
public class TableMessage
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    /// <summary>Null when the GM wrote it.</summary>
    public int? AuthorSeatId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public TableMessageKind Kind { get; set; }
    public TableVisibility Visibility { get; set; }
    /// <summary>Whisper target.</summary>
    public int? TargetSeatId { get; set; }
    public string Text { get; set; } = string.Empty;
    /// <summary>The roll, term by term, for roll messages.</summary>
    public string? RollJson { get; set; }
}
