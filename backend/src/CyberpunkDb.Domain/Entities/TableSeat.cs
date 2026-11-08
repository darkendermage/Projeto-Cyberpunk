namespace CyberpunkDb.Domain.Entities;

// No accounts: the GM sends each player a link with the secret Token. Revoking closes the seat.
public class TableSeat
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    // pointer, pings and chat name
    public string Color { get; set; } = string.Empty;
    public int? CharacterId { get; set; }
    // #rrggbb, null = seat color
    public string? DiceColor { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
