using System.Security.Cryptography;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Transfer;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

// A seat is a secret link: whoever has the token plays it until the GM revokes it. Claiming or releasing a seat
// changes its key, so only one device ever has it.
public sealed class SeatService(IAppDbContext db, TableLobby lobby, ITableNotifier notifier, CharacterService characters)
{
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

    // for good. Messages stay in the history.
    public async Task<SeatDto> RevokeAsync(int id, CancellationToken ct)
    {
        var seat = await FindAsync(id, ct);
        seat.RevokedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(seat);
    }

    // 401 for a wrong or revoked token. A valid key means the seat is taken, so it leaves the lobby.
    public async Task<TableSeat> AuthenticateAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64) throw Denied();
        var seat = await db.TableSeats.AsNoTracking().FirstOrDefaultAsync(s => s.Token == token, ct);
        if (seat is not { RevokedAt: null }) throw Denied();
        if (lobby.MarkClaimed(seat.Id)) await notifier.SeatsAsync(ct);
        return seat;
    }

    // general link

    public LobbyStatusDto LobbyStatus() => new(lobby.Open);

    public LobbyStatusDto SetLobby(SetLobbyRequest r)
    {
        lobby.SetOpen(r.Open);
        return LobbyStatus();
    }

    public async Task<LobbyDto> LobbyAsync(CancellationToken ct)
    {
        if (!lobby.Open) return new LobbyDto(false, []);
        var seats = (await db.TableSeats.AsNoTracking().Where(s => s.RevokedAt == null).OrderBy(s => s.Id).ToListAsync(ct))
            .Where(s => !lobby.IsClaimed(s.Id)).ToList();
        var ids = seats.Where(s => s.CharacterId is not null).Select(s => s.CharacterId!.Value).Distinct().ToList();
        var names = await db.Characters.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, Name = c.Handle ?? c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var portraits = (await db.CharacterPortraits.AsNoTracking().Where(p => ids.Contains(p.CharacterId)).Select(p => p.CharacterId).ToListAsync(ct)).ToHashSet();
        return new LobbyDto(true, seats.Select(s => new LobbySeatDto(s.Id, s.Label, s.Color,
            s.CharacterId is { } c ? names.GetValueOrDefault(c) : null, s.CharacterId is { } p && portraits.Contains(p))).ToList());
    }

    public async Task<(byte[] Data, string ContentType, DateTime UpdatedAt)> LobbyPortraitAsync(int seatId, CancellationToken ct)
    {
        var seat = lobby.Open && !lobby.IsClaimed(seatId) ? await db.TableSeats.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seatId && s.RevokedAt == null, ct) : null;
        var portrait = seat?.CharacterId is { } characterId ? await db.CharacterPortraits.AsNoTracking().FirstOrDefaultAsync(p => p.CharacterId == characterId, ct) : null;
        return portrait is null ? throw new NotFoundException(L.T("Sem retrato.", "No portrait.")) : (portrait.Data, portrait.ContentType, portrait.UpdatedAt);
    }

    // New key, so any older link stops working. First one to claim wins.
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

    // new key kicks the device out, seat goes back to the lobby
    public async Task<SeatDto> ReleaseAsync(int id, CancellationToken ct)
    {
        var seat = await FindAsync(id, ct);
        seat.Token = NewToken();
        await db.SaveChangesAsync(ct);
        lobby.Release(id);
        await notifier.SeatsAsync(ct);
        return ToDto(seat);
    }

    // 409: a new sheet never replaces the one already on the seat
    public static void RequireNoCharacter(TableSeat seat)
    {
        if (seat.CharacterId is not null)
            throw new ConflictException(L.T("Este lugar já tem um personagem. Peça ao mestre para desligá-lo antes de importar outro.", "This seat already has a character. Ask the GM to unlink it before importing another."));
    }

    // The player's own file. A sheet the GM already has (same Uid) is overwritten and linked here, unless another
    // seat plays it; an unknown one is created, but only on a seat without a character.
    public async Task<(PlayerSeatDto Seat, bool Created)> ImportAsync(TableSeat seat, CharacterExportFile file, CancellationToken ct)
    {
        var tracked = await FindAsync(seat.Id, ct);
        if (await characters.IdByUidAsync(file.Uid, ct) is { } existing)
        {
            if (await db.TableSeats.AnyAsync(s => s.CharacterId == existing && s.Id != tracked.Id, ct))
                throw new ConflictException(L.T("Essa ficha pertence a outro lugar da mesa.", "That sheet belongs to another seat at the table."));
            if (tracked.CharacterId is not null && tracked.CharacterId != existing) RequireNoCharacter(tracked);
            await characters.ImportAsync(file, ImportMode.Update, ct);
            return (await LinkAsync(tracked, existing, ct), false);
        }
        RequireNoCharacter(tracked);
        var created = await characters.ImportAsync(file, ImportMode.New, ct);
        return (await LinkAsync(tracked, created.Id, ct), true);
    }

    private async Task<PlayerSeatDto> LinkAsync(TableSeat tracked, int characterId, CancellationToken ct)
    {
        if (tracked.CharacterId != characterId)
        {
            tracked.CharacterId = characterId;
            await db.SaveChangesAsync(ct);
            await notifier.SeatsAsync(ct);
        }
        await notifier.PartyAsync(characterId, ct);
        return ToPlayer(tracked);
    }

    public static PlayerSeatDto ToPlayer(TableSeat s) => new(s.Id, s.Label, s.Color, s.CharacterId, s.DiceColor);

    // null -> back to the seat color
    public async Task<PlayerSeatDto> SetDiceColorAsync(TableSeat seat, DiceColorRequest r, CancellationToken ct)
    {
        var tracked = await FindAsync(seat.Id, ct);
        tracked.DiceColor = TableSettingsService.CleanColor(r.Color);
        await db.SaveChangesAsync(ct);
        return ToPlayer(tracked);
    }

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

    // 128 random bits, URL-safe
    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static ConflictException Taken() =>
        new(L.T("Esse personagem acabou de ser escolhido.", "That character was just taken."));

    private static UnauthorizedException Denied() =>
        new(L.T("Este convite não vale mais. Peça um link novo ao mestre.", "This invite is no longer valid. Ask the GM for a new link."));
}
