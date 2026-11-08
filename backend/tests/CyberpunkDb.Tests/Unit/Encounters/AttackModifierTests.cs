using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Tests.Unit.NightMarkets;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Encounters;

// What adds to an attack besides the skill: Excellent quality, a Smartgun, a Targeting Scope on aimed shots
public class AttackModifierTests
{
    private static readonly AttackResolver Resolver = new(CombatRuleBook.LoadEmbedded(), Rules);

    private static CharacterSheet Armed(WeaponQuality? quality = null, bool smartLink = false, params int[] cyber)
    {
        var inventory = new List<InventoryEntry>
        {
            new() { InstanceId = "gun", ItemId = HeavyPistol.Id, State = ItemState.Equipped, Quality = quality,
                    Weapon = new WeaponState { Attachments = smartLink ? ["link"] : [] } },
        };
        if (smartLink) inventory.Add(new InventoryEntry { InstanceId = "link", ItemId = SmartgunLink.Id });
        return CompletePackage() with
        {
            Inventory = inventory,
            Cyberware = cyber.Select((id, i) => new CharacterCyberware { InstanceId = $"cy{i}", ItemId = id }).ToList(),
        };
    }

    private static WeaponLine Gun(CharacterSheet sheet) =>
        Calculator.Calculate(sheet.Normalize(), Catalog).Weapons.Single(w => w.InstanceId == "gun");

    [Fact]
    public void Excellent_weapon_adds_1()
    {
        var plain = Gun(Armed());
        var excellent = Gun(Armed(WeaponQuality.Excellent));

        plain.AttackBonuses.Should().BeEmpty();
        excellent.AttackBonuses.Should().ContainSingle(p => p.Value == 1 && p.Label.Contains("Excellent"));
    }

    [Fact]
    public void Smartgun_needs_link_and_plugs()
    {
        Gun(Armed(smartLink: true)).AttackBonuses.Should().BeEmpty("a Smartgun Link needs a Neural Link and Interface Plugs or a Subdermal Grip");
        Gun(Armed(smartLink: true, cyber: [NeuralLink.Id])).AttackBonuses.Should().BeEmpty();
        Gun(Armed(smartLink: true, cyber: [NeuralLink.Id, SubdermalGrip.Id])).AttackBonuses.Should().ContainSingle(p => p.Value == 1 && p.Label.Contains("Smartgun"));
        Gun(Armed(smartLink: false, cyber: [NeuralLink.Id, SubdermalGrip.Id])).AttackBonuses.Should().BeEmpty();
    }

    private static FighterView Shooter(params string[] cyberware) =>
        new("A", CombatantKind.Pc, "A", CombatSide.Ally, null, 40, 40, WoundState.Unhurt, 0, null, 0, 0, 0, 0, 6, 6, 6, 6, 6, 10, 8, 8,
            [new WeaponView("gun", "Pistol", WeaponClass.HeavyPistol, "handgun", 14, null, "3d6", 2, 8, 8, null, false, false)], [], [],
            0, 6, false, false, false, null, null, null, null, Cyberware: cyberware);

    private static FighterView Target() =>
        new("B", CombatantKind.Generic, "B", CombatSide.Enemy, null, 40, 40, WoundState.Unhurt, 0, null, 0, 0, 0, 0, 6, 6, 6, 6, 6, 10, 8, 8,
            [], [], [], 0, 6, false, false, false, null, null, null, null);

    [Fact]
    public void Targeting_scope_adds_1_to_aimed_only()
    {
        AttackRequest Shot(AttackMode mode) => new(1, "A", ["B"], "gun", mode, 5, false, 0, 5, null, [1, 1, 1]);
        var plainAimed = Resolver.Resolve(Shot(AttackMode.AimedHead), Shooter(), [Target()], new FixedDice(1)).AttackTotal;
        var scopedAimed = Resolver.Resolve(Shot(AttackMode.AimedHead), Shooter("targeting-scope"), [Target()], new FixedDice(1)).AttackTotal;
        var scopedSingle = Resolver.Resolve(Shot(AttackMode.Single), Shooter("targeting-scope"), [Target()], new FixedDice(1)).AttackTotal;

        scopedAimed.Should().Be(plainAimed + 1);
        scopedSingle.Should().Be(14 + 5);
    }
}
