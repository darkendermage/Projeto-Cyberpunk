using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace CyberpunkDb.Application.Table;

/// <summary>
/// A sheet saved anywhere (the characters page, the combat table, the player's popup) reaches the table: the fight on
/// the live scene is sent again when the PC is in it, and every screen refreshes the party strip. Inside a change of
/// the fight itself (<see cref="Quiet"/>) nothing is sent, because the fight sends its own result once it is saved.
/// </summary>
public sealed class CharacterBroadcast(IServiceProvider services, ITableNotifier notifier) : ICharacterObserver
{
    private static readonly AsyncLocal<int> QuietDepth = new();

    /// <summary>Silences sheet broadcasts in this async flow until disposed (the fight broadcasts its own change).</summary>
    public static IDisposable Quiet()
    {
        QuietDepth.Value++;
        return new Scope();
    }

    public async Task ChangedAsync(int characterId, CancellationToken ct)
    {
        if (QuietDepth.Value > 0) return;
        // Resolved here, not in the constructor: EncounterService needs CharacterService, which needs this observer.
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
