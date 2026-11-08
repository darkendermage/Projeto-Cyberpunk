using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Npcs.Generation;

// The result is still the same profile (same Id, role, key skills) with the gang's weights added, so the
// planners don't need any gang logic.
public static class GangOverlay
{
    public static NpcProfile Apply(NpcProfile profile, NpcGang gang)
    {
        var bias = gang.Bias.Value ?? new GangBias();
        var forbidden = bias.ForbiddenCyberware.ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool Allowed(string slug) => !bias.AllCyberwareForbidden && !forbidden.Contains(slug);

        return profile with
        {
            StatWeights = profile.StatWeights.ToDictionary(kv => kv.Key, kv => kv.Value * bias.StatWeights.GetValueOrDefault(kv.Key, 1)),
            SecondarySkills = Sum(profile.SecondarySkills, bias.SecondarySkills),
            Cyberware = Sum(profile.Cyberware, bias.Cyberware).Where(kv => Allowed(kv.Key)).ToDictionary(StringComparer.OrdinalIgnoreCase),
            SignatureCyberware = profile.SignatureCyberware.Where(Allowed).ToArray(),
            Gear = Sum(profile.Gear, bias.Gear),
            ArmorStyle = bias.ArmorStyle ?? profile.ArmorStyle,
            Weapons = new ProfileWeapons
            {
                Primary = Weapons(profile.Weapons.Primary, bias),
                Secondary = Weapons(profile.Weapons.Secondary, bias),
            },
        };
    }

    // as in the catalog (SMGs use Handgun, Shotguns Shoulder Arms)
    public static WeaponSkill SkillOf(WeaponClass weaponClass) => weaponClass switch
    {
        WeaponClass.MediumPistol or WeaponClass.HeavyPistol or WeaponClass.VeryHeavyPistol
            or WeaponClass.Smg or WeaponClass.HeavySmg => WeaponSkill.Handgun,
        WeaponClass.Shotgun or WeaponClass.AssaultRifle or WeaponClass.SniperRifle => WeaponSkill.ShoulderArms,
        WeaponClass.MachineGun or WeaponClass.GrenadeLauncher or WeaponClass.RocketLauncher => WeaponSkill.HeavyWeapons,
        WeaponClass.Bow or WeaponClass.Crossbow => WeaponSkill.Archery,
        _ => WeaponSkill.MeleeWeapon,
    };

    // Favored classes go first (the planner weighs by order); forbidden ones drop unless nothing would be left.
    private static WeaponPreference? Weapons(WeaponPreference? pref, GangBias bias)
    {
        if (pref is null) return null;
        var favored = bias.WeaponClasses.Where(c => pref.Skills.Contains(SkillOf(c)));
        var classes = favored.Concat(pref.Classes).Distinct().ToArray();
        var allowed = classes.Where(c => !bias.ForbiddenWeaponClasses.Contains(c)).ToArray();
        return pref with { Classes = allowed.Length > 0 ? allowed : classes };
    }

    private static Dictionary<string, double> Sum(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        var result = new Dictionary<string, double>(a, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, weight) in b) result[key] = result.GetValueOrDefault(key) + weight;
        return result;
    }
}
