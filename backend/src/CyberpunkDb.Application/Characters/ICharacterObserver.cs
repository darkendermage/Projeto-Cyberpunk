namespace CyberpunkDb.Application.Characters;

/// <summary>Told after a character sheet is saved, so live screens (the virtual table) can follow it.</summary>
public interface ICharacterObserver
{
    Task ChangedAsync(int characterId, CancellationToken ct);
}

/// <summary>For hosts and tests without live screens.</summary>
public sealed class NullCharacterObserver : ICharacterObserver
{
    public Task ChangedAsync(int characterId, CancellationToken ct) => Task.CompletedTask;
}
