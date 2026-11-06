namespace CyberpunkDb.Domain.Enums;

public enum TableMessageKind { Text, Roll, System, Attack }

/// <summary>
/// Who reads a chat line: everyone, the author and the GM (Private), only the GM (Secret: the author sees that a roll
/// was made, not its result) or the author, the target seat and the GM (Whisper).
/// </summary>
public enum TableVisibility { Public, Private, Secret, Whisper }
