using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

/// <summary>
/// The players' side of the virtual table, the only API their port reaches. Every call carries the seat link token in
/// the X-Seat header; a wrong or revoked one answers 401.
/// </summary>
[ApiController]
[Route("api/table/play")]
public sealed class TablePlayController(SeatService seats) : ControllerBase
{
    public const string SeatHeader = "X-Seat";

    [HttpGet("me")]
    public async Task<PlayerSeatDto> Me(CancellationToken ct) => SeatService.ToPlayer(await SeatAsync(ct));

    private Task<TableSeat> SeatAsync(CancellationToken ct) => seats.AuthenticateAsync(Request.Headers[SeatHeader].FirstOrDefault(), ct);
}
