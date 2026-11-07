namespace CyberpunkDb.Application.Table;

public enum TokenImageKind { Character, Npc, Compendium }

/// <summary>Whose stored image a token shows, and when that image last changed (it busts the browsers' caches).</summary>
public sealed record TokenImageSource(TokenImageKind Kind, string Key, DateTime Stamp);

/// <summary>When each stored image a scene's tokens may show last changed: characters, saved NPCs and compendium NPCs.</summary>
public sealed record TokenImages(
    IReadOnlyDictionary<int, DateTime> Characters, IReadOnlyDictionary<int, DateTime> Npcs, IReadOnlyDictionary<string, DateTime> Compendium,
    IReadOnlyDictionary<int, CyberpunkDb.Application.Characters.PortraitFocus>? CharacterFocus = null)
{
    public static readonly TokenImages None = new(new Dictionary<int, DateTime>(), new Dictionary<int, DateTime>(), new Dictionary<string, DateTime>());
}
