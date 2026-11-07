using CyberpunkDb.Api.Desktop;
using CyberpunkDb.Api.Table;
using CyberpunkDb.Application.Table;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace CyberpunkDb.Api.Controllers;

/// <summary>
/// The GM's side of the virtual table: player seats (invite links) and uploaded images. The players' port never
/// reaches these routes (PlayerSurfaceGuard), except the image download.
/// </summary>
[ApiController]
[Route("api/table")]
public sealed class TableController(SeatService seats, MediaService media, SceneService scenes, TableFightService fights, TableAttackService attacks, TablePartyService party, TableRulesService rules, ChatService chat, TablePresence presence, TunnelService tunnel) : ControllerBase
{
    // ------------------------------------------------------------------ internet tunnel

    [HttpGet("tunnel")]
    public TunnelStatus Tunnel() => tunnel.Status();

    [HttpPost("tunnel/start")]
    public Task<TunnelStatus> StartTunnel(CancellationToken ct) => tunnel.StartAsync(ct);

    [HttpPost("tunnel/stop")]
    public TunnelStatus StopTunnel() => tunnel.Stop();

    [HttpPost("tunnel/download")]
    public Task<TunnelStatus> DownloadTunnel(CancellationToken ct) => tunnel.DownloadAsync(ct);

    // ------------------------------------------------------------------ chat and dice

    [HttpGet("messages")]
    public Task<IReadOnlyList<TableMessageDto>> Messages(CancellationToken ct) => chat.HistoryAsync(null, ct);

    [HttpPost("messages")]
    public Task<TableMessageDto> Send(SendMessageRequest request, CancellationToken ct) => chat.SendAsync(null, request, ct);

    [HttpPost("rolls")]
    public Task<TableMessageDto> Roll(RollRequest request, CancellationToken ct) => chat.RollAsync(null, request, ct);

    [HttpDelete("messages")]
    public async Task<IActionResult> ClearMessages(CancellationToken ct)
    {
        await chat.ClearAsync(ct);
        return NoContent();
    }

    // ------------------------------------------------------------------ scenes

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

    /// <summary>The book's tables to look up during play.</summary>
    [HttpGet("rules")]
    public Task<TableRulesDto> Rules(CancellationToken ct) => rules.GetAsync(ct);

    /// <summary>The PCs with a token on the scene, with their HP and armor.</summary>
    [HttpGet("scenes/{id:int}/party")]
    public Task<IReadOnlyList<PartyMemberDto>> Party(int id, CancellationToken ct) => party.ForSceneAsync(id, ct);

    /// <summary>A new fight from the chosen tokens of the map, linked to the scene.</summary>
    [HttpPost("scenes/{id:int}/fight")]
    public Task<SceneView> StartFight(int id, CreateFightRequest request, CancellationToken ct) => fights.StartAsync(id, request, ct);

    /// <summary>The GM's quick attack from the map with any fighter of the scene's fight; the distance comes from the tokens.</summary>
    [HttpPost("scenes/{id:int}/attack")]
    public Task<QuickAttackResult> Attack(int id, GmQuickAttackRequest request, CancellationToken ct) => attacks.GmAttackAsync(id, request, ct);

    /// <summary>A selection of tokens and areas moved, cleared, hidden or shown in one save.</summary>
    [HttpPost("scenes/{id:int}/batch")]
    public Task<SceneView> Batch(int id, SceneBatchRequest request, CancellationToken ct) => scenes.BatchAsync(id, request, ct);

    [HttpPost("scenes/{id:int}/encounter-tokens")]
    public Task<SceneView> AddEncounterTokens(int id, CancellationToken ct) => scenes.AddEncounterTokensAsync(id, ct);

    /// <summary>A barrier of the cover table on the scenery layer (and the fight's cover, when the scene has a fight).</summary>
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

    // ------------------------------------------------------------------ seats

    [HttpGet("seats")]
    public Task<IReadOnlyList<SeatDto>> Seats(CancellationToken ct) => seats.ListAsync(ct);

    [HttpGet("seats/online")]
    public IReadOnlyCollection<int> Online() => presence.OnlineSeats();

    [HttpPost("seats")]
    public async Task<ActionResult<SeatDto>> CreateSeat(CreateSeatRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await seats.CreateAsync(request, ct));

    [HttpPut("seats/{id:int}")]
    public Task<SeatDto> UpdateSeat(int id, UpdateSeatRequest request, CancellationToken ct) => seats.UpdateAsync(id, request, ct);

    /// <summary>Gives the seat back to the entrance with a new key; whoever held it is thrown out.</summary>
    [HttpPost("seats/{id:int}/release")]
    public async Task<SeatDto> ReleaseSeat(int id, CancellationToken ct)
    {
        var seat = await seats.ReleaseAsync(id, ct);
        presence.Disconnect(id);
        return seat;
    }

    // ------------------------------------------------------------------ general link

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

    // ------------------------------------------------------------------ images

    [HttpPost("media")]
    [RequestSizeLimit(MediaService.MaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaService.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<MediaDto>> Upload(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return StatusCode(StatusCodes.Status201Created, await media.UploadAsync(stream, file.Length, ct));
    }

    /// <summary>Images never change under the same id, so browsers keep them for good.</summary>
    [HttpGet("media/{id:guid}")]
    public async Task<IActionResult> Media(Guid id, CancellationToken ct)
    {
        var (content, type) = await media.OpenAsync(id, ct);
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(content, type, lastModified: null, entityTag: new EntityTagHeaderValue($"\"{id:N}\""));
    }
}
