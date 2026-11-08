using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Encounters;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

// GM always; players too when it's the fight linked to the scene on their screens (each seat gets its own view).
public sealed class EncounterBroadcast(IAppDbContext db, ITableNotifier notifier, SceneService scenes, ChatService chat) : IEncounterObserver
{
    public async Task ChangedAsync(EncounterDetailDto detail, CancellationToken ct)
    {
        var seats = await SeatViewsAsync(detail.Id, detail, ct);
        await notifier.EncounterAsync(detail, seats, ct);
        // covers damaged on the combat table show on the map barriers
        await scenes.RepublishCoversOfAsync(detail.Id, ct);
        // Attacks in the live fight go to the table chat wherever they were made; undone ones get crossed out.
        if (seats is null) return;
        if (detail.LastAttack is { } attack)
            await chat.PostAttackAsync(TableAttacks.From(detail, attack), detail.Queue.FirstOrDefault(f => f.Id == attack.AttackerId)?.CharacterId, ct);
        if (detail.UndoneAttack is { } seq) await chat.MarkAttackUndoneAsync(detail.Id, seq, ct);
    }

    public async Task RemovedAsync(int id, CancellationToken ct) =>
        await notifier.EncounterRemovedAsync(id, await SeatViewsAsync(id, null, ct), ct);

    // null when no player watches it; null values on removal
    private async Task<Dictionary<int, PlayerEncounterDto?>?> SeatViewsAsync(int encounterId, EncounterDetailDto? detail, CancellationToken ct)
    {
        var scene = await db.Scenes.AsNoTracking().FirstOrDefaultAsync(s => s.Active && s.EncounterId == encounterId, ct);
        if (scene is null) return null;
        var state = SceneService.Deserialize(scene.StateJson);
        var seats = await db.TableSeats.AsNoTracking().Where(s => s.RevokedAt == null).ToListAsync(ct);
        return seats.ToDictionary(s => s.Id, s => detail is null ? null : (PlayerEncounterDto?)PlayerEncounters.For(detail, s, state));
    }
}
