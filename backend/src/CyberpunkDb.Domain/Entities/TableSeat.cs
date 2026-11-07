namespace CyberpunkDb.Domain.Entities;

/// <summary>
/// A player's place at the virtual table. There are no accounts: the GM sends each player a link with the secret
/// <see cref="Token"/>, and revoking the seat closes it.
/// </summary>
public class TableSeat
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>CSS color of the player's pointer, pings and chat name.</summary>
    public string Color { get; set; } = string.Empty;
    /// <summary>The player character this seat plays, if any.</summary>
    public int? CharacterId { get; set; }
    /// <summary>The player's dice color (#rrggbb); null uses the seat's color.</summary>
    public string? DiceColor { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
