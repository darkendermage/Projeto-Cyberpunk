using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace CyberpunkDb.Api.Table;

// so a revoked seat can be kicked right away
public sealed class TablePresence : CyberpunkDb.Application.Table.ITablePresence
{
    private readonly ConcurrentDictionary<string, (int? SeatId, HubCallerContext Context)> _connections = new();

    public void Add(HubCallerContext context, int? seatId) => _connections[context.ConnectionId] = (seatId, context);

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public IReadOnlyCollection<int> OnlineSeats() =>
        _connections.Values.Where(c => c.SeatId is not null).Select(c => c.SeatId!.Value).Distinct().ToList();

    public void Disconnect(int seatId)
    {
        foreach (var (_, (seat, context)) in _connections)
            if (seat == seatId) context.Abort();
    }
}
