using CyberpunkDb.Api.Hubs;
using CyberpunkDb.Application.Encounters;
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

    public async Task EncounterAsync(EncounterDetailDto gm, IReadOnlyDictionary<int, PlayerEncounterDto?>? seats, CancellationToken ct)
    {
        await hub.Clients.Group(TableHub.Gm).SendAsync("encounter", gm, ct);
        await SendSeatsAsync(seats, "encounter", ct);
    }

    public async Task EncounterRemovedAsync(int id, IReadOnlyDictionary<int, PlayerEncounterDto?>? seats, CancellationToken ct)
    {
        await hub.Clients.Group(TableHub.Gm).SendAsync("encounterRemoved", id, ct);
        await SendSeatsAsync(seats, "encounter", ct);
    }

    public async Task MessageAsync(TableMessageDto gm, IReadOnlyDictionary<int, TableMessageDto> seats, CancellationToken ct)
    {
        await hub.Clients.Group(TableHub.Gm).SendAsync("message", gm, ct);
        await SendSeatsAsync(seats!, "message", ct);
    }

    public Task SeatsAsync(CancellationToken ct) => hub.Clients.Group(TableHub.Gm).SendAsync("seats", ct);

    private Task SendSeatsAsync(IReadOnlyDictionary<int, SceneView?>? seats, CancellationToken ct) => SendSeatsAsync(seats, "scene", ct);

    private async Task SendSeatsAsync<T>(IReadOnlyDictionary<int, T?>? seats, string method, CancellationToken ct) where T : class
    {
        if (seats is null) return;
        foreach (var (seat, view) in seats) await hub.Clients.Group(TableHub.Seat(seat)).SendAsync(method, view, ct);
    }
}
