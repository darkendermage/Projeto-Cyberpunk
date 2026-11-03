using CyberpunkDb.Api.Table;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Table;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace CyberpunkDb.Api.Hubs;

/// <summary>
/// The virtual table's live channel. A connection carrying a seat link is that player; one without it is the GM, which
/// only the GM's own port allows. The server pushes scene, combat and chat changes through the groups below.
/// </summary>
public sealed class TableHub(SeatService seats, TablePresence presence, IOptions<TableOptions> options) : Hub
{
    public const string Gm = "gm";
    public const string Players = "players";
    public static string Seat(int id) => $"seat:{id}";

    private const string SeatKey = "seat";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext() ?? throw new HubException("No HTTP context.");
        var token = http.Request.Query["access_token"].FirstOrDefault()
                    ?? http.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        int? seatId = null;
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                seatId = (await seats.AuthenticateAsync(token, Context.ConnectionAborted)).Id;
            }
            catch (UnauthorizedException e)
            {
                throw new HubException(e.Message);
            }
        }
        else if (options.Value.IsPlayerSurface(http))
        {
            throw new HubException("A seat link is required.");
        }

        Context.Items[SeatKey] = seatId;
        if (seatId is { } id)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, Players);
            await Groups.AddToGroupAsync(Context.ConnectionId, Seat(id));
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, Gm);
        }
        presence.Add(Context, seatId);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        presence.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>The seat of this connection; null for the GM.</summary>
    internal int? SeatId => Context.Items.TryGetValue(SeatKey, out var value) ? value as int? : null;
}
