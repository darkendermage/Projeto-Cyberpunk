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

public static class PlayerCharacters
{
    public static PlayerCharacterDto From(CharacterDetailDto c)
    {
        var r = c.Report;
        return new PlayerCharacterDto(
            c.Id, c.Sheet.Identity.Name, c.Sheet.Identity.Handle, r.Health.CurrentHp, r.Health.MaxHp, r.Luck, r.LuckAvailable, r.Humanity.Current,
            r.Stats.Select(s => new PlayerStatDto(s.Key, s.Effective)).ToList(),
            r.Skills.Select(s => new PlayerSkillDto(s.Key, s.SkillId, s.Name, s.NamePt, s.Specialization, s.Category, s.Total)).ToList(),
            r.Weapons.Where(w => w.Equipped).Select(w => new PlayerWeaponDto(w.InstanceId, w.Name, w.SkillTotal, w.Damage, w.Magazine, w.LoadedRounds, w.AutofireTotal)).ToList(),
            new PlayerArmorDto(r.Armor.Head.Sp, r.Armor.Head.MaxSp, r.Armor.Body.Sp, r.Armor.Body.MaxSp));
    }
}
