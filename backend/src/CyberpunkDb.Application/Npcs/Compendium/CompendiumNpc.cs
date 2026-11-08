using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Npcs.Compendium;

public enum CompendiumKind
{
    Sheet,
    // no stat block, just a description
    Profile,
}

// printed page, not the PDF page
public sealed record CompendiumSource(string Book, int Page);

// Current follows the request language
public sealed record LocalizedText(string Pt, string En)
{
    public string Current => L.T(Pt, En);
}

public sealed record CompendiumNpc
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public List<string> Aliases { get; init; } = [];
    public CompendiumKind Kind { get; init; }
    public string? Role { get; init; }
    public int? RoleRank { get; init; }
    public int? Rep { get; init; }
    public string? Affiliation { get; init; }
    public List<CompendiumSource> Sources { get; init; } = [];
    public LocalizedText Summary { get; init; } = new("", "");
    // books disagree on the sheet
    public LocalizedText? SheetNote { get; init; }
    public CompendiumSheet? Sheet { get; init; }
}

// copied as printed, "—" is null
public sealed record CompendiumSheet
{
    public int Hp { get; init; }
    public int? SeriouslyWounded { get; init; }
    public int? DeathSave { get; init; }
    public Dictionary<string, int?> Stats { get; init; } = [];
    // from values like REF 8(6)
    public Dictionary<string, int> ArmoredStats { get; init; } = [];
    public List<CompendiumWeapon> Weapons { get; init; } = [];
    public List<CompendiumArmor> Armor { get; init; } = [];
    // as printed, not recalculated
    public Dictionary<string, int> Skills { get; init; } = [];
    // from values like Athletics 13 (11)
    public Dictionary<string, int> ArmoredSkills { get; init; } = [];
    public List<string> Gear { get; init; } = [];
    public List<string> Cyberware { get; init; } = [];
    public List<CompendiumSpecial> Specials { get; init; } = [];
}

public sealed record CompendiumWeapon
{
    public string Name { get; init; } = "";
    public string Skill { get; init; } = "";
    // as printed
    public int AttackBase { get; init; }
    public string Damage { get; init; } = "";
    public int Rof { get; init; }
    public int? Magazine { get; init; }
    // printed "Autofire (xN)" or the Core class default (3 for SMGs, 4 for ARs)
    public int? AutofireMultiplier { get; init; }
    public bool? AutofireOnly { get; init; }
    // 8 bands (0-6 to 401-800 m/yds), null where the book prints "—"
    public int?[]? RangeDv { get; init; }
    // null outside the catalog classes (a borg's fists)
    public WeaponClass? Class { get; init; }
    public bool Melee { get; init; }
    public string? Notes { get; init; }
}

// SpVsMelee: the "SP vs Melee/Charged Tech Weapons" column
public sealed record CompendiumArmor(string Location, string Name, int Sp, int? SpVsMelee = null);

public sealed record CompendiumSpecial(string Name, LocalizedText Text);
