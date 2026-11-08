namespace CyberpunkDb.Application.Table;

public enum TokenImageKind { Character, Npc, Compendium }

// Stamp busts the browser cache
public sealed record TokenImageSource(TokenImageKind Kind, string Key, DateTime Stamp);

public sealed record TokenImages(
    IReadOnlyDictionary<int, DateTime> Characters, IReadOnlyDictionary<int, DateTime> Npcs, IReadOnlyDictionary<string, DateTime> Compendium,
    IReadOnlyDictionary<int, CyberpunkDb.Application.Characters.PortraitFocus>? CharacterFocus = null)
{
    public static readonly TokenImages None = new(new Dictionary<int, DateTime>(), new Dictionary<int, DateTime>(), new Dictionary<string, DateTime>());
}
