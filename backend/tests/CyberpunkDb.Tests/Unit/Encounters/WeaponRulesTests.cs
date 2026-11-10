using System.Text.Json;
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

// weapon-rules.json against the real catalog: every weapon attacks in every mode it offers, and the odd ones
// (autofire-only, burst, shells only, railguns, stun weapons, bolt actions...) follow their books.
public class WeaponRulesTests
{
    private static readonly CombatRuleBook Combat = CombatRuleBook.LoadEmbedded();
    private static readonly AttackResolver Resolver = new(Combat, CharacterTestData.Rules);
    private static readonly WeaponRuleBook Book = WeaponRuleBook.Embedded;

    private sealed record SeedWeapon(string Slug, string Name, WeaponClass Class, string Skill, string? Damage, int? Rof, int? Magazine,
        int? Autofire, WeaponQuality Quality, int? MinBody);

    private static readonly List<SeedWeapon> Catalog = LoadCatalog();

    private static List<SeedWeapon> LoadCatalog()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seed", "01-compendium.json")));
        int? Int(JsonElement a, string name) => a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
        string? Str(JsonElement a, string name) => a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        return doc.RootElement.EnumerateArray()
            .Where(x => x.GetProperty("category").GetString() == "weapons")
            .Select(x =>
            {
                var a = x.GetProperty("attributes");
                var skill = NpcTestData.Rules.SkillIdFor(Enum.Parse<WeaponSkill>(Str(a, "skill")!))!;
                return new SeedWeapon(x.GetProperty("slug").GetString()!, x.GetProperty("name").GetString()!, Enum.Parse<WeaponClass>(Str(a, "weaponClass")!),
                    skill, Str(a, "damage"), Int(a, "rof"), Int(a, "magazine"), Int(a, "autofire"),
                    Str(a, "quality") is { } q ? Enum.Parse<WeaponQuality>(q) : WeaponQuality.Standard, Int(a, "minBody"));
            }).ToList();
    }

    private static SeedWeapon Seed(string slug) => Catalog.Single(w => w.Slug == slug);

    // every skill at 12; NPC-style magazines (extras start full)
    private static WeaponView View(SeedWeapon w, string? activeMode = null, int? rounds = null, int body = 10, bool jammed = false)
    {
        var melee = w.Skill == "melee-weapon";
        var view = new WeaponView("w", w.Name, w.Class, w.Skill, 12, w.Autofire is null ? null : 12, w.Damage ?? "", w.Rof ?? 1,
            w.Magazine is null ? null : rounds ?? w.Magazine, w.Magazine, w.Autofire, melee, melee, Quality: w.Quality, Jammed: jammed);
        var context = new WeaponContext(_ => 12, id => id, body, [], activeMode, _ => null, true, false, w.MinBody);
        return WeaponModes.Build(view, w.Slug, context, Book);
    }

    private static WeaponView View(string slug, string? activeMode = null, int? rounds = null, int body = 10, bool jammed = false) =>
        View(Seed(slug), activeMode, rounds, body, jammed);

    private static FighterView Fighter(string id, int body = 10, params WeaponView[] weapons) =>
        new(id, CombatantKind.Pc, id, CombatSide.Ally, null, 40, 40, WoundState.Unhurt, 0, null, 11, 11, 11, 11, 6, 6, body, 6, 6, 10, 12, 8,
            weapons, [], [], 0, 6, false, false, false, null, null, null, null, Cyberware: ["targeting-scope"]);

    private static PlannedAttack Shoot(WeaponView weapon, AttackMode mode = AttackMode.Single, int? range = 10, string? weaponMode = null,
        int attackRoll = 5, List<int>? damage = null, int body = 10, params int[] dice) =>
        Resolver.Resolve(new AttackRequest(1, "A", ["B"], "w", mode, range, AttackRoll: attackRoll, DamageDice: damage, WeaponMode: weaponMode),
            Fighter("A", body, weapon), [Fighter("B")], new FixedDice(dice.Length == 0 ? [3] : dice));

    // what the combat table offers for a mode (combat.ts modesFor)
    private static IEnumerable<AttackMode> Offered(WeaponView w)
    {
        var t = w.Traits;
        if (w.AutofireOnly == true)
        {
            yield return AttackMode.Autofire;
            yield return AttackMode.Suppressive;
            yield break;
        }
        if (t?.Shells != "only") yield return AttackMode.Single;
        if (w.AutofireMultiplier is not null)
        {
            yield return AttackMode.Autofire;
            yield return AttackMode.Suppressive;
        }
        if ((!w.Melee || w.Skill == "melee-weapon") && t?.NoAimed != true && t?.Shells != "only") yield return AttackMode.AimedHead;
        if (t?.Shells != "none" && (w.Class == WeaponClass.Shotgun || t?.Shells == "only")) yield return AttackMode.ShotgunShell;
        if (t?.Explosive == true || w.Class is WeaponClass.GrenadeLauncher or WeaponClass.RocketLauncher) yield return AttackMode.Explosive;
    }

    [Fact]
    public void Every_rule_points_at_a_catalog_weapon()
    {
        var slugs = Catalog.Select(w => w.Slug).ToHashSet();
        Book.Document.Weapons.Keys.Where(k => !slugs.Contains(k)).Should().BeEmpty();
    }

    [Fact]
    public void Rules_only_use_rows_magazines_and_skills_that_exist()
    {
        var rows = Combat.Document.SingleShot.Value.Select(r => r.Key).Append(WeaponModes.ThrownRow).ToHashSet();
        var problems = new List<string>();
        foreach (var (slug, rule) in Book.Document.Weapons)
        {
            var magazines = rule.Magazines.Select(m => m.Id).ToHashSet();
            foreach (var mode in rule.Modes.Append(rule.Base).OfType<WeaponModeRule>())
            {
                if (mode.Row is { } row && !rows.Contains(row)) problems.Add($"{slug}: row {row}");
                if (mode.Magazine is { } m && !magazines.Contains(m)) problems.Add($"{slug}: magazine {m}");
                if (mode.Drains is { } d && !magazines.Contains(d)) problems.Add($"{slug}: drains {d}");
                if (mode.Skill is { } s && NpcTestData.Rules.FindSkill(s) is null) problems.Add($"{slug}: skill {s}");
                if (mode.Switch is { } sw && sw is not ("free" or "action" or "move" or "minute")) problems.Add($"{slug}: switch {sw}");
            }
            if (rule.Modes.Select(m => m.Id).Distinct().Count() != rule.Modes.Count) problems.Add($"{slug}: repeated mode id");
        }
        problems.Should().BeEmpty();
    }

    [Fact]
    public void Every_catalog_weapon_attacks_in_every_mode_it_offers()
    {
        var failures = new List<string>();
        foreach (var seed in Catalog)
        {
            var top = View(seed);
            var actives = (top.Modes ?? []).Where(m => m.ModeSwitch is not (null or WeaponModes.Free)).Select(m => m.ModeId).Prepend(null).Distinct();
            foreach (var active in actives)
            {
                var weapon = View(seed, active);
                foreach (var mode in (weapon.Modes ?? [weapon]).Where(m => m.ModeActive))
                foreach (var attack in Offered(mode))
                {
                    var range = mode.Melee ? (int?)null : Math.Min(10, mode.Traits?.MaxRange ?? 10);
                    try
                    {
                        var plan = Shoot(weapon, attack, range, mode.ModeId);
                        if (plan.UsesAmmo && plan.Rounds > (mode.Rounds ?? 0)) failures.Add($"{seed.Slug} [{mode.ModeId}] {attack}: {plan.Rounds} rounds from {mode.Rounds}");
                    }
                    catch (RequestValidationException e)
                    {
                        failures.Add($"{seed.Slug} [{mode.ModeId ?? "-"}] {attack}: {string.Join(" ", e.Errors.SelectMany(x => x.Value))}");
                    }
                }
            }
        }
        failures.Should().BeEmpty();
    }

    [Fact]
    public void Standard_shotgun_fires_a_five_dice_slug_or_a_shell()
    {
        var shotgun = View("shotgun-padrao");

        Shoot(shotgun).Hits.Single().Dice.Should().HaveCount(5, "the slug is 5d6 (CP:R p.341)");
        Shoot(shotgun, AttackMode.ShotgunShell, null).Hits.Single().Dice.Should().HaveCount(3);
    }

    [Fact]
    public void Machine_guns_borrow_the_assault_rifle_tables()
    {
        var lmg = View("militech-mk-27-lmg");

        Shoot(lmg, AttackMode.Autofire, 10).Hits.Single().Dv.Should().Be(20, "Assault Rifle Autofire at 7-12 m");
        Shoot(lmg, AttackMode.Single, 10).Hits.Single().Dv.Should().Be(16, "Assault Rifle single shot at 7-12 m");
        Shoot(lmg).Notes.Should().Contain(n => n.Contains("Rifle de Assalto"));
    }

    [Fact]
    public void Autofire_only_weapons_refuse_single_shots_and_spend_their_own_bullets()
    {
        var pepper = View("fed-arms-pepper-shaker");
        var helix = View("tsunami-arms-helix");

        var single = () => Shoot(pepper);
        single.Should().Throw<RequestValidationException>();
        Shoot(pepper, AttackMode.Autofire).Rounds.Should().Be(6);
        Shoot(helix, AttackMode.Autofire).Rounds.Should().Be(20);
    }

    [Fact]
    public void Masamune_fires_three_rounds_or_empties_the_magazine_for_less()
    {
        Shoot(View("arasaka-hjsh-18-masamune")).Should().Match<PlannedAttack>(p => p.Rounds == 3 && p.Hits[0].Dice.Count == 5);
        var low = Shoot(View("arasaka-hjsh-18-masamune", rounds: 2));
        low.Rounds.Should().Be(2);
        low.Hits.Single().Dice.Should().HaveCount(4);
    }

    [Fact]
    public void Burst_mode_needs_three_rounds_and_counts_as_armor_piercing()
    {
        var full = View("ps-e-tack-rapid-responder");
        var burst = Shoot(full, weaponMode: "burst");
        burst.Rounds.Should().Be(3);
        burst.Hits.Single().Should().Match<PlannedHit>(h => h.Dice.Count == 3 && h.Ablation == 2);

        var act = () => Shoot(View("ps-e-tack-rapid-responder", rounds: 2), weaponMode: "burst");
        act.Should().Throw<RequestValidationException>();
        Shoot(View("ps-e-tack-rapid-responder", rounds: 2), weaponMode: "single").Rounds.Should().Be(1);
    }

    [Fact]
    public void Ocelot_autofires_four_slugs_on_the_smg_table_and_has_no_shells()
    {
        var ocelot = View("hello-cutie-my-friend-ocelot");

        var shot = Shoot(ocelot, AttackMode.Autofire, 10);
        shot.Rounds.Should().Be(4);
        shot.Hits.Single().Dv.Should().Be(17, "SMG Autofire at 7-12 m");
        var shell = () => Shoot(ocelot, AttackMode.ShotgunShell, null);
        shell.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Shell_only_weapons_fire_shotgun_shells_even_as_pistols()
    {
        var crusher = View("militech-crusher");
        var single = () => Shoot(crusher);
        single.Should().Throw<RequestValidationException>();
        Shoot(crusher, AttackMode.ShotgunShell, null).Hits.Single().Dv.Should().Be(13);

        var zhuo = Shoot(View("kang-tao-l-69-zhuo"), AttackMode.ShotgunShell, null);
        zhuo.Rounds.Should().Be(8);
        zhuo.Hits.Single().Dice.Should().HaveCount(4);
    }

    [Fact]
    public void Railgun_and_stun_traits_reach_the_hit()
    {
        Shoot(View("rhinemetall-emg-86-railgun")).Hits.Single().IgnoreArmorBelow.Should().Be(11);
        var aimed = () => Shoot(View("rhinemetall-emg-86-railgun"), AttackMode.AimedHead);
        aimed.Should().Throw<RequestValidationException>();

        var stun = Shoot(View("stun-gun"), attackRoll: 9, dice: [6]);
        stun.Hits.Single().Should().Match<PlannedHit>(h => h.Knockout && h.NoCrit && h.NoAblation && !h.Critical);
    }

    [Fact]
    public void Knockout_leaves_an_npc_unconscious_at_one_hp_without_ablation()
    {
        var npcs = new NpcCombat(CharacterTestData.Rules, CharacterTestData.Calculator);
        var c = new Combatant { Id = "N", Name = "Ganger", Kind = CombatantKind.Npc, Hp = 5, SpBody = 11, SpHead = 11, Snapshot = new CombatSnapshot { MaxHp = 30, Body = 6, MaxSpBody = 11, MaxSpHead = 11 } };

        var outcome = npcs.Damage(c, 30, InjuryLocation.Body, false, false, true, new HitRules(true, true, true, null));

        outcome.Combatant.Hp.Should().Be(1);
        outcome.Combatant.Conditions.Should().Contain(NpcCombat.Unconscious);
        outcome.Combatant.SpBody.Should().Be(11);
    }

    [Fact]
    public void Effect_weapons_deal_no_damage_and_say_what_to_roll()
    {
        var hit = Shoot(View("microwaver"), attackRoll: 9).Hits.Single();

        hit.Hit.Should().BeTrue();
        hit.Damage.Should().Be(0);
        hit.Notes.Should().Contain(n => n.Contains("Cybertech DV15"));
    }

    [Fact]
    public void Fumbles_destroy_cheap_guns_and_bolt_actions_need_an_action()
    {
        var special = Shoot(View("gunmart-special"), attackRoll: 1);
        special.Destroyed.Should().BeTrue();
        special.Jammed.Should().BeFalse("it breaks instead of jamming");

        Shoot(View("everest-kodiak-hunter")).Jammed.Should().BeTrue();
        var again = () => Shoot(View("everest-kodiak-hunter", jammed: true));
        again.Should().Throw<RequestValidationException>().Which.Errors["weapon"].Single().Should().Contain("ferrolho");
    }

    [Fact]
    public void Hellbringer_jams_after_every_shot_below_body_10()
    {
        Shoot(View("magnum-opus-hellbringer", body: 8), body: 8).Jammed.Should().BeTrue();
        Shoot(View("magnum-opus-hellbringer")).Jammed.Should().BeFalse();
    }

    [Fact]
    public void Teen_dreem_empties_the_magazine_and_melts()
    {
        var shot = Shoot(View("teen-dreem"), AttackMode.Autofire);
        shot.Rounds.Should().Be(10);
        shot.Destroyed.Should().BeTrue();
    }

    [Fact]
    public void Rodeo_failing_its_handgun_check_caps_the_multiplier_at_two()
    {
        // Autofire 12 + 9 = 21 vs SMG DV17 at 10 m: margin 4, capped at 2; the check (12 + 1) fails DV15
        var shot = Shoot(View("mustang-arms-rodeo"), AttackMode.Autofire, 10, attackRoll: 9, dice: [1]);
        shot.Notes.Should().Contain(n => n.Contains("DV15") && n.Contains("falhou"));
        shot.Hits.Single().Damage.Should().Be(4);
        Shoot(View("mustang-arms-rodeo"), AttackMode.Autofire, 10, attackRoll: 9, dice: [9]).Notes.Should().Contain(n => n.Contains("passou"));
    }

    [Fact]
    public void Combination_weapon_fires_its_second_half_from_its_own_magazine()
    {
        var tommy = View("tommyknocker");

        tommy.Modes!.Select(m => m.ModeId).Should().Equal("pistol", "shotgun");
        tommy.Magazines!.Should().Contain(m => m.Id == "shotgun" && m.Capacity == 2);
        var shot = Shoot(tommy, AttackMode.ShotgunShell, null, "shotgun");
        shot.MagazineId.Should().Be("shotgun");
        Shoot(tommy, weaponMode: "pistol").MagazineId.Should().BeNull();
    }

    [Fact]
    public void Switched_modes_must_be_on_before_the_attack()
    {
        var act = () => Shoot(View("micro-cutie-sanroo"), weaponMode: "hypurrburst");
        act.Should().Throw<RequestValidationException>();

        var burst = Shoot(View("micro-cutie-sanroo", "hypurrburst"), weaponMode: "hypurrburst");
        burst.Rounds.Should().Be(10, "Hypurrburst drains the magazine");
        burst.Hits.Single().Dice.Should().HaveCount(4);
    }

    [Fact]
    public void Melee_weapons_can_be_thrown_but_only_throwing_weapons_keep_halving_armor()
    {
        var knife = View("heavy-melee-weapon");
        var thrown = Shoot(knife, weaponMode: WeaponModes.ThrowMode);
        thrown.LeavesHand.Should().BeTrue();
        thrown.Hits.Single().HalveArmor.Should().BeFalse("CP:R p.177");
        Shoot(knife, range: null, weaponMode: "melee").Hits.Single().HalveArmor.Should().BeTrue();

        Shoot(View("utility-tomahawk"), weaponMode: WeaponModes.ThrowMode).Hits.Single().HalveArmor.Should().BeTrue();
        var star = View("kendachi-mono-star");
        star.Melee.Should().BeTrue("a Mono-Star still cuts up close");
        Shoot(star, weaponMode: WeaponModes.ThrowMode).Hits.Single().HalveArmor.Should().BeTrue();
    }

    [Fact]
    public void Fired_harpoon_uses_the_bow_row_not_the_throw()
    {
        var harpoon = Shoot(View("slamdance-ballistic-harpoon"), range: 40, weaponMode: "fire");

        harpoon.LeavesHand.Should().BeTrue();
        harpoon.Hits.Single().Dv.Should().Be(17, "Bows & Crossbows at 26-50 m");
    }

    [Fact]
    public void Boomerang_is_only_thrown_and_comes_back_with_a_targeting_scope()
    {
        var boomerang = View("ranger-combat-boomerang");

        boomerang.Modes.Should().BeNull();
        boomerang.Should().Match<WeaponView>(w => w.ThrownOnly && w.HalvesArmor && w.Rof == 2 && w.ReturnsWith == "targeting-scope");
    }
}
