using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace CyberpunkDb.Application.Table;

// A sheet saved anywhere reaches the table: the live fight is resent if the PC is in it, and the party strip
// refreshes. Inside Quiet() nothing is sent, the fight sends its own result once saved.
public sealed class CharacterBroadcast(IServiceProvider services, ITableNotifier notifier) : ICharacterObserver
{
    private static readonly AsyncLocal<int> QuietDepth = new();

    // until disposed
    public static IDisposable Quiet()
    {
        QuietDepth.Value++;
        return new Scope();
    }

    public async Task ChangedAsync(int characterId, CancellationToken ct)
    {
        if (QuietDepth.Value > 0) return;
        // resolved here because EncounterService -> CharacterService -> this would be a cycle
        var scenes = services.GetRequiredService<SceneService>();
        var (encounterId, _) = await scenes.ActiveAsync(ct);
        if (encounterId is { } id)
        {
            try
            {
                var detail = await services.GetRequiredService<EncounterService>().GetAsync(id, ct);
                if (detail.Status == EncounterStatus.Active && detail.Queue.Any(f => f.CharacterId == characterId))
                    await services.GetRequiredService<IEncounterObserver>().ChangedAsync(detail, ct);
            }
            catch (NotFoundException) { /* the fight is gone: only the party refreshes */ }
        }
        await notifier.PartyAsync(characterId, ct);
    }

    private sealed class Scope : IDisposable
    {
        private bool _done;
        public void Dispose()
        {
            if (_done) return;
            _done = true;
            QuietDepth.Value--;
        }
    }
}
