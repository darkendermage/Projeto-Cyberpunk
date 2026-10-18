using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Npcs.Generation;

/// <summary>
/// Buys the NPC's gear within the template budget: signature gear, armor aimed at the suggested SP range,
/// the profile's primary weapon (in the quality the template asks for), ammunition, a backup weapon and
/// extras. Every purchase is checked against the remaining budget before it is added.
/// </summary>
public sealed class EquipmentPlanner(NpcMechanics mechanics, NpcCatalog catalog, NpcTemplate template, Random rng)
{
    private readonly NpcRuleBook _rules = mechanics.Rules;
    private readonly List<NpcGearEntry> _entries = [];
    private readonly int _budget = template.GearBudget.Value;

    public IReadOnlyList<NpcGearEntry> Entries => _entries;
    public int Spent => _entries.Sum(Cost);
    private int Remaining => _budget - Spent;

    public void AddLocked(IEnumerable<NpcGearEntry> locked) => _entries.AddRange(locked.Select(e => e with { Locked = true }));

    public void Fill(NpcProfile profile, int strengthBody, int cyberwareSp)
    {
        BuySignatureGear(profile);
        if (!_entries.Any(e => e.Worn == ArmorSlot.Body)) BuyArmor(profile, cyberwareSp);

        var primary = _entries.Select(e => catalog.Find(e.ItemId)?.Weapon).FirstOrDefault(w => w is not null);
        CatalogItem? primaryItem = null;
        if (primary is null && profile.Weapons.Primary is { } pref)
            primaryItem = BuyWeapon(pref, strengthBody, reserve: 20);
        if (primaryItem is not null) BuyAmmo(primaryItem, packs: 2);

        if (profile.Weapons.Secondary is { } sec && rng.NextDouble() < _rules.Document.Generator.SecondaryWeaponChance.Value)
        {
            var secondary = BuyWeapon(sec, strengthBody, reserve: 0, maxShare: 0.6);
            if (secondary is not null) BuyAmmo(secondary, packs: 1);
        }

        BuyExtras(profile);
    }

    // ------------------------------------------------------------ purchases

    private void BuySignatureGear(NpcProfile profile)
    {
        if (profile.SignatureGear.Length == 0) return;
        if (_entries.Any(e => profile.SignatureGear.Contains(catalog.Find(e.ItemId)?.Slug, StringComparer.OrdinalIgnoreCase))) return;
        var options = profile.SignatureGear
            .Select(slug => catalog.Find(slug))
            .Where(i => i?.Price is { } p && p <= Remaining * 0.6)
            .Select(i => i!)
            .ToList();
        if (options.Count == 0) return;
        var pick = Weighted.Pick(options, i => profile.Gear.GetValueOrDefault(i.Slug, 1) * (1 + (double)i.Price!.Value / _budget), rng);
        Add(new NpcGearEntry { ItemId = pick.Id });
    }

    private void BuyArmor(NpcProfile profile, int cyberwareSp)
    {
        var range = template.SuggestedSp.Value;
        if (cyberwareSp >= range.Min) return;

        var target = profile.ArmorStyle switch
        {
            ArmorStyle.Heavy => range.Max,
            ArmorStyle.Medium => Weighted.Between(rng, range.Min, range.Max),
            _ => range.Min,
        };
        var cap = (int)(_budget * 0.5);
        var options = catalog.Items
            .Where(i => i.Armor is { Type: ArmorType.BodyArmor, Sp: not null } && i.Price is not null)
            .Where(i => i.Price <= Math.Min(cap, Remaining))
            .Where(i => profile.ArmorStyle != ArmorStyle.Light || i.Armor!.RefPenalty == 0)
            .ToList();
        if (options.Count == 0) return;

        // Closest SP to the target, inside the suggested range when possible; Core Book items first.
        var best = options
            .OrderBy(i => range.Contains(i.Armor!.Sp!.Value) ? 0 : 1)
            .ThenBy(i => Math.Abs(i.Armor!.Sp!.Value - target))
            .ThenBy(i => IsCore(i) ? 0 : 1)
            .ThenBy(i => i.Price)
            .Take(3)
            .ToList();
        var pick = best[rng.Next(best.Count)];
        Add(new NpcGearEntry { ItemId = pick.Id, Worn = ArmorSlot.Body });
        if (pick.Price!.Value * 2 <= cap && pick.Price <= Remaining)
            Add(new NpcGearEntry { ItemId = pick.Id, Worn = ArmorSlot.Head });
    }

    private CatalogItem? BuyWeapon(WeaponPreference pref, int strengthBody, int reserve, double maxShare = 0.85)
    {
        var cap = Math.Min(Remaining - reserve, (int)(_budget * maxShare));
        if (cap <= 0) return null;

        var offers = WeaponOffers(pref, strengthBody).Where(o => o.Price <= cap).ToList();
        if (offers.Count == 0) return null;
        var g = _rules.Document.Generator;
        var pick = Weighted.Pick(offers, o =>
        {
            var classIndex = Array.IndexOf(pref.Classes, o.Item.Weapon!.Class);
            var w = classIndex < 0 ? 0.5 : Math.Max(1, 3 - classIndex);
            if (IsCore(o.Item)) w *= g.CoreBookWeight.Value;
            if (o.Item.Weapon.Exotic) w *= g.ExoticWeaponWeight.Value;
            // Higher tiers favor better (pricier) weapons within the budget.
            var priceWeight = template.Level is NpcLevel.MiniBoss or NpcLevel.Boss ? 3.0 : 1.0;
            return w * (1 + priceWeight * o.Price / Math.Max(1, cap));
        }, rng);
        Add(new NpcGearEntry { ItemId = pick.Item.Id, Quality = pick.Quality == pick.Item.Weapon!.Quality ? null : pick.Quality });
        return pick.Item;
    }

    /// <summary>Weapons the template allows, each offered in the quality its rules ask for, with the right price.</summary>
    private IEnumerable<(CatalogItem Item, WeaponQuality Quality, int Price)> WeaponOffers(WeaponPreference pref, int strengthBody)
    {
        var weapons = catalog.Items.Where(i => i.Weapon is { } w && pref.Skills.Contains(w.Skill) && i.Price is not null
                                               && (w.MinBody ?? 0) <= strengthBody).ToList();
        if (weapons.Any(i => pref.Classes.Contains(i.Weapon!.Class)))
            weapons = weapons.Where(i => pref.Classes.Contains(i.Weapon!.Class)).ToList();

        var rule = template.WeaponQuality.Value;
        var noPoor = template.Hardened && template.Level != NpcLevel.Mook;
        var offers = new List<(CatalogItem, WeaponQuality, int)>();
        foreach (var item in weapons)
        {
            var native = item.Weapon!.Quality;
            var qualities = rule == WeaponQualityRule.PoorOnly
                ? new[] { WeaponQuality.Poor }
                : new[] { WeaponQuality.Standard, WeaponQuality.Excellent, WeaponQuality.Poor };
            foreach (var q in qualities)
            {
                if (q == WeaponQuality.Excellent && (rule == WeaponQualityRule.NoExcellent || template.Level is NpcLevel.Mook)) continue;
                if (q == WeaponQuality.Poor && (noPoor || rule != WeaponQualityRule.PoorOnly)) continue;
                var (price, _, error) = mechanics.GearUnitPrice(item, q == native ? null : q);
                if (error is null && price is { } p) offers.Add((item, q, p));
            }
        }
        // If no Poor version exists for a Mook, fall back to Standard (the validator flags it as a guideline warning).
        if (offers.Count == 0 && rule == WeaponQualityRule.PoorOnly)
            offers.AddRange(weapons.Where(i => i.Weapon!.Quality == WeaponQuality.Standard).Select(i => (i, WeaponQuality.Standard, i.Price!.Value)));
        return offers;
    }

    private void BuyAmmo(CatalogItem weapon, int packs)
    {
        var calibers = weapon.Weapon!.Calibers;
        if (calibers.Count == 0 || weapon.Weapon.Skill == WeaponSkill.MeleeWeapon) return;
        var ammo = catalog.Items
            .Where(i => i.Ammo is { } a && a.Effect == AmmoEffect.Lethal && a.Calibers.Intersect(calibers).Any() && i.Price is not null)
            .OrderBy(i => IsCore(i) ? 0 : 1).ThenBy(i => i.Price)
            .FirstOrDefault();
        if (ammo is null) return;
        var affordable = Math.Min(packs, Remaining / Math.Max(1, ammo.Price!.Value));
        if (affordable > 0) Add(new NpcGearEntry { ItemId = ammo.Id, Quantity = affordable });
    }

    private void BuyExtras(NpcProfile profile)
    {
        var floor = _budget * _rules.Document.Generator.GearSpendFloor.Value;
        var options = profile.Gear
            .Where(kv => !profile.SignatureGear.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .Select(kv => (Item: catalog.Find(kv.Key), Weight: kv.Value))
            .Where(o => o.Item is { Price: not null, Cyber: null })
            .Select(o => (Item: o.Item!, o.Weight))
            .ToList();
        for (var guard = 0; guard < 20 && options.Count > 0 && Spent < floor; guard++)
        {
            var pick = Weighted.Pick(options, o => o.Weight, rng);
            options.Remove(pick);
            if (pick.Item.Price <= Remaining && !_entries.Any(e => e.ItemId == pick.Item.Id))
                Add(new NpcGearEntry { ItemId = pick.Item.Id });
        }
    }

    // ------------------------------------------------------------ helpers

    private void Add(NpcGearEntry entry)
    {
        if (Cost(entry) > Remaining) return; // never exceed the budget, even by rounding
        _entries.Add(entry);
    }

    private int Cost(NpcGearEntry e) =>
        catalog.Find(e.ItemId) is { } item ? (mechanics.GearUnitPrice(item, e.Quality).Price ?? 0) * e.Quantity : 0;

    private static bool IsCore(CatalogItem i) => i.SourceBook is "Core Book";
}
