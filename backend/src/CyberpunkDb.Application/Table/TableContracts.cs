namespace CyberpunkDb.Application.Table;

/// <summary>A seat as the GM manages it: the token builds the invite link.</summary>
public sealed record SeatDto(int Id, string Label, string Color, int? CharacterId, string Token, DateTime CreatedAt, bool Revoked);

/// <summary>The seat as its player sees it (no token: the player already has it).</summary>
public sealed record PlayerSeatDto(int Id, string Label, string Color, int? CharacterId);

public sealed record CreateSeatRequest(string Label, int? CharacterId = null, string? Color = null);
public sealed record UpdateSeatRequest(string Label, int? CharacterId, string Color);

public sealed record MediaDto(Guid Id, string Url, string ContentType, long Length);
