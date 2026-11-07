using CyberpunkDb.Application.Encounters;

namespace CyberpunkDb.Application.Table;

/// <summary>
/// Pushes table changes to the connected screens (SignalR in the API). The application decides what each seat may
/// see; the notifier only delivers.
/// </summary>
public interface ITableNotifier
{
    /// <summary>
    /// A scene changed. The GM gets the full view; <paramref name="seats"/> holds each open seat's view of the active
    /// scene (null value: no active scene), or is null when the players' screens don't change.
    /// </summary>
    Task SceneAsync(SceneView gm, IReadOnlyDictionary<int, SceneView?>? seats, CancellationToken ct);

    Task SceneRemovedAsync(int id, IReadOnlyDictionary<int, SceneView?>? seats, CancellationToken ct);

    /// <summary>A fight changed: the GM gets the full detail; <paramref name="seats"/> as in <see cref="SceneAsync"/>.</summary>
    Task EncounterAsync(EncounterDetailDto gm, IReadOnlyDictionary<int, PlayerEncounterDto?>? seats, CancellationToken ct);

    Task EncounterRemovedAsync(int id, IReadOnlyDictionary<int, PlayerEncounterDto?>? seats, CancellationToken ct);

    /// <summary>A chat line or roll: the GM's view, and the view of each seat allowed to read it.</summary>
    Task MessageAsync(TableMessageDto gm, IReadOnlyDictionary<int, TableMessageDto> seats, CancellationToken ct);

    /// <summary>Seats changed hands (picked at the entrance, entered, released): the GM's invites refresh.</summary>
    Task SeatsAsync(CancellationToken ct);

    /// <summary>
    /// A character's sheet changed, or a player came or went (<paramref name="characterId"/> null): every screen
    /// refreshes the party strip, and the owner their own sheet.
    /// </summary>
    Task PartyAsync(int? characterId, CancellationToken ct);
}

/// <summary>For hosts without live screens (and tests that don't care).</summary>
public sealed class NullTableNotifier : ITableNotifier
{
    public Task SceneAsync(SceneView gm, IReadOnlyDictionary<int, SceneView?>? seats, CancellationToken ct) => Task.CompletedTask;
    public Task SceneRemovedAsync(int id, IReadOnlyDictionary<int, SceneView?>? seats, CancellationToken ct) => Task.CompletedTask;
    public Task EncounterAsync(EncounterDetailDto gm, IReadOnlyDictionary<int, PlayerEncounterDto?>? seats, CancellationToken ct) => Task.CompletedTask;
    public Task EncounterRemovedAsync(int id, IReadOnlyDictionary<int, PlayerEncounterDto?>? seats, CancellationToken ct) => Task.CompletedTask;
    public Task MessageAsync(TableMessageDto gm, IReadOnlyDictionary<int, TableMessageDto> seats, CancellationToken ct) => Task.CompletedTask;
    public Task SeatsAsync(CancellationToken ct) => Task.CompletedTask;
    public Task PartyAsync(int? characterId, CancellationToken ct) => Task.CompletedTask;
}
