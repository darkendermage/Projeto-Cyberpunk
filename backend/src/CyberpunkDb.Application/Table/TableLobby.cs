namespace CyberpunkDb.Application.Table;

// In memory on purpose: the tunnel address (and every key a phone remembered) changes on restart, so a fresh
// start frees every seat and keeps the lobby closed until the GM opens it.
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

    // false when it's taken
    public bool TryClaim(int seatId)
    {
        lock (_gate) return _claimed.Add(seatId);
    }

    // e.g. through its own link; true if it was free
    public bool MarkClaimed(int seatId)
    {
        lock (_gate) return _claimed.Add(seatId);
    }

    public void Release(int seatId)
    {
        lock (_gate) _claimed.Remove(seatId);
    }
}
