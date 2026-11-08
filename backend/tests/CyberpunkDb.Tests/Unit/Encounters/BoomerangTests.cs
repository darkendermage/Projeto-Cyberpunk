using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Tests.Unit.Characters;
using CyberpunkDb.Tests.Unit.NightMarkets;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit.Encounters;

// The Ranger Combat Boomerang (Black Chrome p.100): thrown with Athletics, halves SP, comes back with a Targeting Scope.
public class BoomerangTests
{
    private static readonly CombatRuleBook Rules = CombatRuleBook.LoadEmbedded();
    private static readonly AttackResolver Resolver = new(Rules, CharacterTestData.Rules);

    private static WeaponView Boomerang(bool thrown = false) =>
        new("boom", "Ranger Combat Boomerang", WeaponClass.HeavyMelee, "athletics", 12, null, "3d6", 2, null, null, null, false, true,
            ThrownOnly: true, Thrown: thrown);

    private static FighterView Fighter(string id, params WeaponView[] weapons) =>
        new(id, CombatantKind.Pc, id, CombatSide.Ally, null, 40, 40, WoundState.Unhurt, 0, null, 11, 11, 11, 11, 6, 6, 6, 6, 6, 10, 12, 8,
            weapons, [], [], 0, 6, false, false, false, null, null, null, null);

    [Fact]
    public void The_rules_list_the_boomerang()
    {
        var rule = Rules.Document.SpecialWeapons.Value["ranger-combat-boomerang"];
        rule.ThrownOnly.Should().BeTrue();
        rule.HalvesArmor.Should().BeTrue();
        rule.ReturnsWith.Should().Be("targeting-scope");
    }

    [Fact]
    public void Boomerang_uses_the_thrown_dv_and_halves_armor()
    {
        var shot = new AttackRequest(1, "A", ["B"], "boom", AttackMode.Single, 10, false, 0, 5, null, [1, 1, 1]);
        var thrownObject = shot with { Weapon = AttackResolver.Thrown, ThrownDamage = "3d6" };

        var boomerang = Resolver.Resolve(shot, Fighter("A", Boomerang()), [Fighter("B")], new FixedDice(1)).Hits.Single();
        var rock = Resolver.Resolve(thrownObject, Fighter("A", Boomerang()), [Fighter("B")], new FixedDice(1)).Hits.Single();

        boomerang.Dv.Should().Be(rock.Dv, "both use the thrown row of the range table (CP:R p.177)");
        boomerang.HalveArmor.Should().BeTrue();
        rock.HalveArmor.Should().BeFalse();
    }

    [Fact]
    public void Boomerang_reaches_only_as_far_as_a_throw()
    {
        var far = new AttackRequest(1, "A", ["B"], "boom", AttackMode.Single, 40, false, 0, 5, null, [1, 1, 1]);
        var act = () => Resolver.Resolve(far, Fighter("A", Boomerang()), [Fighter("B")], new FixedDice(1));

        act.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Thrown_boomerang_cannot_attack_again()
    {
        var shot = new AttackRequest(1, "A", ["B"], "boom", AttackMode.Single, 10, false, 0, 5, null, [1, 1, 1]);
        var act = () => Resolver.Resolve(shot, Fighter("A", Boomerang(thrown: true)), [Fighter("B")], new FixedDice(1));

        act.Should().Throw<RequestValidationException>();
    }
}
