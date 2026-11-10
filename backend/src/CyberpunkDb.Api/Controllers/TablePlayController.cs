using CyberpunkDb.Api.Table;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

// Player side of the table, the only API their port can reach. Every call sends the seat token in X-Seat
// (or ?seat= on images, since <img> can't send headers). Wrong or revoked token -> 401.
[ApiController]
[Route("api/table/play")]
public sealed class TablePlayController(
    SeatService seats, SceneService scenes, EncounterService encounters, CharacterService characters,
    NpcPortraitService npcImages, ChatService chat, TablePresence presence,
    TablePartyService party, TableRulesService rules, TableAttackService attacks,
    TableTurnService turns, TableMusicService music, MediaService media,
    PlayerUploadLimit uploads) : ControllerBase
{
    public const string SeatHeader = "X-Seat";

    // general link (no seat key yet)

    // free seats, while the GM keeps the general link open
    [HttpGet("lobby")]
    public Task<LobbyDto> Lobby(CancellationToken ct) => seats.LobbyAsync(ct);

    [HttpGet("lobby/{seatId:int}/portrait")]
    public async Task<IActionResult> LobbyPortrait(int seatId, CancellationToken ct)
    {
        var (data, type, _) = await seats.LobbyPortraitAsync(seatId, ct);
        Response.Headers.CacheControl = "no-store";
        return File(data, type);
    }

    // returns the new key; connections with the old one get dropped
    [HttpPost("lobby/{seatId:int}/claim")]
    public async Task<ClaimResultDto> Claim(int seatId, CancellationToken ct)
    {
        var result = await seats.ClaimAsync(seatId, ct);
        presence.Disconnect(seatId);
        return result;
    }

    // the seat's own screen

    [HttpGet("me")]
    public async Task<PlayerSeatDto> Me(CancellationToken ct) => SeatService.ToPlayer(await SeatAsync(ct));

    // initial load, the hub keeps it updated after
    [HttpGet("state")]
    public async Task<PlayerStateDto> State(CancellationToken ct)
    {
        var seat = await SeatAsync(ct);
        return new PlayerStateDto(SeatService.ToPlayer(seat), await scenes.PlayerSceneAsync(seat, ct), await music.CurrentAsync(ct));
    }

    // 204 when there's no fight
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

    // 204 when the seat has no character
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

    // for party cards of PCs without a token on the scene
    [HttpGet("party/{characterId:int}/portrait")]
    public async Task<IActionResult> PartyPortrait(int characterId, CancellationToken ct)
    {
        await SeatAsync(ct);
        var (data, type, updated) = await party.PortraitAsync(characterId, ct);
        Response.Headers.CacheControl = "private, no-cache";
        return File(data, type, lastModified: updated, entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{updated.Ticks}\""));
    }

    // 204 when the seat has no character
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

    // costs 1 LUCK
    [HttpPost("rolls/{messageId:long}/reroll")]
    public async Task<TableMessageDto> Reroll(long messageId, CancellationToken ct) => await chat.RerollAsync(await SeatAsync(ct), messageId, ct);

    [HttpGet("rules")]
    public async Task<TableRulesDto> Rules(CancellationToken ct)
    {
        await SeatAsync(ct);
        return await rules.GetAsync(ct);
    }

    [HttpGet("party")]
    public async Task<IReadOnlyList<PartyMemberDto>> Party(CancellationToken ct) => await party.ForSeatAsync(await SeatAsync(ct), ct);

    [HttpPost("armor")]
    public async Task<PlayerCharacterDto> Armor(ArmorAdjustRequest request, CancellationToken ct) =>
        await party.AdjustOwnArmorAsync(await SeatAsync(ct), request, ct);

    // only on its Turn, only at fighters it can see
    [HttpPost("attack")]
    public async Task<QuickAttackResult> Attack(QuickAttackRequest request, CancellationToken ct) =>
        await attacks.PlayerAttackAsync(await SeatAsync(ct), request, ct);

    [HttpPost("reload")]
    public async Task<PlayerReloadResult> Reload(PlayerWeaponRequest request, CancellationToken ct) => await turns.ReloadAsync(await SeatAsync(ct), request, ct);

    // modes that stay on the weapon: an Action (or what the rule says) on its Turn in a fight
    [HttpPost("mode")]
    public async Task<PlayerReloadResult> Mode(PlayerWeaponRequest request, CancellationToken ct) => await turns.SwitchModeAsync(await SeatAsync(ct), request, ct);

    // costs an Action
    [HttpPost("unjam")]
    public async Task<PlayerEncounterDto> Unjam(PlayerWeaponRequest request, CancellationToken ct) => await turns.UnjamAsync(await SeatAsync(ct), request, ct);

    [HttpPost("defense")]
    public async Task<PlayerEncounterDto> Defense(PlayerDefenseRequest request, CancellationToken ct) => await turns.DefenseAsync(await SeatAsync(ct), request, ct);

    [HttpPost("critical")]
    public async Task<PlayerEncounterDto> Critical(CancellationToken ct) => await turns.CriticalAsync(await SeatAsync(ct), ct);

    [HttpPost("pick-up")]
    public async Task<PlayerEncounterDto> PickUp(PlayerWeaponRequest request, CancellationToken ct) => await turns.PickUpAsync(await SeatAsync(ct), request, ct);

    // #rrggbb, null = seat color
    [HttpPut("dice-color")]
    public async Task<PlayerSeatDto> DiceColor(DiceColorRequest request, CancellationToken ct) => await seats.SetDiceColorAsync(await SeatAsync(ct), request, ct);

    // Player imports their own sheet (exported from their copy of the app). Only for seats without a character.
    [HttpPost("character/import")]
    [RequestSizeLimit(CharacterService.ImportMaxBytes)]
    public async Task<ActionResult<PlayerSeatDto>> ImportCharacter(CyberpunkDb.Application.Characters.Transfer.CharacterExportFile file, CancellationToken ct)
    {
        var seat = await SeatAsync(ct);
        SeatService.RequireNoCharacter(seat);
        var created = await characters.ImportAsync(file, ct);
        return StatusCode(StatusCodes.Status201Created, await seats.LinkImportedAsync(seat, created.Id, ct));
    }

    // 2 MB max, the browser shrinks it first
    [HttpPost("media")]
    [RequestSizeLimit(MediaService.PlayerMaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaService.PlayerMaxBytes + 64 * 1024)]
    public async Task<ActionResult<MediaDto>> Upload(IFormFile file, CancellationToken ct)
    {
        uploads.Take((await SeatAsync(ct)).Id);
        await using var stream = file.OpenReadStream();
        return StatusCode(StatusCodes.Status201Created, await media.UploadAsync(stream, file.Length, ct, MediaService.PlayerMaxBytes));
    }

    [HttpPost("images")]
    public async Task<SceneView> AddImage(ImageInput input, CancellationToken ct) => await scenes.PlayerAddImageAsync(await SeatAsync(ct), input, ct);

    [HttpPut("images/{imageId}")]
    public async Task<SceneView> UpdateImage(string imageId, ImageInput input, CancellationToken ct)
        => await scenes.PlayerUpdateImageAsync(await SeatAsync(ct), imageId, input, ct);

    [HttpPost("images/{imageId}/order")]
    public async Task<SceneView> OrderImage(string imageId, ImageOrderRequest request, CancellationToken ct)
        => await scenes.PlayerOrderImageAsync(await SeatAsync(ct), imageId, request, ct);

    [HttpDelete("images/{imageId}")]
    public async Task<SceneView> RemoveImage(string imageId, CancellationToken ct) => await scenes.PlayerRemoveImageAsync(await SeatAsync(ct), imageId, ct);

    [HttpPost("end-turn")]
    public async Task<PlayerEncounterDto> EndTurn(CancellationToken ct) => await turns.EndTurnAsync(await SeatAsync(ct), ct);

    [HttpPost("batch")]
    public async Task<SceneView> Batch(SceneBatchRequest request, CancellationToken ct) =>
        await scenes.PlayerBatchAsync(await SeatAsync(ct), request, ct);

    // one per seat
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
