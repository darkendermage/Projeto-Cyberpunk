using CyberpunkDb.Api.Table;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

/// <summary>
/// The players' side of the virtual table, the only API their port reaches. Every call carries the seat link token in
/// the X-Seat header (or ?seat= on images, which load without headers); a wrong or revoked one answers 401.
/// </summary>
[ApiController]
[Route("api/table/play")]
public sealed class TablePlayController(SeatService seats, SceneService scenes, EncounterService encounters, CharacterService characters, NpcPortraitService npcImages, ChatService chat, TablePresence presence, TablePartyService party, TableRulesService rules, TableAttackService attacks) : ControllerBase
{
    public const string SeatHeader = "X-Seat";

    // ------------------------------------------------------------------ general link (no seat key yet)

    /// <summary>The entrance of the general link: the free seats while the GM keeps it open.</summary>
    [HttpGet("lobby")]
    public Task<LobbyDto> Lobby(CancellationToken ct) => seats.LobbyAsync(ct);

    [HttpGet("lobby/{seatId:int}/portrait")]
    public async Task<IActionResult> LobbyPortrait(int seatId, CancellationToken ct)
    {
        var (data, type, _) = await seats.LobbyPortraitAsync(seatId, ct);
        Response.Headers.CacheControl = "no-store";
        return File(data, type);
    }

    /// <summary>Takes a free seat; answers its new key. Connections still holding the old one are dropped.</summary>
    [HttpPost("lobby/{seatId:int}/claim")]
    public async Task<ClaimResultDto> Claim(int seatId, CancellationToken ct)
    {
        var result = await seats.ClaimAsync(seatId, ct);
        presence.Disconnect(seatId);
        return result;
    }

    // ------------------------------------------------------------------ the seat's own screen

    [HttpGet("me")]
    public async Task<PlayerSeatDto> Me(CancellationToken ct) => SeatService.ToPlayer(await SeatAsync(ct));

    /// <summary>Everything a player's screen needs on load; the hub keeps it current afterwards.</summary>
    [HttpGet("state")]
    public async Task<PlayerStateDto> State(CancellationToken ct)
    {
        var seat = await SeatAsync(ct);
        return new PlayerStateDto(SeatService.ToPlayer(seat), await scenes.PlayerSceneAsync(seat, ct));
    }

    /// <summary>The fight linked to the active scene, as this seat sees it (204 when there is none).</summary>
    [HttpGet("encounter")]
    public async Task<PlayerEncounterDto?> Encounter(CancellationToken ct)
    {
        var seat = await SeatAsync(ct);
        var (encounterId, state) = await scenes.ActiveAsync(ct);
        if (encounterId is not { } id) return null;
        try
        {
            return PlayerEncounters.For(await encounters.GetAsync(id, ct), seat, state);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    /// <summary>The seat's own character, for skill checks from the token (204 when the seat plays none).</summary>
    [HttpGet("character")]
    public async Task<PlayerCharacterDto?> Character(CancellationToken ct)
    {
        var seat = await SeatAsync(ct);
        if (seat.CharacterId is not { } id) return null;
        try
        {
            return PlayerCharacters.From(await characters.GetAsync(id, ct));
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    [HttpGet("messages")]
    public async Task<IReadOnlyList<TableMessageDto>> Messages(CancellationToken ct) => await chat.HistoryAsync(await SeatAsync(ct), ct);

    [HttpPost("messages")]
    public async Task<TableMessageDto> Send(SendMessageRequest request, CancellationToken ct) => await chat.SendAsync(await SeatAsync(ct), request, ct);

    [HttpPost("rolls")]
    public async Task<TableMessageDto> Roll(RollRequest request, CancellationToken ct) => await chat.RollAsync(await SeatAsync(ct), request, ct);

    [HttpPost("tokens/{tokenId}/move")]
    public async Task<SceneView> MoveToken(string tokenId, TokenMoveRequest move, CancellationToken ct) =>
        await scenes.PlayerMoveTokenAsync(await SeatAsync(ct), tokenId, move, ct);

    [HttpGet("tokens/{tokenId}/portrait")]
    public async Task<IActionResult> TokenPortrait(string tokenId, CancellationToken ct)
    {
        var source = await scenes.PlayerTokenImageAsync(await SeatAsync(ct), tokenId, ct);
        var (data, type, updated) = source.Kind switch
        {
            TokenImageKind.Character => await characters.GetPortraitAsync(int.Parse(source.Key), ct),
            TokenImageKind.Npc => await npcImages.GetSavedAsync(int.Parse(source.Key), ct),
            _ => await npcImages.GetCompendiumAsync(source.Key, ct),
        };
        Response.Headers.CacheControl = "private, no-cache";
        return File(data, type, lastModified: updated, entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{updated.Ticks}\""));
    }

    /// <summary>The seat's own sheet to read (204 when the seat plays no character).</summary>
    [HttpGet("sheet")]
    public async Task<PlayerSheetDto?> Sheet(CancellationToken ct)
    {
        var seat = await SeatAsync(ct);
        if (seat.CharacterId is not { } id) return null;
        try
        {
            return PlayerCharacters.Sheet(await characters.GetAsync(id, ct));
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    /// <summary>Rolls one of the player's own Checks again for 1 LUCK (the table's reroll).</summary>
    [HttpPost("rolls/{messageId:long}/reroll")]
    public async Task<TableMessageDto> Reroll(long messageId, CancellationToken ct) => await chat.RerollAsync(await SeatAsync(ct), messageId, ct);

    /// <summary>The book's tables to look up during play.</summary>
    [HttpGet("rules")]
    public async Task<TableRulesDto> Rules(CancellationToken ct)
    {
        await SeatAsync(ct);
        return await rules.GetAsync(ct);
    }

    /// <summary>The PCs on the live scene the seat can see, with their HP and armor.</summary>
    [HttpGet("party")]
    public async Task<IReadOnlyList<PartyMemberDto>> Party(CancellationToken ct) => await party.ForSeatAsync(await SeatAsync(ct), ct);

    /// <summary>The seat's own armor up (repair) or down (ablation) on one location.</summary>
    [HttpPost("armor")]
    public async Task<PlayerCharacterDto> Armor(ArmorAdjustRequest request, CancellationToken ct) =>
        await party.AdjustOwnArmorAsync(await SeatAsync(ct), request, ct);

    /// <summary>An attack with the seat's own character, on its Turn, at fighters it sees; the fight resolves it.</summary>
    [HttpPost("attack")]
    public async Task<TableAttack> Attack(QuickAttackRequest request, CancellationToken ct) =>
        await attacks.PlayerAttackAsync(await SeatAsync(ct), request, ct);

    /// <summary>The player's selection moved or cleared at once (their tokens, players' areas).</summary>
    [HttpPost("batch")]
    public async Task<SceneView> Batch(SceneBatchRequest request, CancellationToken ct) =>
        await scenes.PlayerBatchAsync(await SeatAsync(ct), request, ct);

    /// <summary>The player's own token on the live scene (one per seat).</summary>
    [HttpPost("my-token")]
    public async Task<SceneView> PlaceOwnToken(PlaceTokenRequest request, CancellationToken ct) =>
        await scenes.PlayerPlaceOwnTokenAsync(await SeatAsync(ct), request, ct);

    [HttpPost("templates")]
    public async Task<SceneView> AddTemplate(TemplateInput input, CancellationToken ct) =>
        await scenes.PlayerAddTemplateAsync(await SeatAsync(ct), input, ct);

    [HttpPut("templates/{templateId}")]
    public async Task<SceneView> UpdateTemplate(string templateId, TemplateInput input, CancellationToken ct) =>
        await scenes.PlayerUpdateTemplateAsync(await SeatAsync(ct), templateId, input, ct);

    [HttpDelete("templates/{templateId}")]
    public async Task<SceneView> RemoveTemplate(string templateId, CancellationToken ct) =>
        await scenes.PlayerRemoveTemplateAsync(await SeatAsync(ct), templateId, ct);

    private Task<TableSeat> SeatAsync(CancellationToken ct) =>
        seats.AuthenticateAsync(Request.Headers[SeatHeader].FirstOrDefault() ?? Request.Query["seat"].FirstOrDefault(), ct);
}
