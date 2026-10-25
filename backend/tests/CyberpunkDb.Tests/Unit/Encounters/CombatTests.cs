using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Tests.Unit.Characters;
using CyberpunkDb.Tests.Unit.NightMarkets;
using CyberpunkDb.Tests.Unit.Npcs;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit.Encounters;

public class CombatTests
{
    private static readonly CombatRuleBook Rules = CombatRuleBook.LoadEmbedded();
    private static readonly AttackResolver Resolver = new(Rules, CharacterTestData.Rules);
    private static readonly NpcCombat Npc = new(CharacterTestData.Rules, CharacterTestData.Calculator);
    private static readonly FighterViews Views = new(CharacterTestData.Rules, CharacterTestData.Calculator, NpcTestData.Rules, Rules);

    private static WeaponView Weapon(string key, WeaponClass? cls, int attack, string damage, bool melee = false, int? autofire = null, int? autofireTotal = null) =>
        new(key, key, cls, melee ? "melee-weapon" : "shoulder-arms", attack, autofireTotal, damage, melee ? 2 : 1, null, null, autofire, melee, melee);

    private static FighterView Fighter(string id, int reflex = 6, int evasion = 10, int spBody = 0, params WeaponView[] weapons) =>
        new(id, CombatantKind.Generic, id, CombatSide.Enemy, null, 40, 40, WoundState.Unhurt, 0, null, spBody, spBody, spBody, spBody,
            reflex, 6, 6, 6, 6, evasion, 8, 8, weapons, [], [], 0, 6, false, false, false, null, null, null, null);

    private static AttackRequest Attack(string weapon, AttackMode mode = AttackMode.Single, int? range = null, int? roll = null, int[]? damage = null,
        Dictionary<string, int>? defense = null, bool dodge = false) =>
        new(1, "A", ["B"], weapon, mode, range, dodge, 0, roll, defense, damage?.ToList());

    private static Combatant Npc_(int hp = 40, int sp = 11, int body = 6) => new()
    {
        Id = "B", Kind = CombatantKind.Generic, Name = "Booster", Hp = hp, SpBody = sp, SpHead = sp,
        Snapshot = new CombatSnapshot { MaxHp = 40, Body = body, MaxSpBody = sp, MaxSpHead = sp },
    };

    [Fact]
    public void Every_rule_value_has_a_page_or_is_complementary()
    {
        var missing = new List<string>();
        NpcRuleBookTests.Walk(Rules.Document, "rules", missing, depth: 0);
        missing.Should().BeEmpty();
        Rules.Document.SingleShot.Value.Should().HaveCount(8).And.OnlyContain(r => r.Dv.Length == 8);
        Rules.Document.RangeBands.Value.Should().HaveCount(8);
        CharacterTestData.Rules.Document.Combat.AutofireDv.Bands.Should().HaveCount(5, "o Autofire usa as 5 primeiras faixas");
    }

    [Fact]
    public void Cover_examples_match_the_materials_except_the_metal_door()
    {
        foreach (var e in Rules.Document.CoverExamples.Value.Where(e => e.Label != "Porta de metal"))
            e.Hp.Should().Be(Rules.CoverHp(e.Material, e.Thick), e.Label);
    }

    [Theory]
    [InlineData(4, false, "1d6")]
    [InlineData(4, true, "2d6")]
    [InlineData(6, false, "2d6")]
    [InlineData(8, true, "3d6")]
    [InlineData(11, false, "4d6")]
    public void Unarmed_damage_follows_body(int body, bool cyberarm, string dice) => Rules.UnarmedDice(body, cyberarm).Should().Be(dice);

    /// <summary>CP:R p.170: Royal, Assault Rifle at 24 m, DV15; 4 + 8 REF + 6 Shoulder Arms = 18, hits for 25.</summary>
    [Fact]
    public void Royal_single_shot_from_the_core_book()
    {
        var royal = Fighter("A", 8, 10, 0, Weapon("rifle", WeaponClass.AssaultRifle, 14, "5d6"));
        var plan = Resolver.Resolve(Attack("rifle", range: 24, roll: 4, damage: [5, 5, 5, 5, 5]), royal, [Fighter("B")], new FixedDice(1));

        var hit = plan.Hits.Single();
        hit.Dv.Should().Be(15);
        plan.AttackTotal.Should().Be(18);
        hit.Hit.Should().BeTrue();
        hit.Damage.Should().Be(25);
    }

    /// <summary>CP:R p.173: Autofire at 14 m, DV17; total 21 beats it by 4; 2d6 = 10 × 4 = 40.</summary>
    [Fact]
    public void Royal_autofire_from_the_core_book()
    {
        var royal = Fighter("A", 8, 10, 0, Weapon("rifle", WeaponClass.AssaultRifle, 14, "5d6", autofire: 4, autofireTotal: 14));
        var plan = Resolver.Resolve(Attack("rifle", AttackMode.Autofire, 14, 7, [4, 6]), royal, [Fighter("B")], new FixedDice(1));

        plan.Hits.Single().Dv.Should().Be(17);
        plan.Hits.Single().Damage.Should().Be(40);
        plan.Rounds.Should().Be(10);
    }

    [Fact]
    public void Defender_wins_ties_and_two_sixes_are_a_critical()
    {
        var shooter = Fighter("A", 8, 10, 0, Weapon("pistol", WeaponClass.HeavyPistol, 10, "3d6"));
        Resolver.Resolve(Attack("pistol", range: 5, roll: 3, damage: [6, 6, 1]), shooter, [Fighter("B")], new FixedDice(1))
            .Hits.Single().Hit.Should().BeFalse("13 contra DV13: empate é do defensor");

        var crit = Resolver.Resolve(Attack("pistol", range: 5, roll: 4, damage: [6, 6, 1]), shooter, [Fighter("B")], new FixedDice(1)).Hits.Single();
        crit.Hit.Should().BeTrue();
        crit.Critical.Should().BeTrue();
    }

    [Fact]
    public void Melee_is_opposed_by_evasion_and_halves_armor()
    {
        var clown = Fighter("A", 6, 10, 0, Weapon("machete", WeaponClass.MediumMelee, 12, "2d6", melee: true));
        var plan = Resolver.Resolve(Attack("machete", roll: 5, damage: [3, 4], defense: new() { ["B"] = 6 }), clown, [Fighter("B", 6, 10)], new FixedDice(1));

        var hit = plan.Hits.Single();
        hit.Dv.Should().Be(16, "Evasion 10 + d10 6");
        hit.Hit.Should().BeTrue();
        hit.HalveArmor.Should().BeTrue();
    }

    /// <summary>CP:R p.175: Rico Rico's SP11 counts as SP6 against a machete; 7 damage lets 1 through and ablates to 10.</summary>
    [Fact]
    public void Rico_rico_machete_from_the_core_book()
    {
        var outcome = Npc.Damage(Npc_(sp: 11), 7, InjuryLocation.Body, halveArmor: true, bypassArmor: false, fromAttack: true);

        outcome.Sp.Should().Be(6);
        outcome.Through.Should().Be(1);
        outcome.Combatant.SpBody.Should().Be(10);
        outcome.Combatant.Hp.Should().Be(39);
    }

    [Fact]
    public void Head_damage_through_armor_is_doubled()
    {
        Npc.Damage(Npc_(sp: 11), 15, InjuryLocation.Head, false, false, true).Through.Should().Be(8);
    }

    [Fact]
    public void Dodging_needs_ref_8_and_range_must_be_in_the_table()
    {
        var shooter = Fighter("A", 8, 10, 0, Weapon("pistol", WeaponClass.HeavyPistol, 10, "3d6"));
        var slow = Resolver.Resolve(Attack("pistol", range: 5, roll: 5, damage: [1, 1, 1], dodge: true), shooter, [Fighter("B", reflex: 6)], new FixedDice(1));
        slow.Hits.Single().Defense.Should().Contain("DV13");

        var fast = Resolver.Resolve(Attack("pistol", range: 5, roll: 5, damage: [1, 1, 1], dodge: true, defense: new() { ["B"] = 9 }), shooter,
            [Fighter("B", reflex: 8, evasion: 12)], new FixedDice(1));
        fast.Hits.Single().Dv.Should().Be(21);

        var act = () => Resolver.Resolve(Attack("pistol", range: 300), shooter, [Fighter("B")], new FixedDice(5));
        act.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Critical_injury_rolls_again_until_new_and_deals_bonus_damage()
    {
        var first = Npc.RollCritical(InjuryLocation.Body, [], null, new FixedDice(4, 4));
        first.Name.Should().Be("Broken Leg");
        var second = Npc.RollCritical(InjuryLocation.Body, ["Broken Leg"], null, new FixedDice(4, 4, 3, 3));
        second.Name.Should().NotBe("Broken Leg");

        Npc.AddCritical(Npc_(), first, InjuryLocation.Body).Hp.Should().Be(35);
    }

    [Fact]
    public void Death_save_rises_and_a_ten_always_fails()
    {
        var dying = Npc_(hp: 0, body: 6);
        var (after, survived, _) = Npc.DeathSave(dying, 5);
        survived.Should().BeTrue();
        after.DeathSavePenalty.Should().Be(1);

        Npc.DeathSave(after, 5).Survived.Should().BeFalse("5 + 1 = 6 não fica abaixo de BODY 6");
        Npc.DeathSave(Npc_(hp: 0, body: 12), 10).Survived.Should().BeFalse();
    }

    [Fact]
    public void Wounds_lower_the_attack_of_an_npc()
    {
        var npc = Npc_(hp: 15) with
        {
            Snapshot = new CombatSnapshot
            {
                MaxHp = 40, Body = 6, Stats = new(StringComparer.OrdinalIgnoreCase) { ["REF"] = 7, ["DEX"] = 6 },
                Skills = new(StringComparer.OrdinalIgnoreCase) { ["handgun"] = 13, ["evasion"] = 11 },
                Weapons = [new CombatWeapon { Key = "w0", Name = "Pistola", Class = WeaponClass.HeavyPistol, Skill = "handgun", Damage = "3d6", Magazine = 8 }],
            },
        };
        var view = Views.ForNpc(npc);
        view.WoundState.Should().Be(WoundState.Seriously);
        view.Weapon("w0")!.AttackTotal.Should().Be(11, "13 − 2 de Seriously Wounded");
        view.Evasion.Should().Be(9);
        view.Weapon("w0")!.Rounds.Should().Be(8);
    }
}
