using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Tests.Unit.NightMarkets;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

// Implants that change the dice at the table by themselves, each with its source on the roll
public class CyberwareTableEffectsTests
{
    private static readonly CombatOperations Combat = new(Rules, Calculator, new FixedDice(5));
    private static readonly AttackResolver Resolver = new(CombatRuleBook.LoadEmbedded(), Rules);

    private static CharacterSheet With(params CatalogItem[] items) => CompletePackage() with
    {
        Cyberware = items.Select((i, n) => new CharacterCyberware { InstanceId = $"cw{n}", ItemId = i.Id }).ToList(),
    };

    private static CharacterReport Calc(CharacterSheet s) => Calculator.Calculate(s, Catalog);
    private static CharacterChange Change(CharacterSheet s) => new(s, Catalog, DateTime.UtcNow);

    [Fact]
    public void Kerenzikov_and_targeting_scope_reach_the_table_as_labeled_traits()
    {
        var traits = Calc(With(NeuralLink, Kerenzikov, Cybereye, TargetingScope)).CyberTraits!;
        traits.Should().Contain(t => t.Kind == "initiative" && t.Add == 2 && t.Label == Kerenzikov.Name);
        traits.Should().Contain(t => t.Kind == "aimed" && t.Add == 1);
    }

    [Fact]
    public void A_switched_on_sandevistan_replaces_kerenzikov_instead_of_stacking()
    {
        var traits = Calc(With(NeuralLink, Kerenzikov, Sandevistan)).CyberTraits!;
        FighterViews.InitiativeParts(traits, new Dictionary<string, ActiveCyberState>()).Sum(p => p.Value).Should().Be(2);
        var on = new Dictionary<string, ActiveCyberState> { ["sandevistan"] = new(3, 1) };
        var parts = FighterViews.InitiativeParts(traits, on);
        parts.Sum(p => p.Value).Should().Be(3, "Speedware doesn't stack (CP:R p.359)");
        parts.Should().ContainSingle(p => p.Label == Sandevistan.Name);
    }

    [Fact]
    public void Pain_editor_cancels_the_seriously_wounded_penalty_and_says_so()
    {
        var hurt = With(PainEditor) with { Combat = new CombatState { CurrentHp = 5 } };
        var report = Calc(hurt);
        report.Health.WoundState.Should().Be(WoundState.Seriously);
        report.ActionPenalty.Should().Be(0);
        report.ActionPenalties.Should().Contain(p => p.Label.Contains(PainEditor.Name) && p.Value > 0);
        Calc(CompletePackage() with { Combat = new CombatState { CurrentHp = 5 } }).ActionPenalty.Should().BeNegative();
    }

    [Fact]
    public void Internal_hydraulics_lift_ref_dex_and_move_to_their_floor()
    {
        var weak = With(Hydraulics) with { Stats = new Dictionary<string, int>(CompletePackage().Stats) { ["REF"] = 2, ["DEX"] = 3, ["MOVE"] = 7 } };
        var stats = Calc(weak).Stats.ToDictionary(s => s.Key, s => s.Effective);
        stats["REF"].Should().Be(4);
        stats["DEX"].Should().Be(4);
        stats["MOVE"].Should().Be(7, "already above the floor");
    }

    [Fact]
    public void An_immune_implant_stops_the_injury_but_not_the_bonus_damage()
    {
        var sheet = With(RacerBracer);
        var hp = Calc(sheet).Health.CurrentHp;
        var change = Change(sheet);
        var after = Combat.AddInjury(sheet, new AddInjuryRequest(0, InjuryLocation.Head, "Whiplash"), change);

        after.Injuries.Should().NotContain(i => i.Name == "Whiplash");
        Calc(after).Health.CurrentHp.Should().Be(hp - 5);
        change.Notices.Should().Contain(n => n.Contains("Whiplash"));
    }

    [Fact]
    public void Cyberskull_keeps_head_hits_from_doubling()
    {
        var plain = Combat.Damage(CompletePackage(), new DamageRequest(0, 10, InjuryLocation.Head, BypassArmor: true), Change(CompletePackage()));
        var skull = With(Cyberskull);
        var shielded = Combat.Damage(skull, new DamageRequest(0, 10, InjuryLocation.Head, BypassArmor: true), Change(skull));
        (Calc(CompletePackage()).Health.CurrentHp - Calc(plain).Health.CurrentHp).Should().Be(20);
        (Calc(skull).Health.CurrentHp - Calc(shielded).Health.CurrentHp).Should().Be(10);
    }

    [Fact]
    public void A_popup_shield_is_the_shield_in_hand_and_takes_damage_on_the_implant()
    {
        var sheet = With(PopupShield);
        Calc(sheet).Health.ShieldHp.Should().Be(10);
        var inventory = new InventoryOperations(Rules, new CyberwareOperations(Rules, new CyberwareInstaller(Rules, new FixedDice(2)), Calculator, new FixedDice(2)));
        var hit = inventory.AdjustShield(sheet, -4, null, Change(sheet));
        Calc(hit).Health.ShieldHp.Should().Be(6);
        hit.Cyberware.Single().CurrentHp.Should().Be(6);
    }

    [Fact]
    public void Situational_bonuses_come_as_numbers_to_tick_and_stay_out_of_the_base()
    {
        var line = Calc(With(ImageEnhanceFree)).Skills.Single(s => s.SkillId == "concentration");
        line.SituationalParts.Should().ContainSingle(p => p.Value == 2);
        line.Total.Should().Be(Calc(CompletePackage()).Skills.Single(s => s.SkillId == "concentration").Total);
    }

    private static FighterView Shooter(IReadOnlyList<CyberTraitLine> traits) =>
        new("A", CombatantKind.Pc, "A", CombatSide.Ally, null, 40, 40, WoundState.Unhurt, 0, null, 0, 0, 0, 0, 6, 6, 6, 6, 6, 10, 8, 8,
            [new WeaponView("gun", "Rifle", WeaponClass.AssaultRifle, "shoulder-arms", 14, null, "5d6", 1, 25, 25, null, false, false)], [], [],
            0, 6, false, false, false, null, null, null, null, Traits: traits);

    private static FighterView Target(bool coProcessor = false) =>
        new("B", CombatantKind.Pc, "B", CombatSide.Enemy, null, 40, 40, WoundState.Unhurt, 0, null, 0, 0, 0, 0, 5, 6, 6, 6, 6, 10, 8, 8,
            [], [], [], 0, 6, false, false, false, null, null, null, null, DodgeAnyRef: coProcessor);

    [Fact]
    public void Teleoptics_adds_one_only_at_51_meters_or_more_and_the_card_says_where_it_came_from()
    {
        var traits = Calc(With(Cybereye, Teleoptics)).CyberTraits!;
        AttackRequest Shot(int range) => new(1, "A", ["B"], "gun", AttackMode.Single, range, false, 0, 5, null, [1, 1, 1, 1, 1]);
        var near = Resolver.Resolve(Shot(30), Shooter(traits), [Target()], new FixedDice(1));
        var far = Resolver.Resolve(Shot(60), Shooter(traits), [Target()], new FixedDice(1));
        far.AttackTotal.Should().Be(near.AttackTotal + 1);
        far.Notes.Should().Contain(n => n.StartsWith("Cyberware") && n.Contains(Teleoptics.Name));
    }

    [Fact]
    public void Reflex_co_processor_lets_a_low_ref_target_dodge_bullets()
    {
        Target().CanDodge(8).Should().BeFalse();
        Target(coProcessor: true).CanDodge(8).Should().BeTrue();
        Calc(With(NeuralLink, ReflexCoProcessor)).CyberTraits.Should().Contain(t => t.Kind == "dodgeAnyRef");
    }
}
