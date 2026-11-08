using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Table;

// Uploads per seat per hour. Player links reach the GM's disk through the tunnel and the per-scene cap only
// counts placed images. In memory, a restart resets it.
public sealed class PlayerUploadLimit(TimeProvider clock)
{
    public const int PerHour = 30;

    private readonly Dictionary<int, Queue<DateTimeOffset>> _uploads = [];

    // 429 when the hour is used up
    public void Take(int seatId)
    {
        var now = clock.GetUtcNow();
        lock (_uploads)
        {
            if (!_uploads.TryGetValue(seatId, out var times)) _uploads[seatId] = times = new();
            while (times.Count > 0 && now - times.Peek() >= TimeSpan.FromHours(1)) times.Dequeue();
            if (times.Count >= PerHour)
                throw new TooManyRequestsException(L.T($"Limite de {PerHour} envios de imagem por hora; espere um pouco.", $"Limit of {PerHour} image uploads per hour; wait a little."));
            times.Enqueue(now);
        }
    }
}
