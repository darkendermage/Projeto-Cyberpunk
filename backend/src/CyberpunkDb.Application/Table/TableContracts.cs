namespace CyberpunkDb.Application.Table;

/// <summary>A seat as the GM manages it: the token builds the invite link.</summary>
/// <see cref="Claimed"/>: someone holds the seat (picked it at the entrance or opened its link) since the app started.
public sealed record SeatDto(int Id, string Label, string Color, int? CharacterId, string Token, DateTime CreatedAt, bool Revoked, bool Claimed);

/// <summary>The seat as its player sees it (no token: the player already has it).</summary>
public sealed record PlayerSeatDto(int Id, string Label, string Color, int? CharacterId, string? DiceColor = null);

public sealed record CreateSeatRequest(string Label, int? CharacterId = null, string? Color = null);
public sealed record UpdateSeatRequest(string Label, int? CharacterId, string Color);

/// <summary>A player's screen on load: their seat and the active scene (null when the GM shows none).</summary>
public sealed record PlayerStateDto(PlayerSeatDto Seat, SceneView? Scene);

/// <summary>A free seat as the general link's entrance shows it: never its key.</summary>
public sealed record LobbySeatDto(int Id, string Label, string Color, string? CharacterName, bool HasPortrait);

/// <summary>The entrance: closed shows nothing; open lists the free seats.</summary>
public sealed record LobbyDto(bool Open, IReadOnlyList<LobbySeatDto> Seats);

public sealed record LobbyStatusDto(bool Open);
public sealed record SetLobbyRequest(bool Open);

/// <summary>The fresh key of the seat a player just picked.</summary>
public sealed record ClaimResultDto(string Token);

public sealed record MediaDto(Guid Id, string Url, string ContentType, long Length);
