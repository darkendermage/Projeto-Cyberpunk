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
public sealed class TableController(SeatService seats, MediaService media, SceneService scenes, TablePresence presence) : ControllerBase
{
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

    [HttpPost("scenes/{id:int}/encounter-tokens")]
    public Task<SceneView> AddEncounterTokens(int id, CancellationToken ct) => scenes.AddEncounterTokensAsync(id, ct);

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
