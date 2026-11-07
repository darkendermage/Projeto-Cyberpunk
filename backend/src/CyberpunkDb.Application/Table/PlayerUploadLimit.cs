using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Table;

/// <summary>
/// How many images one seat may upload per hour: the player link reaches the GM's disk through the tunnel, and the
/// per-scene cap counts placed images, not uploads. Kept in memory (a restart resets it).
/// </summary>
public sealed class PlayerUploadLimit(TimeProvider clock)
{
    public const int PerHour = 30;

    private readonly Dictionary<int, Queue<DateTimeOffset>> _uploads = [];

    /// <summary>Counts one upload for the seat, or throws 429 when the seat used up its hour.</summary>
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
