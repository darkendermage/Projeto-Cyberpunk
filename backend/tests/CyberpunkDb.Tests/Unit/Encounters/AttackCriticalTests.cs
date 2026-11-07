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

/// <summary>Natural 10 and 1 on the attack's d10 (CP:R p.130), Fumble Recovery (p.146) and Poor weapons jamming (p.344).</summary>
public class AttackCriticalTests
{
    private static readonly CombatRuleBook Rules = CombatRuleBook.LoadEmbedded();
    private static readonly AttackResolver Resolver = new(Rules, CharacterTestData.Rules);

    private static WeaponView Rifle(WeaponQuality quality = WeaponQuality.Standard, bool jammed = false) =>
        new("rifle", "Rifle", WeaponClass.AssaultRifle, "shoulder-arms", 14, null, "5d6", 1, 25, 25, null, false, false, Quality: quality, Jammed: jammed);

    private static FighterView Fighter(string id, WeaponView? weapon = null, Dictionary<string, int>? awareness = null) =>
        new(id, CombatantKind.Generic, id, CombatSide.Enemy, null, 40, 40, WoundState.Unhurt, 0, null, 0, 0, 0, 0,
            6, 6, 6, 6, 6, 10, 8, 8, weapon is null ? [] : [weapon], [], [], 0, 6, false, false, false, null, null, null, null,
            CombatAwareness: awareness);

    private static AttackRequest Shot(int roll, int? extra) =>
        new(1, "A", ["B"], "rifle", AttackMode.Single, 10, false, 0, roll, null, [1, 1, 1, 1, 1], AttackExtra: extra);

    [Fact]
    public void Natural_10_adds_extra_d10()
    {
        var plan = Resolver.Resolve(Shot(10, 4), Fighter("A", Rifle()), [Fighter("B")], new FixedDice(1));

        plan.AttackTotal.Should().Be(14 + 10 + 4);
        plan.AttackExtra.Should().Be(4);
    }

    [Fact]
    public void Natural_10_rolls_the_extra_when_not_typed()
    {
        var plan = Resolver.Resolve(Shot(10, null), Fighter("A", Rifle()), [Fighter("B")], new FixedDice(7));

        plan.AttackExtra.Should().Be(7);
        plan.AttackTotal.Should().Be(14 + 10 + 7);
    }

    [Fact]
    public void Natural_1_subtracts_extra_d10()
    {
        var plan = Resolver.Resolve(Shot(1, 3), Fighter("A", Rifle()), [Fighter("B")], new FixedDice(1));

        plan.AttackTotal.Should().Be(14 + 1 - 3);
        plan.AttackExtra.Should().Be(-3);
    }

    [Fact]
    public void Other_rolls_have_no_extra()
    {
        var plan = Resolver.Resolve(Shot(5, 9), Fighter("A", Rifle()), [Fighter("B")], new FixedDice(1));

        plan.AttackTotal.Should().Be(19);
        plan.AttackExtra.Should().BeNull();
    }

    [Fact]
    public void Fumble_recovery_ignores_natural_1()
    {
        var solo = Fighter("A", Rifle(), new Dictionary<string, int> { ["fumbleRecovery"] = 4 });
        var plan = Resolver.Resolve(Shot(1, 3), solo, [Fighter("B")], new FixedDice(1));

        plan.AttackTotal.Should().Be(14 + 1);
        plan.AttackExtra.Should().BeNull();
        plan.Notes.Should().Contain(n => n.Contains("Fumble Recovery"));
    }

    [Fact]
    public void Poor_weapon_jams_on_natural_1_even_with_fumble_recovery()
    {
        Resolver.Resolve(Shot(1, 3), Fighter("A", Rifle(WeaponQuality.Poor)), [Fighter("B")], new FixedDice(1)).Jammed.Should().BeTrue();
        var solo = Fighter("A", Rifle(WeaponQuality.Poor), new Dictionary<string, int> { ["fumbleRecovery"] = 4 });
        Resolver.Resolve(Shot(1, 3), solo, [Fighter("B")], new FixedDice(1)).Jammed.Should().BeTrue();
        Resolver.Resolve(Shot(1, 3), Fighter("A", Rifle()), [Fighter("B")], new FixedDice(1)).Jammed.Should().BeFalse();
        Resolver.Resolve(Shot(2, null), Fighter("A", Rifle(WeaponQuality.Poor)), [Fighter("B")], new FixedDice(1)).Jammed.Should().BeFalse();
    }

    [Fact]
    public void Jammed_weapon_cannot_attack()
    {
        var act = () => Resolver.Resolve(Shot(5, null), Fighter("A", Rifle(jammed: true)), [Fighter("B")], new FixedDice(1));

        act.Should().Throw<RequestValidationException>().Which.Errors["weapon"].Single().ToLowerInvariant().Should().ContainAny("travada", "jammed");
    }
}
