namespace CyberpunkDb.Domain.Enums;

/// <summary>NET Architecture Difficulty Rating (CP:R p.210): the DV of every Password, File and Control Node.</summary>
public enum NetDifficulty { Basic, Standard, Uncommon, Advanced }

/// <summary>What waits behind a floor's "door" (CP:R p.209).</summary>
public enum NetContentKind { BlackIce, Password, File, ControlNode }
