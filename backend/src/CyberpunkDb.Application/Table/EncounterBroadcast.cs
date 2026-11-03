using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Encounters;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

/// <summary>
/// Sends every fight change to the GM's screens, and to the players when the fight is the one linked to the scene on
/// their screens, each seat with its own view.
/// </summary>
public sealed class EncounterBroadcast(IAppDbContext db, ITableNotifier notifier) : IEncounterObserver
{
    public async Task ChangedAsync(EncounterDetailDto detail, CancellationToken ct) =>
        await notifier.EncounterAsync(detail, await SeatViewsAsync(detail.Id, detail, ct), ct);

    public async Task RemovedAsync(int id, CancellationToken ct) =>
        await notifier.EncounterRemovedAsync(id, await SeatViewsAsync(id, null, ct), ct);

    /// <summary>Null when no player watches this fight; otherwise each open seat's view (null values on removal).</summary>
    private async Task<Dictionary<int, PlayerEncounterDto?>?> SeatViewsAsync(int encounterId, EncounterDetailDto? detail, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active && s.EncounterId == encounterId, ct);
        if (scene is null) return null;
        var state = SceneService.Deserialize(scene.StateJson);
        var seats = await db.TableSeats.AsNoTracking().Where(s => s.RevokedAt == null).ToListAsync(ct);
        return seats.ToDictionary(s => s.Id, s => detail is null ? null : (PlayerEncounterDto?)PlayerEncounters.For(detail, s, state));
    }
}
