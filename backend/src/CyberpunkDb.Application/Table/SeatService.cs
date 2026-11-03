using System.Security.Cryptography;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

/// <summary>
/// The players' seats at the virtual table. A seat is a secret link: whoever holds the token plays it, until the GM
/// revokes it. With the entrance open, the general link lets a player pick a free seat; picking or releasing a seat
/// changes its key, so only one device ever holds it.
/// </summary>
public sealed class SeatService(IAppDbContext db, TableLobby lobby, ITableNotifier notifier)
{
    /// <summary>Neon colors handed out in turn to new seats.</summary>
    private static readonly string[] Palette = ["#00f0ff", "#ff2a6d", "#f5e663", "#05ffa1", "#b967ff", "#ff9f1c", "#4d9de0", "#ff6ec7"];

    public async Task<IReadOnlyList<SeatDto>> ListAsync(CancellationToken ct) =>
        (await db.TableSeats.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<SeatDto> CreateAsync(CreateSeatRequest r, CancellationToken ct)
    {
        var label = RequireLabel(r.Label);
        await RequireCharacterAsync(r.CharacterId, ct);
        var count = await db.TableSeats.CountAsync(ct);
        var seat = new TableSeat
        {
            Token = NewToken(), Label = label, CharacterId = r.CharacterId,
            Color = Color(r.Color) ?? Palette[count % Palette.Length], CreatedAt = DateTime.UtcNow,
        };
        db.TableSeats.Add(seat);
        await db.SaveChangesAsync(ct);
        return ToDto(seat);
    }

    public async Task<SeatDto> UpdateAsync(int id, UpdateSeatRequest r, CancellationToken ct)
    {
        var seat = await FindAsync(id, ct);
        seat.Label = RequireLabel(r.Label);
        await RequireCharacterAsync(r.CharacterId, ct);
        seat.CharacterId = r.CharacterId;
        seat.Color = Color(r.Color) ?? throw RequestValidationException.For("color", L.T("Use uma cor no formato #rrggbb.", "Use a color in the #rrggbb format."));
        await db.SaveChangesAsync(ct);
        return ToDto(seat);
    }

    /// <summary>Closes the seat for good: its link stops working. The history keeps its messages.</summary>
    public async Task<SeatDto> RevokeAsync(int id, CancellationToken ct)
    {
        var seat = await FindAsync(id, ct);
        seat.RevokedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(seat);
    }

    /// <summary>
    /// The open seat a link token belongs to; 401 for a wrong or revoked one. Whoever holds a valid key holds the seat,
    /// so it leaves the entrance.
    /// </summary>
    public async Task<TableSeat> AuthenticateAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64) throw Denied();
        var seat = await db.TableSeats.AsNoTracking().FirstOrDefaultAsync(s => s.Token == token, ct);
        if (seat is not { RevokedAt: null }) throw Denied();
        if (lobby.MarkClaimed(seat.Id)) await notifier.SeatsAsync(ct);
        return seat;
    }

    // ------------------------------------------------------------------ general link

    public LobbyStatusDto LobbyStatus() => new(lobby.Open);

    public LobbyStatusDto SetLobby(SetLobbyRequest r)
    {
        lobby.SetOpen(r.Open);
        return LobbyStatus();
    }

    /// <summary>The free seats, while the entrance is open: label, color and the character's name and portrait.</summary>
    public async Task<LobbyDto> LobbyAsync(CancellationToken ct)
    {
        if (!lobby.Open) return new LobbyDto(false, []);
        var seats = (await db.TableSeats.AsNoTracking().Where(s => s.RevokedAt == null).OrderBy(s => s.Id).ToListAsync(ct))
            .Where(s => !lobby.IsClaimed(s.Id)).ToList();
        var ids = seats.Where(s => s.CharacterId is not null).Select(s => s.CharacterId!.Value).Distinct().ToList();
        var names = await db.Characters.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Handle ?? c.Name, ct);
        var portraits = (await db.CharacterPortraits.AsNoTracking().Where(p => ids.Contains(p.CharacterId)).Select(p => p.CharacterId).ToListAsync(ct)).ToHashSet();
        return new LobbyDto(true, seats.Select(s => new LobbySeatDto(s.Id, s.Label, s.Color,
            s.CharacterId is { } c ? names.GetValueOrDefault(c) : null, s.CharacterId is { } p && portraits.Contains(p))).ToList());
    }

    /// <summary>The portrait of a free seat's character, while the entrance is open.</summary>
    public async Task<(byte[] Data, string ContentType, DateTime UpdatedAt)> LobbyPortraitAsync(int seatId, CancellationToken ct)
    {
        var seat = lobby.Open && !lobby.IsClaimed(seatId) ? await db.TableSeats.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seatId && s.RevokedAt == null, ct) : null;
        var portrait = seat?.CharacterId is { } characterId ? await db.CharacterPortraits.AsNoTracking().FirstOrDefaultAsync(p => p.CharacterId == characterId, ct) : null;
        return portrait is null ? throw new NotFoundException(L.T("Sem retrato.", "No portrait.")) : (portrait.Data, portrait.ContentType, portrait.UpdatedAt);
    }

    /// <summary>
    /// A player picks a free seat at the entrance and gets a fresh key: any earlier link of the seat stops working, so
    /// only this device holds it. The first of two players picking the same seat wins.
    /// </summary>
    public async Task<ClaimResultDto> ClaimAsync(int seatId, CancellationToken ct)
    {
        if (!lobby.Open) throw new NotFoundException(L.T("A entrada está fechada. Peça ao mestre para abrir.", "The table is closed. Ask the GM to open it."));
        var seat = await FindAsync(seatId, ct);
        if (seat.RevokedAt is not null || !lobby.TryClaim(seatId)) throw Taken();
        try
        {
            seat.Token = NewToken();
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            lobby.Release(seatId);
            throw;
        }
        await notifier.SeatsAsync(ct);
        return new ClaimResultDto(seat.Token);
    }

    /// <summary>The GM frees a seat: its key changes (the device holding it is thrown out) and it goes back to the entrance.</summary>
    public async Task<SeatDto> ReleaseAsync(int id, CancellationToken ct)
    {
        var seat = await FindAsync(id, ct);
        seat.Token = NewToken();
        await db.SaveChangesAsync(ct);
        lobby.Release(id);
        await notifier.SeatsAsync(ct);
        return ToDto(seat);
    }

    public static PlayerSeatDto ToPlayer(TableSeat s) => new(s.Id, s.Label, s.Color, s.CharacterId);

    private SeatDto ToDto(TableSeat s) => new(s.Id, s.Label, s.Color, s.CharacterId, s.Token, s.CreatedAt, s.RevokedAt is not null, lobby.IsClaimed(s.Id));

    private async Task<TableSeat> FindAsync(int id, CancellationToken ct) =>
        await db.TableSeats.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException(L.T("Assento não encontrado.", "Seat not found."));

    private async Task RequireCharacterAsync(int? id, CancellationToken ct)
    {
        if (id is { } characterId && !await db.Characters.AnyAsync(c => c.Id == characterId, ct))
            throw RequestValidationException.For("characterId", L.T("Personagem não encontrado.", "Character not found."));
    }

    private static string RequireLabel(string? label)
    {
        var trimmed = label?.Trim() ?? "";
        if (trimmed.Length is 0 or > 60) throw RequestValidationException.For("label", L.T("Dê um nome ao assento (até 60 caracteres).", "Name the seat (up to 60 characters)."));
        return trimmed;
    }

    private static string? Color(string? color) =>
        color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit) ? color.ToLowerInvariant() : null;

    /// <summary>128 random bits, URL-safe.</summary>
    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static ConflictException Taken() =>
        new(L.T("Esse personagem acabou de ser escolhido.", "That character was just taken."));

    private static UnauthorizedException Denied() =>
        new(L.T("Este convite não vale mais. Peça um link novo ao mestre.", "This invite is no longer valid. Ask the GM for a new link."));
}
