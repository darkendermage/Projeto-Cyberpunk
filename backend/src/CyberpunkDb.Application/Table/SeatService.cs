using System.Security.Cryptography;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

/// <summary>
/// The players' seats at the virtual table. A seat is a secret link: whoever holds the token plays it, until the GM
/// revokes it.
/// </summary>
public sealed class SeatService(IAppDbContext db)
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

    /// <summary>The open seat a link token belongs to; 401 for a wrong or revoked one.</summary>
    public async Task<TableSeat> AuthenticateAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64) throw Denied();
        var seat = await db.TableSeats.AsNoTracking().FirstOrDefaultAsync(s => s.Token == token, ct);
        return seat is { RevokedAt: null } ? seat : throw Denied();
    }

    public static PlayerSeatDto ToPlayer(TableSeat s) => new(s.Id, s.Label, s.Color, s.CharacterId);

    private static SeatDto ToDto(TableSeat s) => new(s.Id, s.Label, s.Color, s.CharacterId, s.Token, s.CreatedAt, s.RevokedAt is not null);

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

    private static UnauthorizedException Denied() =>
        new(L.T("Este convite não vale mais. Peça um link novo ao mestre.", "This invite is no longer valid. Ask the GM for a new link."));
}
