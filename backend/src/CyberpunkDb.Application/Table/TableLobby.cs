namespace CyberpunkDb.Application.Table;

/// <summary>
/// The entrance of the general link: whether it is open, and which seats someone already holds. It lives in memory on
/// purpose: the tunnel address (and with it every key a phone remembered) changes when the app restarts, so a fresh
/// start frees every seat and keeps the entrance closed until the GM opens it.
/// </summary>
public sealed class TableLobby
{
    private readonly Lock _gate = new();
    private readonly HashSet<int> _claimed = [];
    private volatile bool _open;

    public bool Open => _open;

    public void SetOpen(bool open) => _open = open;

    public bool IsClaimed(int seatId)
    {
        lock (_gate) return _claimed.Contains(seatId);
    }

    /// <summary>Takes a free seat; false when someone already holds it.</summary>
    public bool TryClaim(int seatId)
    {
        lock (_gate) return _claimed.Add(seatId);
    }

    /// <summary>Records that someone entered the seat (e.g. by its individual link). True when it was free before.</summary>
    public bool MarkClaimed(int seatId)
    {
        lock (_gate) return _claimed.Add(seatId);
    }

    public void Release(int seatId)
    {
        lock (_gate) _claimed.Remove(seatId);
    }
}
