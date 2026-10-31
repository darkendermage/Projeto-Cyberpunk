namespace CyberpunkDb.Application.Npcs.Compendium;

/// <summary>List entry: identity, role and the bio, without the stat block.</summary>
public sealed record CompendiumNpcSummary(
    string Id,
    string Name,
    IReadOnlyList<string> Aliases,
    CompendiumKind Kind,
    string? Role,
    int? RoleRank,
    int? Rep,
    string? Affiliation,
    IReadOnlyList<CompendiumSource> Sources,
    string Summary);

public sealed record CompendiumNpcDetail(
    string Id,
    string Name,
    IReadOnlyList<string> Aliases,
    CompendiumKind Kind,
    string? Role,
    int? RoleRank,
    int? Rep,
    string? Affiliation,
    IReadOnlyList<CompendiumSource> Sources,
    string Summary,
    string? SheetNote,
    CompendiumSheetDto? Sheet);

public sealed record CompendiumSpecialDto(string Name, string Text);

public sealed record CompendiumSheetDto(
    int Hp,
    int? SeriouslyWounded,
    int? DeathSave,
    IReadOnlyDictionary<string, int?> Stats,
    IReadOnlyDictionary<string, int> ArmoredStats,
    IReadOnlyList<CompendiumWeapon> Weapons,
    IReadOnlyList<CompendiumArmor> Armor,
    IReadOnlyDictionary<string, int> Skills,
    IReadOnlyDictionary<string, int> ArmoredSkills,
    IReadOnlyList<string> Gear,
    IReadOnlyList<string> Cyberware,
    IReadOnlyList<CompendiumSpecialDto> Specials);
