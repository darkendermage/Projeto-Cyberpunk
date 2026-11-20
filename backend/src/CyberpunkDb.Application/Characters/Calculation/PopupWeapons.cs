using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Characters.Calculation;

// The weapon a Popup Melee / Ranged Weapon holds (CP:R p.365): a one-handed weapon of the right kind from the
// inventory, at most one popup per weapon.
public static class PopupWeapons
{
    private static readonly WeaponClass[] MeleeClasses = [WeaponClass.LightMelee, WeaponClass.MediumMelee, WeaponClass.HeavyMelee, WeaponClass.VeryHeavyMelee];

    public static PopupWeaponRule? Rule(CharacterRuleBook rules, string slug) =>
        (rules.Document.Cyberware.PopupWeapons.Value ?? []).FirstOrDefault(r => r.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    public static bool Fits(PopupWeaponRule rule, WeaponInfo weapon)
    {
        if (weapon.Hands > 1) return false;
        var melee = MeleeClasses.Contains(weapon.Class);
        if (melee != rule.Melee) return false;
        return rule.Classes is not { Length: > 0 } classes || classes.Contains(weapon.Class.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    // inventory weapons this popup can take: the right kind and not already in another popup
    public static IReadOnlyList<string> Candidates(CharacterRuleBook rules, CharacterSheet sheet, NpcCatalog catalog, CharacterCyberware popup)
    {
        if (catalog.Find(popup.ItemId) is not { } item || Rule(rules, item.Slug) is not { } rule) return [];
        var taken = sheet.Cyberware.Where(c => c.InstanceId != popup.InstanceId && c.HostedWeaponId is not null).Select(c => c.HostedWeaponId!).ToHashSet();
        return sheet.Inventory
            .Where(e => !taken.Contains(e.InstanceId) && catalog.Find(e.ItemId)?.Weapon is { } w && Fits(rule, w))
            .Select(e => e.InstanceId).ToList();
    }
}
