using CyberpunkDb.Application.Characters;

namespace CyberpunkDb.Application.Table;

public sealed record PlayerStatDto(string Key, int Value);

/// <summary>A skill ready to roll: <see cref="Total"/> is STAT + level + modifiers + penalties (add 1d10).</summary>
public sealed record PlayerSkillDto(string Key, string SkillId, string Name, string? NamePt, string? Specialization, string Category, int Total);

/// <summary>An equipped weapon, ready to roll: <see cref="AttackTotal"/> is the skill total (add 1d10).</summary>
public sealed record PlayerWeaponDto(string Key, string Name, int AttackTotal, string? Damage, int? Magazine, int Rounds, int? AutofireTotal);

/// <summary>SP worn on the head and the body, and their full values (null: nothing to repair there).</summary>
public sealed record PlayerArmorDto(int SpHead, int? MaxSpHead, int SpBody, int? MaxSpBody);

/// <summary>What the token's quick panel needs from the seat's own sheet, in or out of a fight.</summary>
public sealed record PlayerCharacterDto(
    int Id, string Name, string? Handle, int Hp, int MaxHp, int Luck, int LuckAvailable, int Humanity,
    IReadOnlyList<PlayerStatDto> Stats, IReadOnlyList<PlayerSkillDto> Skills, IReadOnlyList<PlayerWeaponDto> Weapons, PlayerArmorDto Armor);

/// <summary>The seat's own sheet to read at the table: who they are, their numbers, gear, chrome and trained skills.</summary>
public sealed record PlayerSheetDto(
    string Name, string? Handle, IReadOnlyList<string> Roles, int Hp, int MaxHp, int Humanity, int MaxHumanity,
    IReadOnlyList<PlayerStatDto> Stats, PlayerArmorDto Armor, string? HeadArmor, string? BodyArmor,
    IReadOnlyList<PlayerSheetWeaponDto> Weapons, IReadOnlyList<string> Cyberware, IReadOnlyList<PlayerGearDto> Gear,
    IReadOnlyList<PlayerSheetSkillDto> Skills);

public sealed record PlayerSheetWeaponDto(string Name, string? Damage, int? Rof, int? Magazine, int Rounds, bool Equipped);
public sealed record PlayerGearDto(string Name, int Quantity);
public sealed record PlayerSheetSkillDto(string Name, string? NamePt, string? Specialization, string Category, int Level, int Total);

public static class PlayerCharacters
{
    public static PlayerSheetDto Sheet(CharacterDetailDto c)
    {
        var r = c.Report;
        return new PlayerSheetDto(
            c.Sheet.Identity.Name, c.Sheet.Identity.Handle,
            r.Roles.Select(x => $"{x.Role} {x.Rank} · {x.Ability}").ToList(),
            r.Health.CurrentHp, r.Health.MaxHp, r.Humanity.Current, r.Humanity.Max,
            r.Stats.Select(s => new PlayerStatDto(s.Key, s.Effective)).ToList(),
            new PlayerArmorDto(r.Armor.Head.Sp, r.Armor.Head.MaxSp, r.Armor.Body.Sp, r.Armor.Body.MaxSp), r.Armor.Head.Name, r.Armor.Body.Name,
            r.Weapons.Select(w => new PlayerSheetWeaponDto(w.Name, w.Damage, w.Rof, w.Magazine, w.LoadedRounds, w.Equipped)).ToList(),
            r.Cyberware.Select(x => x.Name).ToList(),
            r.Inventory.Where(i => i.Category != Domain.Enums.ItemCategory.Weapon).Select(i => new PlayerGearDto(i.Name, i.Quantity)).ToList(),
            r.Skills.Where(s => s.Level > 0).Select(PlayerSkillSheet).ToList());
    }

    private static PlayerSheetSkillDto PlayerSkillSheet(CyberpunkDb.Application.Characters.Calculation.CharacterSkillLine s) =>
        new(s.Name, s.NamePt, s.Specialization, s.Category, s.Level, s.Total);

    public static PlayerCharacterDto From(CharacterDetailDto c)
    {
        var r = c.Report;
        return new PlayerCharacterDto(
            c.Id, c.Sheet.Identity.Name, c.Sheet.Identity.Handle, r.Health.CurrentHp, r.Health.MaxHp, r.Luck, r.LuckAvailable, r.Humanity.Current,
            r.Stats.Select(s => new PlayerStatDto(s.Key, s.Effective)).ToList(),
            r.Skills.Select(s => new PlayerSkillDto(s.Key, s.SkillId, s.Name, s.NamePt, s.Specialization, s.Category, s.Total)).ToList(),
            r.Weapons.Where(w => w.Equipped).Select(w => new PlayerWeaponDto(w.InstanceId, w.Name, w.AttackTotal, w.Damage, w.Magazine, w.LoadedRounds,
                w.AutofireTotal + (w.AttackBonuses?.Sum(p => p.Value) ?? 0))).ToList(),
            new PlayerArmorDto(r.Armor.Head.Sp, r.Armor.Head.MaxSp, r.Armor.Body.Sp, r.Armor.Body.MaxSp));
    }
}
