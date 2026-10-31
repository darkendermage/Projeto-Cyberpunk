using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Npcs.Compendium;

public enum CompendiumKind
{
    /// <summary>The book prints a stat block.</summary>
    Sheet,
    /// <summary>The book only describes the character.</summary>
    Profile,
}

/// <summary>Book and printed page (not the PDF page) an entry was taken from.</summary>
public sealed record CompendiumSource(string Book, int Page);

/// <summary>A text we write in both languages; <see cref="Current"/> follows the request language.</summary>
public sealed record LocalizedText(string Pt, string En)
{
    public string Current => L.T(Pt, En);
}

/// <summary>One named NPC of the books, with the stat block when the book prints one.</summary>
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
    /// <summary>Set when more than one book prints a sheet and they differ.</summary>
    public LocalizedText? SheetNote { get; init; }
    public CompendiumSheet? Sheet { get; init; }
}

/// <summary>The printed stat block. Values are copied as printed; a "—" in the book is null.</summary>
public sealed record CompendiumSheet
{
    public int Hp { get; init; }
    public int? SeriouslyWounded { get; init; }
    public int? DeathSave { get; init; }
    public Dictionary<string, int?> Stats { get; init; } = [];
    /// <summary>Stats with the armor penalty, from values printed like REF 8(6). Empty when the book has none.</summary>
    public Dictionary<string, int> ArmoredStats { get; init; } = [];
    public List<CompendiumWeapon> Weapons { get; init; } = [];
    public List<CompendiumArmor> Armor { get; init; } = [];
    /// <summary>Printed Skill Bases (STAT + skill + bonus), not recalculated.</summary>
    public Dictionary<string, int> Skills { get; init; } = [];
    public List<string> Gear { get; init; } = [];
    public List<string> Cyberware { get; init; } = [];
    public List<CompendiumSpecial> Specials { get; init; } = [];
}

public sealed record CompendiumWeapon
{
    public string Name { get; init; } = "";
    public string Skill { get; init; } = "";
    /// <summary>Printed attack total (STAT + skill + bonus).</summary>
    public int AttackBase { get; init; }
    public string Damage { get; init; } = "";
    public int Rof { get; init; }
    public int? Magazine { get; init; }
    /// <summary>Null for weapons outside the catalog classes (a borg's fists).</summary>
    public WeaponClass? Class { get; init; }
    public bool Melee { get; init; }
    public string? Notes { get; init; }
}

public sealed record CompendiumArmor(string Location, string Name, int Sp);

public sealed record CompendiumSpecial(string Name, LocalizedText Text);
