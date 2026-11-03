using CyberpunkDb.Api.Hubs;
using CyberpunkDb.Application.Table;
using Microsoft.AspNetCore.SignalR;

namespace CyberpunkDb.Api.Table;

/// <summary>Delivers table changes over the hub: the GM's group gets the full views, each seat its own.</summary>
public sealed class HubTableNotifier(IHubContext<TableHub> hub) : ITableNotifier
{
    public async Task SceneAsync(SceneView gm, IReadOnlyDictionary<int, SceneView?>? seats, CancellationToken ct)
    {
        await hub.Clients.Group(TableHub.Gm).SendAsync("scene", gm, ct);
        await SendSeatsAsync(seats, ct);
    }

    public async Task SceneRemovedAsync(int id, IReadOnlyDictionary<int, SceneView?>? seats, CancellationToken ct)
    {
        await hub.Clients.Group(TableHub.Gm).SendAsync("sceneRemoved", id, ct);
        await SendSeatsAsync(seats, ct);
    }

    private async Task SendSeatsAsync(IReadOnlyDictionary<int, SceneView?>? seats, CancellationToken ct)
    {
        if (seats is null) return;
        foreach (var (seat, view) in seats) await hub.Clients.Group(TableHub.Seat(seat)).SendAsync("scene", view, ct);
    }
}
