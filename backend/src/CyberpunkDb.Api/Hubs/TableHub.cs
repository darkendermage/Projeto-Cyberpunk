using CyberpunkDb.Api.Table;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Domain.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace CyberpunkDb.Api.Hubs;

// Live channel. A connection with a seat link is that player, one without is the GM (only allowed on the GM's
// port). Clients only send throwaway stuff here (drag previews, pings); real changes go through the HTTP API.
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
        if (seat is not null)
        {
            await Clients.Group(Gm).SendAsync("seats");
            // so the party strip can show who's online
            await Clients.Groups(Gm, Players).SendAsync("party", new { characterId = (int?)null });
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        presence.Remove(Context.ConnectionId);
        if (CurrentSeat is not null)
        {
            await Clients.Group(Gm).SendAsync("seats");
            await Clients.Groups(Gm, Players).SendAsync("party", new { characterId = (int?)null });
        }
        await base.OnDisconnectedAsync(exception);
    }

    // the drop itself goes through the API; hidden images only preview to the GM
    public async Task DragImage(int sceneId, string imageId, double x, double y, double width, double height, double rotation)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(rotation) || imageId.Length > 20) return;
        var everyone = await scenes.ImageDragTargetAsync(CurrentSeat, sceneId, imageId, Context.ConnectionAborted);
        if (everyone is null) return;
        var preview = new { sceneId, imageId, x, y, width, height, rotation };
        if (everyone.Value) await Clients.Others.SendAsync("imageDrag", preview);
        else await Clients.GroupExcept(Gm, Context.ConnectionId).SendAsync("imageDrag", preview);
    }

    // Drag preview, the drop goes through the API. Tokens hidden from some players only preview to the GM.
    public async Task DragToken(int sceneId, string tokenId, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || tokenId.Length > 20) return;
        var everyone = await scenes.DragTargetAsync(CurrentSeat, sceneId, tokenId, Context.ConnectionAborted);
        if (everyone is null) return;
        var preview = new { sceneId, tokenId, x, y };
        if (everyone.Value) await Clients.Others.SendAsync("tokenDrag", preview);
        else await Clients.GroupExcept(Gm, Context.ConnectionId).SendAsync("tokenDrag", preview);
    }

    // not saved
    public async Task Ping(int sceneId, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !await scenes.IsActiveAsync(sceneId, Context.ConnectionAborted)) return;
        var seat = CurrentSeat;
        await Clients.All.SendAsync("ping", new { sceneId, x, y, color = seat?.Color ?? GmPingColor, label = seat?.Label ?? "GM" });
    }

    // null = GM
    private TableSeat? CurrentSeat => Context.Items.TryGetValue(SeatKey, out var value) ? value as TableSeat : null;
}
