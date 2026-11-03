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
public sealed class TablePlayController(SeatService seats, SceneService scenes, EncounterService encounters, CharacterService characters, NpcPortraitService npcImages, ChatService chat) : ControllerBase
{
    public const string SeatHeader = "X-Seat";

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
