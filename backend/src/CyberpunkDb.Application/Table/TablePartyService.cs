using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

/// <summary>
/// A PC of the table, as the strip under the map shows it: name, HP, Wound State and SP of head and body, the seat
/// that plays it, whether that player is connected and whether the PC has a token on the scene being looked at.
/// </summary>
public sealed record PartyMemberDto(
    int CharacterId, string Name, int Hp, int MaxHp, WoundState WoundState, int SpHead, int? MaxSpHead, int SpBody, int? MaxSpBody,
    int SeatId, bool Online, bool InScene, string? ImageUrl);

/// <summary>Which seats have a live connection now (the table hub's presence).</summary>
public interface ITablePresence
{
    IReadOnlyCollection<int> OnlineSeats();
}

/// <summary>For hosts without the hub.</summary>
public sealed class NoTablePresence : ITablePresence
{
    public IReadOnlyCollection<int> OnlineSeats() => [];
}

/// <summary>
/// The players of the table (each other's numbers are public among them; NPCs stay with the fight's Wound State), and a
/// player's own armor adjusted from the table. Every open seat with a character is listed, on the scene or not.
/// </summary>
public sealed class TablePartyService(IAppDbContext db, SceneService scenes, CharacterService characters, ITablePresence presence)
{
    /// <summary>The party as a seat sees it: on the scene means a token of the PC this seat can see on the live scene.</summary>
    public async Task<IReadOnlyList<PartyMemberDto>> ForSeatAsync(TableSeat seat, CancellationToken ct) =>
        await MembersAsync((await scenes.PlayerSceneAsync(seat, ct))?.Tokens ?? [],
            id => $"/api/table/play/party/{id}/portrait?seat={Uri.EscapeDataString(seat.Token)}", ct);

    /// <summary>The GM's view of a scene (live or being prepared).</summary>
    public async Task<IReadOnlyList<PartyMemberDto>> ForSceneAsync(int sceneId, CancellationToken ct) =>
        await MembersAsync((await scenes.GetAsync(sceneId, ct)).Tokens, id => $"/api/characters/{id}/portrait", ct);

    /// <summary>A party member's portrait for a player: only PCs that have an open seat at the table.</summary>
    public async Task<(byte[] Data, string ContentType, DateTime UpdatedAt)> PortraitAsync(int characterId, CancellationToken ct)
    {
        if (!await db.TableSeats.AnyAsync(s => s.CharacterId == characterId && s.RevokedAt == null, ct))
            throw new NotFoundException(L.T("Este personagem não está na mesa.", "This character isn't at the table."));
        return await characters.GetPortraitAsync(characterId, ct);
    }

    /// <summary>The seat's own armor up or down; the fight and the party follow through the sheet's broadcast.</summary>
    public async Task<PlayerCharacterDto> AdjustOwnArmorAsync(TableSeat seat, ArmorAdjustRequest r, CancellationToken ct)
    {
        if (seat.CharacterId is not { } id)
            throw RequestValidationException.For("location", L.T("Este convite não tem personagem.", "This invite has no character."));
        var detail = await characters.AdjustArmorAsync(id, r.Location, r.Delta, L.T("Mesa virtual", "Virtual table"), ct);
        return PlayerCharacters.From(detail);
    }

    private async Task<IReadOnlyList<PartyMemberDto>> MembersAsync(IReadOnlyList<TokenView> tokens, Func<int, string> portrait, CancellationToken ct)
    {
        var seats = await db.TableSeats.AsNoTracking().Where(s => s.RevokedAt == null && s.CharacterId != null).OrderBy(s => s.Id).ToListAsync(ct);
        var online = presence.OnlineSeats().ToHashSet();
        var onScene = tokens.Where(t => t.CharacterId is not null).Select(t => t.CharacterId!.Value).ToHashSet();
        // PCs on the scene first (their tokens' order), then the seats' order; one card per character.
        var ids = tokens.Where(t => t.CharacterId is not null).Select(t => t.CharacterId!.Value)
            .Concat(seats.Select(s => s.CharacterId!.Value)).Distinct().ToList();
        var members = new List<PartyMemberDto>();
        foreach (var id in ids)
        {
            try
            {
                var c = await characters.GetAsync(id, ct);
                var r = c.Report;
                var seatsOf = seats.Where(s => s.CharacterId == id).ToList();
                var seatId = seatsOf.FirstOrDefault(s => online.Contains(s.Id))?.Id ?? seatsOf.FirstOrDefault()?.Id ?? 0;
                members.Add(new PartyMemberDto(c.Id, c.Sheet.Identity.Handle is { Length: > 0 } h ? h : c.Sheet.Identity.Name, r.Health.CurrentHp, r.Health.MaxHp,
                    r.Health.WoundState, r.Armor.Head.Sp, r.Armor.Head.MaxSp, r.Armor.Body.Sp, r.Armor.Body.MaxSp,
                    seatId, seatsOf.Any(s => online.Contains(s.Id)), onScene.Contains(id),
                    c.HasPortrait ? $"{portrait(id)}{(portrait(id).Contains('?') ? '&' : '?')}v={c.UpdatedAt.Ticks}" : null));
            }
            catch (NotFoundException) { /* a token of a deleted character */ }
        }
        return members;
    }
}

/// <summary>SP of one location up or down from the table's +/−.</summary>
public sealed record ArmorAdjustRequest(ArmorSlot Location, int Delta);
