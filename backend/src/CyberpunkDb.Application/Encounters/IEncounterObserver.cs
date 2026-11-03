namespace CyberpunkDb.Application.Encounters;

/// <summary>Told about every saved change to a fight, so open screens (the virtual table) can follow it live.</summary>
public interface IEncounterObserver
{
    Task ChangedAsync(EncounterDetailDto detail, CancellationToken ct);
    Task RemovedAsync(int id, CancellationToken ct);
}
