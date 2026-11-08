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

// One card on the strip under the map.
public sealed record PartyMemberDto(
    int CharacterId, string Name, int Hp, int MaxHp, WoundState WoundState, int SpHead, int? MaxSpHead, int SpBody, int? MaxSpBody,
    int SeatId, bool Online, bool InScene, string? ImageUrl);

// hub presence
public interface ITablePresence
{
    IReadOnlyCollection<int> OnlineSeats();
}

// for hosts without the hub
public sealed class NoTablePresence : ITablePresence
{
    public IReadOnlyCollection<int> OnlineSeats() => [];
}

// Players see each other's numbers; NPCs only show the Wound State in the fight. Every open seat with a
// character is listed, on the scene or not.
public sealed class TablePartyService(IAppDbContext db, SceneService scenes, CharacterService characters, ITablePresence presence)
{
    // on the scene = a token of the PC this seat can see on the live scene
    public async Task<IReadOnlyList<PartyMemberDto>> ForSeatAsync(TableSeat seat, CancellationToken ct) =>
        await MembersAsync((await scenes.PlayerSceneAsync(seat, ct))?.Tokens ?? [],
            id => $"/api/table/play/party/{id}/portrait?seat={Uri.EscapeDataString(seat.Token)}", ct);

    // live or being prepared
    public async Task<IReadOnlyList<PartyMemberDto>> ForSceneAsync(int sceneId, CancellationToken ct) =>
        await MembersAsync((await scenes.GetAsync(sceneId, ct)).Tokens, id => $"/api/characters/{id}/portrait", ct);

    // only PCs with an open seat
    public async Task<(byte[] Data, string ContentType, DateTime UpdatedAt)> PortraitAsync(int characterId, CancellationToken ct)
    {
        if (!await db.TableSeats.AnyAsync(s => s.CharacterId == characterId && s.RevokedAt == null, ct))
            throw new NotFoundException(L.T("Este personagem não está na mesa.", "This character isn't at the table."));
        return await characters.GetPortraitAsync(characterId, ct);
    }

    // the fight and party update through the sheet broadcast
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
        // PCs on the scene first (token order), then seat order; one card per character
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

public sealed record ArmorAdjustRequest(ArmorSlot Location, int Delta);
