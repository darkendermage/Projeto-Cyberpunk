using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

public class TableMessage
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    // null = GM
    public int? AuthorSeatId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public TableMessageKind Kind { get; set; }
    public TableVisibility Visibility { get; set; }
    // whisper target
    public int? TargetSeatId { get; set; }
    public string Text { get; set; } = string.Empty;
    // the roll (per term) or the attack
    public string? RollJson { get; set; }
    // null only for lines from before sessions existed
    public int? SessionId { get; set; }
}
