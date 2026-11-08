namespace CyberpunkDb.Application.Encounters;

// so open screens (the table) can follow the fight live
public interface IEncounterObserver
{
    Task ChangedAsync(EncounterDetailDto detail, CancellationToken ct);
    Task RemovedAsync(int id, CancellationToken ct);
}
