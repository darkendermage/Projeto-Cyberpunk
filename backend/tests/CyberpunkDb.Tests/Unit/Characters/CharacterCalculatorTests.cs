using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Npcs;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

public class CharacterCalculatorTests
{
    private static CharacterReport Calc(CharacterSheet sheet) => Calculator.Calculate(sheet.Normalize(), Catalog);

    private static CharacterSheet WithStat(CharacterSheet s, string key, int value) =>
        s with { Stats = new Dictionary<string, int>(s.Stats, StringComparer.OrdinalIgnoreCase) { [key] = value } };

    // 6. HP after BODY or WILL changes

    [Fact]
    public void Hp_is_ten_plus_five_times_the_rounded_up_average_of_body_and_will()
    {
        var sheet = CompletePackage(); // BODY 7, WILL 6
        Calc(sheet).Health.MaxHp.Should().Be(10 + 5 * 7);              // ceil(6.5) = 7 → 45

        Calc(WithStat(sheet, "BODY", 8)).Health.MaxHp.Should().Be(10 + 5 * 7);   // ceil(7) = 7 → 45
        Calc(WithStat(sheet, "WILL", 8)).Health.MaxHp.Should().Be(10 + 5 * 8);   // ceil(7.5) = 8 → 50
        Calc(WithStat(sheet, "BODY", 2)).Health.MaxHp.Should().Be(10 + 5 * 4);   // ceil(4) = 4 → 30
    }

    [Fact]
    public void Full_hp_follows_the_maximum_when_current_hp_was_never_set()
    {
        var sheet = CompletePackage();
        Calc(WithStat(sheet, "WILL", 8)).Health.CurrentHp.Should().Be(50);
    }

    // 7–8. Seriously Wounded threshold and penalty

    [Fact]
    public void Seriously_wounded_threshold_is_half_the_hp_rounded_up()
    {
        Calc(CompletePackage()).Health.SeriouslyWoundedThreshold.Should().Be(23); // 45 / 2 = 22.5 → 23
        Calc(WithStat(CompletePackage(), "WILL", 8)).Health.SeriouslyWoundedThreshold.Should().Be(25);
    }

    [Fact]
    public void Seriously_wounded_applies_below_the_threshold_and_is_removed_when_healed()
    {
        var sheet = CompletePackage();

        var atThreshold = Calc(sheet with { Combat = new CombatState { CurrentHp = 23 } });
        atThreshold.Health.WoundState.Should().Be(WoundState.Lightly, "o livro diz 'menos que' metade (CP:R p.186)");
        atThreshold.ActionPenalty.Should().Be(0);

        var below = Calc(sheet with { Combat = new CombatState { CurrentHp = 22 } });
        below.Health.WoundState.Should().Be(WoundState.Seriously);
        below.ActionPenalty.Should().Be(-2);
        below.Skills.Single(s => s.Key == "handgun").Total.Should().Be(below.Skills.Single(s => s.Key == "handgun").Base - 2);

        var healed = Calc(sheet with { Combat = new CombatState { CurrentHp = 40 } });
        healed.Health.WoundState.Should().Be(WoundState.Lightly);
        healed.ActionPenalty.Should().Be(0);
    }

    [Fact]
    public void Mortally_wounded_replaces_seriously_wounded_and_slows_move_to_at_least_one()
    {
        var report = Calc(CompletePackage() with { Combat = new CombatState { CurrentHp = 0 } });

        report.Health.WoundState.Should().Be(WoundState.Mortally);
        report.ActionPenalty.Should().Be(-4, "cada Wound State substitui o anterior");
        report.Stats.Single(s => s.Key == "MOVE").Effective.Should().Be(1);
        report.Health.MustRollDeathSave.Should().BeTrue();
        report.Health.StabilizationDv.Should().Be(15);
    }

    // 9. Death Save and Death Save Penalty

    [Fact]
    public void Death_save_follows_body_and_penalty_adds_injuries_and_accumulated_saves()
    {
        var sheet = CompletePackage() with
        {
            Combat = new CombatState { CurrentHp = 0, DeathSavePenalty = 2 },
            Injuries =
            [
                new CriticalInjuryRecord { Id = "i1", Name = "Collapsed Lung", Location = InjuryLocation.Body },
                new CriticalInjuryRecord { Id = "i2", Name = "Broken Ribs", Location = InjuryLocation.Body },
                new CriticalInjuryRecord { Id = "i3", Name = "Whiplash", Location = InjuryLocation.Head, Status = InjuryStatus.Healed },
            ],
        };

        var h = Calc(sheet).Health;
        h.DeathSave.Should().Be(7);
        h.BaseDeathSavePenalty.Should().Be(1, "só Collapsed Lung ativa aumenta a penalidade base");
        h.DeathSavePenalty.Should().Be(3);
        Calc(WithStat(sheet, "BODY", 5)).Health.DeathSave.Should().Be(5);
    }

    [Fact]
    public void Quick_fixed_injuries_suspend_their_effect()
    {
        var injured = CompletePackage() with
        {
            Injuries = [new CriticalInjuryRecord { Id = "i1", Name = "Broken Leg", Location = InjuryLocation.Body }],
        };
        Calc(injured).Stats.Single(s => s.Key == "MOVE").Effective.Should().Be(2); // 6 - 4

        var fixedLeg = injured with { Injuries = [injured.Injuries[0] with { Status = InjuryStatus.QuickFixed }] };
        Calc(fixedLeg).Stats.Single(s => s.Key == "MOVE").Effective.Should().Be(6);
    }

    [Fact]
    public void Implanted_linear_frame_replaces_body_for_hp_and_death_save()
    {
        var sheet = CompletePackage() with
        {
            Cyberware = [new CharacterCyberware { InstanceId = "f", ItemId = ImplantedFrame.Id, HumanityLossApplied = 14 }],
        };
        var h = Calc(sheet).Health;
        h.BodyForDerived.Should().Be(12);
        h.DeathSave.Should().Be(12);
        h.MaxHp.Should().Be(10 + 5 * 9); // ceil((12 + 6) / 2)
    }

    // ------------------------------------------------------------ cyberware effects on the sheet

    private static CharacterSheet Installed(params CatalogItem[] items) => CompletePackage() with
    {
        Cyberware = items.Select((i, n) => new CharacterCyberware { InstanceId = $"cw-{n}", ItemId = i.Id }).ToList(),
    };

    [Fact]
    public void Grafted_muscle_and_bone_lace_raises_body_up_to_ten_and_changes_hp()
    {
        // BODY 7, WILL 6. One installation: BODY 9 (CP:R p.363), HP 10 + 5 × ceil(15/2) = 50.
        var one = Calc(Installed(BoneLace));
        var body = one.Stats.Single(s => s.Key == "BODY");
        body.Base.Should().Be(7, "o valor comprado não muda");
        body.Effective.Should().Be(9);
        one.Health.BodyForDerived.Should().Be(9);
        one.Health.DeathSave.Should().Be(9);
        one.Health.MaxHp.Should().Be(50);
        one.Health.BodySource.Should().Contain("grafted-muscle-and-bone-lace");

        // Two stack, but BODY can't go above 10: 7 + 2 + 1.
        var two = Calc(Installed(BoneLace, BoneLace)).Stats.Single(s => s.Key == "BODY");
        two.Effective.Should().Be(10);
        two.Parts.Should().ContainSingle(p => p.Label.Contains("teto 10") && p.Value == 3);

        // In the inventory it does nothing until installed.
        Calc(CompletePackage() with { Inventory = [new InventoryEntry { InstanceId = "i", ItemId = BoneLace.Id }] })
            .Stats.Single(s => s.Key == "BODY").Effective.Should().Be(7);
    }

    [Fact]
    public void Cyberware_skill_bonuses_join_the_base_unless_they_depend_on_a_situation()
    {
        var report = Calc(Installed(MedscannerArm, ImageEnhance, Chemskin));

        var firstAid = report.Skills.Single(s => s.Key == "first-aid");
        firstAid.Modifier.Should().Be(2, "Medscanner: +2 em First Aid (CP:R p.365)");
        firstAid.ModifierParts.Should().ContainSingle(p => p.Label == "medscanner-integrated" && p.Value == 2);
        firstAid.Base.Should().Be(firstAid.StatValue + firstAid.Level + 2);

        var perception = report.Skills.Single(s => s.Key == "perception");
        perception.Modifier.Should().Be(0, "Image Enhance só vale em testes que envolvem visão");
        perception.Situational.Should().ContainSingle(s => s.Contains("visão"));

        report.Skills.Single(s => s.Key == "personal-grooming").Modifier.Should().Be(0, "Chemskin sozinho não dá o bônus");
        Calc(Installed(Chemskin, Techhair)).Skills.Single(s => s.Key == "personal-grooming").Modifier.Should().Be(2);

        report.CyberwareEffects.Select(e => e.Source).Should().OnlyContain(s => s.StartsWith("CP:R p."));
        report.CyberwareEffects.Should().HaveCount(2);
    }

    [Fact]
    public void Every_cyberware_effect_cites_a_page_and_names_known_skills_and_stats()
    {
        foreach (var e in Rules.Document.CyberwareEffects.Effects)
        {
            e.Source.Should().StartWith("CP:R p.");
            (e.Items.Length + e.AllOf.Length).Should().BePositive();
            (e.Stat is not null || e.Skills.Length > 0 || e.Note is not null).Should().BeTrue();
            if (e.Stat is not null) Rules.StatOrder.Should().Contain(e.Stat);
            foreach (var skill in e.Skills) Rules.FindSkill(skill).Should().NotBeNull(skill);
        }
    }

    // 10. Humanity and effective Empathy

    [Fact]
    public void Humanity_max_is_emp_times_ten_and_effective_emp_is_the_tens_place()
    {
        var sheet = CompletePackage(); // EMP 6
        var full = Calc(sheet).Humanity;
        full.Max.Should().Be(60);
        full.Current.Should().Be(60);
        full.EmpEffective.Should().Be(6);

        var chromed = Calc(sheet with { Humanity = new HumanityState { Lost = 14 } });
        chromed.Humanity.Current.Should().Be(46);
        chromed.Humanity.EmpEffective.Should().Be(4);
        chromed.Stats.Single(s => s.Key == "EMP").Effective.Should().Be(4);
        chromed.Stats.Single(s => s.Key == "EMP").Base.Should().Be(6);

        Calc(sheet with { Humanity = new HumanityState { Lost = 61 } }).Humanity.Cyberpsychosis.Should().BeTrue();
        Calc(sheet with { Humanity = new HumanityState { Lost = 60 } }).Humanity.Cyberpsychosis.Should().BeFalse();
    }

    [Fact]
    public void Therapy_cap_drops_two_per_piece_four_per_borgware_and_ignores_zero_loss_pieces()
    {
        var sheet = CompletePackage() with
        {
            Cyberware =
            [
                new CharacterCyberware { InstanceId = "nl", ItemId = NeuralLink.Id, HumanityLossApplied = 7 },
                new CharacterCyberware { InstanceId = "sv", ItemId = Sandevistan.Id, ParentInstanceId = "nl", HumanityLossApplied = 7 },
                new CharacterCyberware { InstanceId = "bm", ItemId = Biomonitor.Id, HumanityLossApplied = 0 },
                new CharacterCyberware { InstanceId = "dp", ItemId = DragoonPlating.Id, HumanityLossApplied = 14 },
            ],
            Humanity = new HumanityState { Lost = 28 },
        };

        var h = Calc(sheet).Humanity;
        h.PiecesLoweringCap.Should().Be(2);
        h.BorgwareLoweringCap.Should().Be(1);
        h.TherapyCap.Should().Be(60 - 2 * 2 - 4);
        h.Current.Should().Be(32);
    }

    // 11. Modifiers never overwrite base values

    [Fact]
    public void Modifiers_change_effective_values_but_never_the_base()
    {
        var sheet = CompletePackage() with
        {
            Modifiers =
            [
                new StatModifier { Id = "m1", Target = ModifierTarget.Stat, Key = "REF", Value = 1, Source = "Synthcoke" },
                new StatModifier { Id = "m2", Target = ModifierTarget.Skill, Key = "handgun", Value = 2, Source = "Smartgun" },
                new StatModifier { Id = "m3", Target = ModifierTarget.Stat, Key = "COOL", Value = 3, Source = "Inativo", Active = false },
            ],
        };

        var report = Calc(sheet);
        var reflexes = report.Stats.Single(s => s.Key == "REF");
        reflexes.Base.Should().Be(7);
        reflexes.Effective.Should().Be(8);
        report.Stats.Single(s => s.Key == "COOL").Effective.Should().Be(6);

        var handgun = report.Skills.Single(s => s.Key == "handgun");
        handgun.StatValue.Should().Be(8);
        handgun.Level.Should().Be(6);
        handgun.Modifier.Should().Be(2);
        handgun.Base.Should().Be(16);

        sheet.Stats["REF"].Should().Be(7, "o valor-base continua intacto");
    }

    // 22 (calculation side). Armor penalty applied once, SP does not stack

    [Fact]
    public void Armor_penalty_is_taken_once_and_only_the_highest_sp_counts()
    {
        var sheet = CompletePackage() with
        {
            Inventory =
            [
                new InventoryEntry { InstanceId = "b", ItemId = HeavyArmorjack.Id, State = ItemState.Equipped, Worn = ArmorSlot.Body },
                new InventoryEntry { InstanceId = "h", ItemId = MediumArmorjack.Id, State = ItemState.Equipped, Worn = ArmorSlot.Head },
                new InventoryEntry { InstanceId = "c", ItemId = Flak.Id, State = ItemState.Carried },
            ],
            Cyberware = [new CharacterCyberware { InstanceId = "sd", ItemId = SubdermalArmor.Id, HumanityLossApplied = 14 }],
        };

        var report = Calc(sheet);
        report.Armor.Penalty.Ref.Should().Be(2, "a penalidade mais pesada vale uma vez só; Flak carregada não conta");
        report.Stats.Single(s => s.Key == "REF").Effective.Should().Be(5);
        report.Stats.Single(s => s.Key == "MOVE").Effective.Should().Be(4);
        report.Armor.Body.Sp.Should().Be(13);
        report.Armor.Head.Sp.Should().Be(12);
        report.Armor.Head.Name.Should().Be("medium-armorjack");
        report.Skills.Single(s => s.Key == "handgun").StatValue.Should().Be(5);
    }

    [Fact]
    public void Ablated_armor_uses_current_sp_against_implants()
    {
        var sheet = CompletePackage() with
        {
            Inventory = [new InventoryEntry { InstanceId = "b", ItemId = LightArmorjack.Id, State = ItemState.Equipped, Worn = ArmorSlot.Body, CurrentSp = 9 }],
            Cyberware = [new CharacterCyberware { InstanceId = "sd", ItemId = SubdermalArmor.Id, HumanityLossApplied = 14 }],
        };

        var body = Calc(sheet).Armor.Body;
        body.Sp.Should().Be(11);
        body.FromCyberware.Should().BeTrue();
    }

    [Fact]
    public void Skill_lines_cover_every_skill_and_flag_basic_and_role_skills()
    {
        var report = Calc(CompletePackage());

        report.Skills.Select(s => s.SkillId).Distinct().Should().HaveCount(Rules.Skills.Count);
        var streetslang = report.Skills.Single(s => s.Key == "language:Streetslang");
        streetslang.Basic.Should().BeTrue();
        streetslang.NamePt.Should().Be("Idioma");
        report.Skills.Single(s => s.Key == "tactics").RoleSkill.Should().BeTrue();
        report.Skills.Single(s => s.Key == "accounting").RoleSkill.Should().BeFalse();
        report.Roles.Single().Ability.Should().Be("Combat Awareness");
    }
}
