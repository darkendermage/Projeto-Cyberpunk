using System.Collections.Concurrent;

namespace CyberpunkDb.Application.Table;

// One write at a time per scene so simultaneous drags don't eat each other's moves. Single process, so an
// in-memory lock is enough.
public sealed class SceneLocks
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(int sceneId, CancellationToken ct)
    {
        var gate = _locks.GetOrAdd(sceneId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new Release(gate);
    }

    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        private int _done;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0) gate.Release();
        }
    }
}
