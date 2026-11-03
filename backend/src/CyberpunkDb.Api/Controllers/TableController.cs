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
public sealed class TableController(SeatService seats, MediaService media, TablePresence presence) : ControllerBase
{
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
