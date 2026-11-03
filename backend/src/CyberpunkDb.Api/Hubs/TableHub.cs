using CyberpunkDb.Api.Table;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace CyberpunkDb.Api.Hubs;

/// <summary>
/// The virtual table's live channel. A connection carrying a seat link is that player; one without it is the GM, which
/// only the GM's own port allows. The server pushes scene, combat and chat changes through the groups below; clients
/// send only fleeting things here (drag previews, pings) — every lasting change goes through the HTTP API.
/// </summary>
public sealed class TableHub(SeatService seats, SceneService scenes, TablePresence presence, IOptions<TableOptions> options) : Hub
{
    public const string Gm = "gm";
    public const string Players = "players";
    public static string Seat(int id) => $"seat:{id}";

    private const string SeatKey = "seat";
    private const string GmPingColor = "#f5e663";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext() ?? throw new HubException("No HTTP context.");
        var token = http.Request.Query["access_token"].FirstOrDefault()
                    ?? http.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        TableSeat? seat = null;
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                seat = await seats.AuthenticateAsync(token, Context.ConnectionAborted);
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

        Context.Items[SeatKey] = seat;
        if (seat is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, Players);
            await Groups.AddToGroupAsync(Context.ConnectionId, Seat(seat.Id));
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, Gm);
        }
        presence.Add(Context, seat?.Id);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        presence.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Where a token is while someone drags it (the drop goes through the API). Others see it slide; a token hidden
    /// from some players only previews to the GM.
    /// </summary>
    public async Task DragToken(int sceneId, string tokenId, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || tokenId.Length > 20) return;
        var everyone = await scenes.DragTargetAsync(CurrentSeat, sceneId, tokenId, Context.ConnectionAborted);
        if (everyone is null) return;
        var preview = new { sceneId, tokenId, x, y };
        if (everyone.Value) await Clients.Others.SendAsync("tokenDrag", preview);
        else await Clients.GroupExcept(Gm, Context.ConnectionId).SendAsync("tokenDrag", preview);
    }

    /// <summary>A pulse on the scene the players see, for everyone, for a moment; nothing is saved.</summary>
    public async Task Ping(int sceneId, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !await scenes.IsActiveAsync(sceneId, Context.ConnectionAborted)) return;
        var seat = CurrentSeat;
        await Clients.All.SendAsync("ping", new { sceneId, x, y, color = seat?.Color ?? GmPingColor, label = seat?.Label ?? "GM" });
    }

    /// <summary>The seat of this connection; null for the GM.</summary>
    private TableSeat? CurrentSeat => Context.Items.TryGetValue(SeatKey, out var value) ? value as TableSeat : null;
}
