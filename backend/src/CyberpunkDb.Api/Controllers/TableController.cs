using CyberpunkDb.Api.Desktop;
using CyberpunkDb.Api.Table;
using CyberpunkDb.Application.Table;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace CyberpunkDb.Api.Controllers;

// GM side of the table. The players' port never reaches these routes (PlayerSurfaceGuard), except image downloads.
[ApiController]
[Route("api/table")]
public sealed class TableController(
    SeatService seats, MediaService media, SceneService scenes,
    TableFightService fights, TableAttackService attacks,
    TablePartyService party, TableRulesService rules, ChatService chat, TablePresence presence,
    TunnelService tunnel, TableSessionService sessions, TableSettingsService settings,
    TableMusicService music) : ControllerBase
{
    // internet tunnel

    [HttpGet("tunnel")]
    public TunnelStatus Tunnel() => tunnel.Status();

    [HttpPost("tunnel/start")]
    public Task<TunnelStatus> StartTunnel(CancellationToken ct) => tunnel.StartAsync(ct);

    [HttpPost("tunnel/stop")]
    public TunnelStatus StopTunnel() => tunnel.Stop();

    [HttpPost("tunnel/download")]
    public Task<TunnelStatus> DownloadTunnel(CancellationToken ct) => tunnel.DownloadAsync(ct);

    // chat and dice

    [HttpGet("messages")]
    public Task<IReadOnlyList<TableMessageDto>> Messages(CancellationToken ct) => chat.HistoryAsync(null, ct);

    [HttpPost("messages")]
    public Task<TableMessageDto> Send(SendMessageRequest request, CancellationToken ct) => chat.SendAsync(null, request, ct);

    [HttpPost("rolls")]
    public Task<TableMessageDto> Roll(RollRequest request, CancellationToken ct) => chat.RollAsync(null, request, ct);

    // a Check the GM made from a PC's sheet with LUCK reserved
    [HttpPost("rolls/{messageId:long}/reroll")]
    public Task<TableMessageDto> Reroll(long messageId, CancellationToken ct) => chat.RerollAsync(null, messageId, ct);

    // null = default dice
    [HttpGet("dice-color")]
    public async Task<DiceColorRequest> GmDiceColor(CancellationToken ct) => new(await settings.GetAsync(TableSettingsService.GmDiceColor, ct));

    [HttpPut("dice-color")]
    public async Task<DiceColorRequest> SetGmDiceColor(DiceColorRequest request, CancellationToken ct)
    {
        var color = TableSettingsService.CleanColor(request.Color);
        await settings.SetAsync(TableSettingsService.GmDiceColor, color, ct);
        return new DiceColorRequest(color);
    }

    // music (YouTube)

    [HttpGet("music")]
    public Task<TableMusicStateDto> Music(CancellationToken ct) => music.StateAsync(ct);

    // starts paused
    [HttpPut("music")]
    public Task<TableMusicStateDto> LoadMusic(LoadMusicRequest request, CancellationToken ct) => music.LoadAsync(request, ct);

    [HttpPost("music/play")]
    public Task<TableMusicStateDto> PlayMusic(MusicPositionRequest request, CancellationToken ct) => music.PlayAsync(request, ct);

    [HttpPost("music/pause")]
    public Task<TableMusicStateDto> PauseMusic(CancellationToken ct) => music.PauseAsync(ct);

    [HttpPost("music/seek")]
    public Task<TableMusicStateDto> SeekMusic(MusicPositionRequest request, CancellationToken ct) => music.SeekAsync(request, ct);

    [HttpPost("music/next")]
    public Task<TableMusicStateDto> NextMusic(CancellationToken ct) => music.NextAsync(ct);

    [HttpPost("music/repeat")]
    public Task<TableMusicStateDto> RepeatMusic(MusicRepeatRequest request, CancellationToken ct) => music.RepeatAsync(request, ct);

    [HttpPost("music/volume")]
    public Task<TableMusicStateDto> MusicVolume(MusicVolumeRequest request, CancellationToken ct) => music.VolumeAsync(request, ct);

    [HttpPost("music/recent/forget")]
    public Task<TableMusicStateDto> ForgetRecentMusic(ForgetRecentMusicRequest request, CancellationToken ct) => music.ForgetRecentAsync(request, ct);

    [HttpDelete("music")]
    public Task<TableMusicStateDto> StopMusic(CancellationToken ct) => music.StopAsync(ct);

    // sessions

    [HttpGet("sessions")]
    public Task<IReadOnlyList<TableSessionDto>> Sessions(CancellationToken ct) => sessions.ListAsync(ct);

    // closes the open one with this name and starts an empty chat
    [HttpPost("sessions")]
    public async Task<TableSessionDto> NewSession(NewSessionRequest request, [FromServices] SceneService scenes, CancellationToken ct)
    {
        // the old scenes stay with the old session; the new one starts empty
        await scenes.CloseSessionScenesAsync(ct);
        return await sessions.StartNewAsync(request, ct);
    }

    // a past session becomes the open one again (the open one closes), to edit its scenes and keep playing it
    [HttpPost("sessions/{id:int}/reopen")]
    public async Task<TableSessionDto> ReopenSession(int id, [FromServices] SceneService scenes, CancellationToken ct)
    {
        await sessions.EnsureExistsAsync(id, ct);
        await scenes.CloseSessionScenesAsync(ct);
        return await sessions.ReopenAsync(id, ct);
    }

    [HttpGet("sessions/{id:int}/scenes")]
    public Task<IReadOnlyList<SceneSummaryDto>> SessionScenes(int id, [FromServices] SceneService scenes, CancellationToken ct) => scenes.ListOfSessionAsync(id, ct);

    [HttpPost("scenes/{id:int}/copy-to-current")]
    public Task<SceneView> CopySceneToCurrent(int id, [FromServices] SceneService scenes, CancellationToken ct) => scenes.CopyToCurrentAsync(id, ct);

    [HttpGet("sessions/{id:int}/messages")]
    public Task<IReadOnlyList<TableMessageDto>> SessionMessages(int id, CancellationToken ct) => chat.SessionHistoryAsync(id, ct);

    [HttpPut("sessions/{id:int}")]
    public Task<TableSessionDto> RenameSession(int id, RenameSessionRequest request, CancellationToken ct) => sessions.RenameAsync(id, request, ct);

    [HttpDelete("sessions/{id:int}")]
    public async Task<IActionResult> DeleteSession(int id, CancellationToken ct)
    {
        await sessions.DeleteAsync(id, ct);
        return NoContent();
    }

    // scenes

    [HttpGet("scenes")]
    public Task<IReadOnlyList<SceneSummaryDto>> Scenes(CancellationToken ct) => scenes.ListAsync(ct);

    [HttpPost("scenes")]
    public async Task<ActionResult<SceneView>> CreateScene(CreateSceneRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await scenes.CreateAsync(request, ct));

    [HttpPost("scenes/reorder")]
    public Task<IReadOnlyList<SceneSummaryDto>> ReorderScenes(ReorderScenesRequest request, CancellationToken ct) => scenes.ReorderAsync(request, ct);

    [HttpGet("scenes/{id:int}")]
    public Task<SceneView> Scene(int id, CancellationToken ct) => scenes.GetAsync(id, ct);

    [HttpPut("scenes/{id:int}")]
    public Task<SceneView> UpdateScene(int id, UpdateSceneRequest request, CancellationToken ct) => scenes.UpdateAsync(id, request, ct);

    [HttpPost("scenes/{id:int}/duplicate")]
    public async Task<ActionResult<SceneView>> DuplicateScene(int id, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await scenes.DuplicateAsync(id, ct));

    [HttpDelete("scenes/{id:int}")]
    public async Task<IActionResult> DeleteScene(int id, CancellationToken ct)
    {
        await scenes.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("scenes/{id:int}/activate")]
    public Task<SceneView> ActivateScene(int id, CancellationToken ct) => scenes.ActivateAsync(id, ct);

    [HttpPost("scenes/{id:int}/tokens")]
    public Task<SceneView> AddToken(int id, TokenInput input, CancellationToken ct) => scenes.AddTokenAsync(id, input, ct);

    [HttpGet("rules")]
    public Task<TableRulesDto> Rules(CancellationToken ct) => rules.GetAsync(ct);

    [HttpGet("scenes/{id:int}/party")]
    public Task<IReadOnlyList<PartyMemberDto>> Party(int id, CancellationToken ct) => party.ForSceneAsync(id, ct);

    [HttpPost("scenes/{id:int}/fight")]
    public Task<SceneView> StartFight(int id, CreateFightRequest request, CancellationToken ct) => fights.StartAsync(id, request, ct);

    [HttpPost("scenes/{id:int}/cyber")]
    public Task<CyberpunkDb.Application.Encounters.EncounterDetailDto> ActivateCyber(int id, GmCyberRequest request, [FromServices] TableCyberService cyber, CancellationToken ct) => cyber.GmAsync(id, request, ct);

    // end of session: eb and IP to the chosen characters at once
    [HttpPost("reward")]
    public Task<TableRewardResult> Reward(TableRewardRequest request, [FromServices] TableRewardService rewards, CancellationToken ct) => rewards.RewardAsync(request, ct);

    [HttpPost("scenes/{id:int}/fight/end")]
    public Task<SceneView> EndFight(int id, CancellationToken ct) => fights.EndAsync(id, ct);

    // distance comes from the tokens
    [HttpPost("scenes/{id:int}/attack")]
    public Task<QuickAttackResult> Attack(int id, GmQuickAttackRequest request, CancellationToken ct) => attacks.GmAttackAsync(id, request, ct);

    // paid by the LUCK reserved with the attack
    [HttpPost("scenes/{id:int}/attack/reroll")]
    public Task<QuickAttackResult> RerollAttack(int id, CyberpunkDb.Application.Encounters.AttackRerollRequest request, CancellationToken ct) => attacks.GmRerollAsync(id, request, ct);

    // one save for the whole selection
    [HttpPost("scenes/{id:int}/batch")]
    public Task<SceneView> Batch(int id, SceneBatchRequest request, CancellationToken ct) => scenes.BatchAsync(id, request, ct);

    [HttpPost("scenes/{id:int}/encounter-tokens")]
    public Task<SceneView> AddEncounterTokens(int id, CancellationToken ct) => scenes.AddEncounterTokensAsync(id, ct);

    // also becomes the fight's cover when the scene has one
    [HttpPost("scenes/{id:int}/barriers")]
    public Task<SceneView> AddBarrier(int id, BarrierRequest request, CancellationToken ct) => fights.AddBarrierAsync(id, request, ct);

    [HttpPost("scenes/{id:int}/barriers/{tokenId}/damage")]
    public Task<SceneView> DamageBarrier(int id, string tokenId, BarrierDamageRequest request, CancellationToken ct) =>
        fights.DamageBarrierAsync(id, tokenId, request, ct);

    [HttpPut("scenes/{id:int}/tokens/{tokenId}")]
    public Task<SceneView> UpdateToken(int id, string tokenId, TokenInput input, CancellationToken ct) => scenes.UpdateTokenAsync(id, tokenId, input, ct);

    [HttpPost("scenes/{id:int}/tokens/{tokenId}/move")]
    public Task<SceneView> MoveToken(int id, string tokenId, TokenMoveRequest move, CancellationToken ct) => scenes.MoveTokenAsync(id, tokenId, move, ct);

    [HttpDelete("scenes/{id:int}/tokens/{tokenId}")]
    public Task<SceneView> RemoveToken(int id, string tokenId, CancellationToken ct) => scenes.RemoveTokenAsync(id, tokenId, ct);

    [HttpPost("scenes/{id:int}/templates")]
    public Task<SceneView> AddTemplate(int id, TemplateInput input, CancellationToken ct) => scenes.AddTemplateAsync(id, input, ct);

    [HttpPut("scenes/{id:int}/templates/{templateId}")]
    public Task<SceneView> UpdateTemplate(int id, string templateId, TemplateInput input, CancellationToken ct) => scenes.UpdateTemplateAsync(id, templateId, input, ct);

    [HttpDelete("scenes/{id:int}/templates/{templateId}")]
    public Task<SceneView> RemoveTemplate(int id, string templateId, CancellationToken ct) => scenes.RemoveTemplateAsync(id, templateId, ct);

    // seats

    [HttpGet("seats")]
    public Task<IReadOnlyList<SeatDto>> Seats(CancellationToken ct) => seats.ListAsync(ct);

    [HttpGet("seats/online")]
    public IReadOnlyCollection<int> Online() => presence.OnlineSeats();

    [HttpPost("seats")]
    public async Task<ActionResult<SeatDto>> CreateSeat(CreateSeatRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await seats.CreateAsync(request, ct));

    [HttpPut("seats/{id:int}")]
    public Task<SeatDto> UpdateSeat(int id, UpdateSeatRequest request, CancellationToken ct) => seats.UpdateAsync(id, request, ct);

    // new key, whoever held it gets kicked
    [HttpPost("seats/{id:int}/release")]
    public async Task<SeatDto> ReleaseSeat(int id, CancellationToken ct)
    {
        var seat = await seats.ReleaseAsync(id, ct);
        presence.Disconnect(id);
        return seat;
    }

    // general link

    [HttpGet("lobby")]
    public LobbyStatusDto Lobby() => seats.LobbyStatus();

    [HttpPut("lobby")]
    public LobbyStatusDto SetLobby(SetLobbyRequest request) => seats.SetLobby(request);

    [HttpPost("seats/{id:int}/revoke")]
    public async Task<SeatDto> RevokeSeat(int id, CancellationToken ct)
    {
        var seat = await seats.RevokeAsync(id, ct);
        presence.Disconnect(id);
        return seat;
    }

    // images

    // images over the map

    [HttpPost("scenes/{id:int}/images")]
    public Task<SceneView> AddImage(int id, ImageInput input, CancellationToken ct) => scenes.AddImageAsync(id, input, ct);

    [HttpPut("scenes/{id:int}/images/{imageId}")]
    public Task<SceneView> UpdateImage(int id, string imageId, ImageInput input, CancellationToken ct) => scenes.UpdateImageAsync(id, imageId, input, ct);

    [HttpPost("scenes/{id:int}/images/{imageId}/order")]
    public Task<SceneView> OrderImage(int id, string imageId, ImageOrderRequest request, CancellationToken ct) => scenes.OrderImageAsync(id, imageId, request, ct);

    [HttpDelete("scenes/{id:int}/images/{imageId}")]
    public Task<SceneView> RemoveImage(int id, string imageId, CancellationToken ct) => scenes.RemoveImageAsync(id, imageId, ct);

    [HttpPost("media")]
    [RequestSizeLimit(MediaService.MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaService.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<MediaDto>> Upload(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return StatusCode(StatusCodes.Status201Created, await media.UploadAsync(stream, file.Length, ct));
    }

    // ids are immutable, so cache forever
    [HttpGet("media/{id:guid}")]
    public async Task<IActionResult> Media(Guid id, CancellationToken ct)
    {
        var (content, type) = await media.OpenAsync(id, ct);
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(content, type, lastModified: null, entityTag: new EntityTagHeaderValue($"\"{id:N}\""));
    }
}
