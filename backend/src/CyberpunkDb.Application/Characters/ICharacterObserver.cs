namespace CyberpunkDb.Application.Characters;

// so live screens (the table) can follow sheet changes
public interface ICharacterObserver
{
    Task ChangedAsync(int characterId, CancellationToken ct);
}

public sealed class NullCharacterObserver : ICharacterObserver
{
    public Task ChangedAsync(int characterId, CancellationToken ct) => Task.CompletedTask;
}
