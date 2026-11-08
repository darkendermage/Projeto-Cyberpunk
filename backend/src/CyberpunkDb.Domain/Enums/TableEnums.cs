namespace CyberpunkDb.Domain.Enums;

public enum TableMessageKind { Text, Roll, System, Attack }

// Private = author + GM, Secret = GM only (author sees that they rolled), Whisper = author + target + GM
public enum TableVisibility { Public, Private, Secret, Whisper }
