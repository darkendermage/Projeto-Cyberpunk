using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Tests.Unit.NightMarkets;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Encounters;

// Martial Arts Special Moves at the table: strikes that land a Critical Injury, Cut the Bullet against a shot
public class MartialArtsMoveTests
{
    private static readonly AttackResolver Resolver = new(CombatRuleBook.LoadEmbedded(), Rules);
    private static readonly MartialArtsRuleBook Book = MartialArtsRuleBook.Embedded;

    private static FighterView Fighter(string id, params WeaponView[] weapons) =>
        new(id, CombatantKind.Pc, id, CombatSide.Ally, null, 40, 40, WoundState.Unhurt, 0, null, 0, 0, 0, 0, 6, 6, 6, 6, 6, 10, 8, 8,
            weapons, [], [], 0, 6, false, false, false, null, null, null, null);

    [Fact]
    public void Every_form_has_moves_and_every_strike_names_a_real_critical_injury()
    {
        Book.Document.Forms.Should().HaveCountGreaterThan(20);
        Book.Document.Forms.Should().OnlyContain(f => f.Moves.Count > 0 && f.Page > 0);
        foreach (var strike in Book.Document.Forms.SelectMany(f => f.Moves).SelectMany(m => m.Strikes).Where(s => s.Injury is not null))
            (Rules.FindCriticalInjury(strike.Head ? InjuryLocation.Head : InjuryLocation.Body, strike.Injury!)).Should().NotBeNull(strike.Injury);
    }

    [Fact]
    public void Knockout_punch_is_a_minus_five_strike_to_the_head_that_breaks_the_jaw()
    {
        var (form, move) = Book.Move("Boxing", "knockout-punch")!.Value;
        form.Source.Should().Be("Interface RED 4");
        move.Kind.Should().Be("attack");
        move.Requires.Should().ContainSingle(r => r.Stat == "BODY" && r.Min == 8);
        var strike = move.Strikes.Single();
        strike.Modifier.Should().Be(-5);
        strike.Head.Should().BeTrue();
        strike.Injury.Should().Be("Broken Jaw");

        MartialArtsRuleBook.Meets(move, s => s == "BODY" ? 7 : 6, _ => 0, 1).Should().BeFalse();
        MartialArtsRuleBook.Meets(move, s => s == "BODY" ? 8 : 6, _ => 0, 1).Should().BeTrue();
    }

    [Fact]
    public void A_strike_lands_on_the_head_and_carries_its_injury()
    {
        var punch = new WeaponView("martial-arts:Boxing", "Martial Arts (Boxing)", null, "martial-arts:Boxing", 9, null, "2d6", 1, null, null, null, true, true,
            Traits: new WeaponTraits { NoAimed = true, HitsHead = true, InjuryOnHit = "Broken Jaw" });
        var r = new AttackRequest(1, "A", ["B"], punch.Key, AttackMode.Single, null, false, 0, 10, new Dictionary<string, int> { ["B"] = 1 }, [3, 3], AttackExtra: 5);
        var hit = Resolver.Resolve(r, Fighter("A", punch), [Fighter("B")], new FixedDice(1)).Hits.Single();

        hit.Hit.Should().BeTrue();
        hit.Location.Should().Be(InjuryLocation.Head);
        hit.InjuryOnHit.Should().Be("Broken Jaw");
    }

    [Fact]
    public void Commando_disarm_is_a_strike_the_target_resists_with_brawling_and_deals_no_damage()
    {
        var (_, move) = Book.Move("Militech Commando Training", "commando-disarm")!.Value;
        move.Kind.Should().Be("attack");
        move.Strikes.Single().Versus.Should().Be("brawling");

        var disarm = new WeaponView("martial-arts:x", "Martial Arts", null, "martial-arts:x", 13, null, "2d6", 1, null, null, null, true, true,
            Traits: new WeaponTraits { NoAimed = true, OpposedBy = "brawling", Effect = "pega a arma", NoCrit = true });
        var brawler = Fighter("B", new WeaponView("brawling", "Brawling", null, "brawling", 9, null, "1d6", 2, null, null, null, true, false));
        var r = new AttackRequest(1, "A", ["B"], disarm.Key, AttackMode.Single, null, false, 0, 5, new Dictionary<string, int> { ["B"] = 4 }, [6, 6]);
        var hit = Resolver.Resolve(r, Fighter("A", disarm), [brawler], new FixedDice(1)).Hits.Single();

        hit.Dv.Should().Be(9 + 4, "Brawling 9 + d10 4, not Evasion");
        hit.Hit.Should().BeTrue();
        hit.Damage.Should().Be(0);
        hit.Critical.Should().BeFalse();
    }

    [Fact]
    public void Cut_the_bullet_negates_a_shot_it_beats_and_lets_through_one_it_loses_to()
    {
        var gun = new WeaponView("gun", "Pistol", WeaponClass.HeavyPistol, "handgun", 14, null, "3d6", 2, 8, 8, null, false, false);
        AttackRequest Shot(int cut) => new(1, "A", ["B"], "gun", AttackMode.Single, 5, false, 0, 5, null, [1, 1, 1], CutTheBullet: new() { ["B"] = cut });

        var cutOut = Resolver.Resolve(Shot(19), Fighter("A", gun), [Fighter("B")], new FixedDice(1)).Hits.Single();
        cutOut.Hit.Should().BeFalse("14 + 5 = 19 ties, and the defender wins ties");
        cutOut.Defense.Should().Contain("Cut the Bullet");

        Resolver.Resolve(Shot(18), Fighter("A", gun), [Fighter("B")], new FixedDice(1)).Hits.Single().Hit.Should().BeTrue();
    }
}
