using CyberpunkDb.Application.Encounters;

namespace CyberpunkDb.Application.Table;

// SignalR in the API. The application decides what each seat sees, this just delivers.
public interface ITableNotifier
{
    // seats: each open seat's view of the active scene (null value = no active scene), or null when players'
    // screens don't change.
    Task SceneAsync(SceneView gm, IReadOnlyDictionary<int, SceneView?>? seats, CancellationToken ct);

    Task SceneRemovedAsync(int id, IReadOnlyDictionary<int, SceneView?>? seats, CancellationToken ct);

    // seats works like in SceneAsync
    Task EncounterAsync(EncounterDetailDto gm, IReadOnlyDictionary<int, PlayerEncounterDto?>? seats, CancellationToken ct);

    Task EncounterRemovedAsync(int id, IReadOnlyDictionary<int, PlayerEncounterDto?>? seats, CancellationToken ct);

    Task MessageAsync(TableMessageDto gm, IReadOnlyDictionary<int, TableMessageDto> seats, CancellationToken ct);

    // lobby claim/release, GM invites refresh
    Task SeatsAsync(CancellationToken ct);

    // characterId null = a player came or went. Refreshes the party strip and the owner's sheet.
    Task PartyAsync(int? characterId, CancellationToken ct);

    // null = stopped
    Task MusicAsync(TableMusic? music, CancellationToken ct);

    Task ChatResetAsync(CancellationToken ct);
}

// for hosts without live screens, and tests
public sealed class NullTableNotifier : ITableNotifier
{
    public Task SceneAsync(SceneView gm, IReadOnlyDictionary<int, SceneView?>? seats, CancellationToken ct) => Task.CompletedTask;
    public Task SceneRemovedAsync(int id, IReadOnlyDictionary<int, SceneView?>? seats, CancellationToken ct) => Task.CompletedTask;
    public Task EncounterAsync(EncounterDetailDto gm, IReadOnlyDictionary<int, PlayerEncounterDto?>? seats, CancellationToken ct) => Task.CompletedTask;
    public Task EncounterRemovedAsync(int id, IReadOnlyDictionary<int, PlayerEncounterDto?>? seats, CancellationToken ct) => Task.CompletedTask;
    public Task MessageAsync(TableMessageDto gm, IReadOnlyDictionary<int, TableMessageDto> seats, CancellationToken ct) => Task.CompletedTask;
    public Task SeatsAsync(CancellationToken ct) => Task.CompletedTask;
    public Task PartyAsync(int? characterId, CancellationToken ct) => Task.CompletedTask;
    public Task MusicAsync(TableMusic? music, CancellationToken ct) => Task.CompletedTask;
    public Task ChatResetAsync(CancellationToken ct) => Task.CompletedTask;
}
