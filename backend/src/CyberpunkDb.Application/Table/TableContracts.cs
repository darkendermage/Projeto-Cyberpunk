namespace CyberpunkDb.Application.Table;

// Token builds the invite link. Claimed: someone has had the seat since the app started.
public sealed record SeatDto(int Id, string Label, string Color, int? CharacterId, string Token, DateTime CreatedAt, bool Revoked, bool Claimed);

// no token, the player already has it
public sealed record PlayerSeatDto(int Id, string Label, string Color, int? CharacterId, string? DiceColor = null);

public sealed record CreateSeatRequest(string Label, int? CharacterId = null, string? Color = null);
public sealed record UpdateSeatRequest(string Label, int? CharacterId, string Color);

// Scene null when the GM shows none
public sealed record PlayerStateDto(PlayerSeatDto Seat, SceneView? Scene, TableMusic? Music = null);

// never the key
public sealed record LobbySeatDto(int Id, string Label, string Color, string? CharacterName, bool HasPortrait);

// closed shows nothing
public sealed record LobbyDto(bool Open, IReadOnlyList<LobbySeatDto> Seats);

public sealed record LobbyStatusDto(bool Open);
public sealed record SetLobbyRequest(bool Open);

public sealed record ClaimResultDto(string Token);

public sealed record MediaDto(Guid Id, string Url, string ContentType, long Length);
