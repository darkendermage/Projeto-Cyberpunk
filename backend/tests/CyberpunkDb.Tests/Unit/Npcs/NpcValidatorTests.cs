using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Npcs.NpcTestData;

namespace CyberpunkDb.Tests.Unit.Npcs;

public class NpcValidatorTests
{
    private static NpcValidationReport Validate(NpcSheet sheet) => Validator.Validate(sheet, Catalog);

    private static NpcSheet WithStat(NpcSheet sheet, string key, int value)
    {
        var stats = new Dictionary<string, int>(sheet.Stats, StringComparer.OrdinalIgnoreCase) { [key] = value };
        return sheet with { Stats = stats };
    }

    private static NpcSheet WithSkill(NpcSheet sheet, string key, int value)
    {
        var skills = new Dictionary<string, int>(sheet.Skills, StringComparer.OrdinalIgnoreCase) { [key] = value };
        return sheet with { Skills = skills };
    }

    [Fact]
    public void Hand_built_mook_is_valid_and_reports_budgets()
    {
        var report = Validate(ValidMook());

        report.Issues.Where(i => i.Severity == NpcIssueSeverity.Error).Should().BeEmpty();
        report.IsValid.Should().BeTrue();
        report.Status.Should().Be(NpcStatus.Valid);
        report.Stats.Should().BeEquivalentTo(new { Used = 31, Total = 33, Remaining = 2 });
        report.Skills.Should().BeEquivalentTo(new { Used = 30, Total = 30, Remaining = 0 });
        report.Gear.Should().BeEquivalentTo(new { Used = 220, Total = 300, Remaining = 80 });
        report.Cyberware.Should().BeEquivalentTo(new { Used = 500, Total = 500, Remaining = 0 });
        report.Issues.Should().Contain(i => i.Code == "STAT_BUDGET_UNSPENT" && i.Message.Contains("Restam 2 pontos"));
    }

    [Fact]
    public void Derived_values_follow_the_core_formula()
    {
        // Hardened Mook from DGD p.53: BODY 4, WILL 5 -> 35 HP, Seriously Wounded 18, Death Save 4.
        var sheet = WithStat(WithStat(ValidMook(), "BODY", 4), "WILL", 5);
        var d = Validate(sheet).Derived;

        d.Hp.Should().Be(35);
        d.SeriouslyWounded.Should().Be(18);
        d.DeathSave.Should().Be(4);
        d.SpBody.Should().Be(7);
        d.SpHead.Should().Be(7);
    }

    [Fact]
    public void Skill_base_is_stat_plus_level_minus_armor_penalty()
    {
        var sheet = ValidMook() with
        {
            Gear = [new NpcGearEntry { ItemId = MediumArmorjack.Id, Worn = ArmorSlot.Body }],
        };
        var report = Validate(sheet);

        var handgun = report.SkillLines.Single(l => l.Key == "handgun");
        handgun.Level.Should().Be(5);
        handgun.Base.Should().Be(6 - 2 + 5, "REF 6 com -2 da Medium Armorjack, mais o nível 5");
        report.Derived.EffectiveStats["REF"].Should().Be(4);
    }

    [Fact]
    public void Stat_budget_overflow_is_blocking_with_the_exact_difference()
    {
        var sheet = WithStat(WithStat(ValidMook(), "INT", 5), "TECH", 3); // 31 + 2 + 1 = 34

        var report = Validate(sheet);

        report.Stats.Used.Should().Be(34);
        report.IsValid.Should().BeFalse();
        report.Status.Should().Be(NpcStatus.Draft);
        report.Issues.Should().ContainSingle(i => i.Code == "STAT_BUDGET_EXCEEDED")
            .Which.Message.Should().Be("O NPC ultrapassa o orçamento de STATs em 1 ponto.");
    }

    [Fact]
    public void Stat_above_category_maximum_is_blocking()
    {
        var sheet = WithStat(WithStat(ValidMook(), "REF", 7), "COOL", 3);

        var issue = Validate(sheet).Issues.Should().ContainSingle(i => i.Key == "STAT_ABOVE_MAX:REF").Subject;
        issue.Severity.Should().Be(NpcIssueSeverity.Error);
        issue.Source.Should().Contain("DGD p.152");
    }

    [Fact]
    public void Stat_below_two_is_blocking_except_emp()
    {
        var low = WithStat(ValidMook(), "TECH", 1);
        var lowEmp = WithStat(ValidMook(), "EMP", 0);

        Validate(low).Issues.Should().Contain(i => i.Key == "STAT_BELOW_MIN:TECH");
        Validate(lowEmp).Issues.Should().NotContain(i => i.Code == "STAT_BELOW_MIN");
    }

    [Fact]
    public void Skill_above_maximum_level_is_blocking()
    {
        var sheet = WithSkill(ValidMook(), "handgun", 11);

        Validate(sheet).Issues.Should().ContainSingle(i => i.Key == "SKILL_ABOVE_MAX:handgun")
            .Which.Message.Should().Contain("ultrapassa o nível máximo de 10");
    }

    [Fact]
    public void Free_skill_levels_are_not_charged_and_x2_skills_cost_double()
    {
        var baseline = Validate(ValidMook()).Skills.Used;

        // Athletics 2 is free (DGD p.151); Autofire is (x2) in CP:R.
        Validate(WithSkill(ValidMook(), "athletics", 2)).Skills.Used.Should().Be(baseline);
        var withAutofire = Validate(WithSkill(ValidMook(), "autofire", 2));
        withAutofire.Skills.Used.Should().Be(baseline + 4);
        withAutofire.Issues.Should().ContainSingle(i => i.Code == "SKILL_BUDGET_EXCEEDED")
            .Which.Message.Should().Contain("em 4 pontos");
    }

    [Fact]
    public void Native_language_and_home_get_their_free_levels()
    {
        var report = Validate(ValidMook() with { NativeLanguage = "Japanese", Home = "Watson" });

        report.SkillLines.Should().Contain(l => l.Key == "language:Japanese" && l.Level == 4 && l.Cost == 0);
        report.SkillLines.Should().Contain(l => l.Key == "local-expert:Watson" && l.Level == 2 && l.Cost == 0);
        report.SkillLines.Should().Contain(l => l.Key == "language:Streetslang" && l.Level == 2 && l.Cost == 0);
    }

    [Fact]
    public void Unknown_skill_or_missing_specialization_is_a_hard_error()
    {
        var report = Validate(WithSkill(WithSkill(ValidMook(), "hacking", 3), "language", 3));

        report.Issues.Should().Contain(i => i.Code == "SKILL_UNKNOWN" && !i.Overridable);
        report.Issues.Should().Contain(i => i.Code == "SKILL_SPECIALIZATION_REQUIRED" && !i.Overridable);
    }

    [Fact]
    public void Key_skill_base_outside_the_suggested_range_is_only_a_warning()
    {
        var report = Validate(WithSkill(ValidMook(), "handgun", 2)); // REF 6 + 2 = 8 < 10

        report.Issues.Should().ContainSingle(i => i.Key == "KEY_SKILL_BASE_OUT_OF_RANGE:handgun")
            .Which.Severity.Should().Be(NpcIssueSeverity.Warning);
        report.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Gear_budget_uses_the_quality_price_table()
    {
        var poor = Validate(ValidMook());
        poor.GearLines.Single(l => l.ItemId == HeavyPistol.Id).UnitPrice.Should().Be(50, "Heavy Pistol de 100eb custa 50eb em Poor Quality (CP:R p.342)");

        var over = ValidMook() with { Gear = [.. ValidMook().Gear, new NpcGearEntry { ItemId = AssaultRifle.Id }] };
        var report = Validate(over);
        report.Gear.Used.Should().Be(720);
        report.Issues.Should().ContainSingle(i => i.Code == "GEAR_BUDGET_EXCEEDED")
            .Which.Message.Should().Contain("em 420eb");
    }

    [Fact]
    public void Quality_change_without_official_price_is_rejected()
    {
        var exotic = ValidMook() with { Gear = [new NpcGearEntry { ItemId = StunGun.Id, Quality = WeaponQuality.Poor }] };

        Validate(exotic).Issues.Should().Contain(i => i.Code == "WEAPON_QUALITY_UNAVAILABLE" && !i.Overridable);
    }

    [Fact]
    public void Cyberware_budget_is_enforced()
    {
        var sheet = ValidMook() with
        {
            Cyberware = [.. ValidMook().Cyberware, new NpcCyberwareEntry { InstanceId = "x", ItemId = HiddenHolster.Id }],
        };

        Validate(sheet).Issues.Should().ContainSingle(i => i.Code == "CYBER_BUDGET_EXCEEDED")
            .Which.Message.Should().Contain("ultrapassa o orçamento de 500eb em 500eb");
    }

    [Fact]
    public void Option_without_its_foundational_piece_is_a_hard_error()
    {
        var sheet = ValidMook() with { Cyberware = [new NpcCyberwareEntry { InstanceId = "k", ItemId = Kerenzikov.Id }] };

        Validate(sheet).Issues.Should().Contain(i => i.Code == "CYBER_PARENT_MISSING" && !i.Overridable);
    }

    [Fact]
    public void Option_slots_of_the_foundational_piece_are_enforced()
    {
        var sheet = ValidMook() with
        {
            Level = NpcLevel.Boss,
            Cyberware =
            [
                new NpcCyberwareEntry { InstanceId = "eye", ItemId = Cybereye.Id },
                new NpcCyberwareEntry { InstanceId = "a", ItemId = TargetingScope.Id, ParentInstanceId = "eye" },
                new NpcCyberwareEntry { InstanceId = "b", ItemId = Teleoptics.Id, ParentInstanceId = "eye" },
                new NpcCyberwareEntry { InstanceId = "c", ItemId = Microoptics.Id, ParentInstanceId = "eye" },
                new NpcCyberwareEntry { InstanceId = "d", ItemId = TargetingScope.Id, ParentInstanceId = "eye" },
            ],
        };

        var report = Validate(sheet);
        report.Issues.Should().Contain(i => i.Code == "CYBER_SLOTS_EXCEEDED" && i.Message.Contains("3 Option Slots"));
        report.Issues.Should().Contain(i => i.Code == "CYBER_DUPLICATE");
    }

    [Fact]
    public void Paired_option_needs_one_unit_in_each_of_two_eyes()
    {
        var single = ValidMook() with
        {
            Level = NpcLevel.Boss,
            Cyberware =
            [
                new NpcCyberwareEntry { InstanceId = "e1", ItemId = Cybereye.Id },
                new NpcCyberwareEntry { InstanceId = "ll1", ItemId = LowLight.Id, ParentInstanceId = "e1" },
            ],
        };
        var paired = single with
        {
            Cyberware =
            [
                .. single.Cyberware,
                new NpcCyberwareEntry { InstanceId = "e2", ItemId = Cybereye.Id },
                new NpcCyberwareEntry { InstanceId = "ll2", ItemId = LowLight.Id, ParentInstanceId = "e2" },
            ],
        };

        Validate(single).Issues.Should().Contain(i => i.Code == "CYBER_PAIR_MISSING");
        Validate(paired).Issues.Should().NotContain(i => i.Code.StartsWith("CYBER_PAIR"));
    }

    [Fact]
    public void Incompatible_implants_are_reported()
    {
        var sheet = ValidMook() with
        {
            Level = NpcLevel.Boss,
            Cyberware =
            [
                new NpcCyberwareEntry { InstanceId = "nl", ItemId = NeuralLink.Id },
                new NpcCyberwareEntry { InstanceId = "k", ItemId = Kerenzikov.Id, ParentInstanceId = "nl" },
                new NpcCyberwareEntry { InstanceId = "s", ItemId = Sandevistan.Id, ParentInstanceId = "nl" },
                new NpcCyberwareEntry { InstanceId = "a1", ItemId = CyberaudioSuite.Id },
                new NpcCyberwareEntry { InstanceId = "a2", ItemId = CyberaudioSuite.Id },
            ],
        };

        var issues = Validate(sheet).Issues;
        issues.Should().Contain(i => i.Code == "CYBER_INCOMPATIBLE" && i.Message.StartsWith("A combinação de implantes selecionada possui incompatibilidades"));
        issues.Should().Contain(i => i.Code == "CYBER_CAPACITY_EXCEEDED" && i.Message.Contains("Cyberaudio"));
    }

    [Fact]
    public void Chipware_socket_uses_the_core_rule_over_catalog_data()
    {
        var sheet = ValidMook() with
        {
            Level = NpcLevel.Boss,
            Cyberware =
            [
                new NpcCyberwareEntry { InstanceId = "nl", ItemId = NeuralLink.Id },
                new NpcCyberwareEntry { InstanceId = "sock", ItemId = ChipwareSocket.Id, ParentInstanceId = "nl" },
                new NpcCyberwareEntry { InstanceId = "pe", ItemId = PainEditor.Id, ParentInstanceId = "sock" },
            ],
        };

        var report = Validate(sheet);
        report.CyberLines.Single(l => l.InstanceId == "sock").Should().BeEquivalentTo(new { SlotsUsed = 1, SlotsProvided = 1, SlotsFree = 0 });
        report.CyberLines.Single(l => l.InstanceId == "nl").SlotsFree.Should().Be(4);
        report.Issues.Should().NotContain(i => i.Code == "CYBER_SLOTS_EXCEEDED");
        report.Issues.Should().Contain(i => i.Code == "CYBER_SLOTS_ASSUMED" && i.Message.Contains("Pain Editor"));
    }

    [Fact]
    public void Free_text_prerequisites_are_flagged_for_manual_review()
    {
        var sheet = ValidMook() with
        {
            Level = NpcLevel.Boss,
            Cyberware = [new NpcCyberwareEntry { InstanceId = "d", ItemId = DragoonPlating.Id }],
        };

        Validate(sheet).Issues.Should().Contain(i => i.Code == "CYBER_REQUIREMENT_TEXT" && i.Message.Contains("BODY 16 ou Full Body Conversion"));
    }

    [Fact]
    public void Forbidden_and_required_choices_are_checked()
    {
        var sheet = ValidMook() with
        {
            CyberwareRules = [new(Chemskin.Id, CyberwareRuleState.Forbidden), new(NeuralLink.Id, CyberwareRuleState.Required)],
        };

        var issues = Validate(sheet).Issues;
        issues.Should().Contain(i => i.Code == "CYBER_FORBIDDEN" && !i.Overridable);
        issues.Should().Contain(i => i.Code == "CYBER_REQUIRED_MISSING" && !i.Overridable);
    }

    [Fact]
    public void Weapon_minimum_body_is_checked()
    {
        var sheet = ValidMook() with { Gear = [new NpcGearEntry { ItemId = VeryHeavyMelee.Id }] };

        Validate(sheet).Issues.Should().Contain(i => i.Code == "WEAPON_MIN_BODY" && i.Message.Contains("não atende aos requisitos cadastrados"));
    }

    [Fact]
    public void Implanted_linear_frame_raises_body_for_hp_and_death_save()
    {
        var sheet = ValidMook() with
        {
            Level = NpcLevel.Boss,
            Cyberware = [new NpcCyberwareEntry { InstanceId = "f", ItemId = ImplantedFrame.Id }],
        };

        var d = Validate(sheet).Derived;
        d.BodyForDerived.Should().Be(12);
        d.Hp.Should().Be(10 + 5 * 8); // ceil((12 + 4) / 2) = 8
        d.DeathSave.Should().Be(12);
    }

    [Fact]
    public void Two_armors_on_the_same_location_conflict()
    {
        var sheet = ValidMook() with
        {
            Gear = [new NpcGearEntry { ItemId = Kevlar.Id, Worn = ArmorSlot.Body }, new NpcGearEntry { ItemId = Leathers.Id, Worn = ArmorSlot.Body }],
        };

        Validate(sheet).Issues.Should().Contain(i => i.Code == "ARMOR_SLOT_CONFLICT");
    }

    [Fact]
    public void Item_in_the_wrong_section_is_rejected()
    {
        var sheet = ValidMook() with { Gear = [new NpcGearEntry { ItemId = Cybereye.Id }] };

        Validate(sheet).Issues.Should().Contain(i => i.Code == "ITEM_WRONG_LIST" && !i.Overridable);
    }

    [Fact]
    public void Gm_exception_accepts_an_overridable_error_and_marks_the_status()
    {
        var sheet = WithStat(WithStat(ValidMook(), "INT", 5), "TECH", 3) with
        {
            Exceptions = [new NpcRuleException("STAT_BUDGET_EXCEEDED", "Chefe de gangue veterano, mais esperto que o normal.")],
        };

        var report = Validate(sheet);

        report.IsValid.Should().BeTrue();
        report.Status.Should().Be(NpcStatus.ValidWithExceptions);
        report.Issues.Single(i => i.Code == "STAT_BUDGET_EXCEEDED").Should().BeEquivalentTo(new { Overridden = true });
    }

    [Fact]
    public void Exceptions_never_cover_structural_errors_or_lack_justification()
    {
        var sheet = ValidMook() with
        {
            Cyberware = [new NpcCyberwareEntry { InstanceId = "k", ItemId = Kerenzikov.Id }],
            Exceptions = [new NpcRuleException("CYBER_PARENT_MISSING:k", "Quero assim.")],
        };
        var noReason = WithStat(ValidMook(), "REF", 7) with { Exceptions = [new NpcRuleException("STAT_ABOVE_MAX:REF", "  ")] };

        Validate(sheet).IsValid.Should().BeFalse();
        Validate(noReason).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Lieutenant_cannot_carry_excellent_weapons_without_exception()
    {
        var sheet = ValidMook() with { Level = NpcLevel.Lieutenant, Gear = [new NpcGearEntry { ItemId = ExcellentPistol.Id }] };

        Validate(sheet).Issues.Should().Contain(i => i.Code == "WEAPON_EXCELLENT_NOT_ALLOWED" && i.Overridable);
    }

    [Fact]
    public void Mook_with_standard_weapon_gets_a_guideline_warning_only()
    {
        var sheet = ValidMook() with { Gear = [new NpcGearEntry { ItemId = HeavyPistol.Id }] };

        var report = Validate(sheet);
        report.Issues.Should().Contain(i => i.Code == "WEAPON_QUALITY_GUIDELINE" && i.Severity == NpcIssueSeverity.Warning);
    }

    [Theory]
    [InlineData(NpcLevel.Mook, false, 33, 6, 30, 300, 500)]
    [InlineData(NpcLevel.Mook, true, 39, 7, 30, 500, 1000)]
    [InlineData(NpcLevel.Lieutenant, false, 44, 8, 60, 700, 1000)]
    [InlineData(NpcLevel.Lieutenant, true, 48, 8, 60, 1000, 1500)]
    [InlineData(NpcLevel.MiniBoss, false, 52, 8, 90, 1200, 2000)]
    [InlineData(NpcLevel.MiniBoss, true, 54, 8, 90, 2000, 3200)]
    [InlineData(NpcLevel.Boss, false, 57, 8, 120, 3300, 10000)]
    [InlineData(NpcLevel.Boss, true, 62, 8, 120, 8300, null)]
    public void Every_category_uses_its_own_parameters(NpcLevel level, bool hardened, int stats, int maxStat, int skills, int gear, int? cyber)
    {
        var report = Validate(ValidMook() with { Level = level, Hardened = hardened });

        report.Stats.Total.Should().Be(stats);
        report.Template.MaxStat.Should().Be(maxStat);
        report.Skills.Total.Should().Be(skills);
        report.Gear.Total.Should().Be(gear);
        report.Cyberware.Total.Should().Be(cyber);
    }

    [Fact]
    public void Same_stats_are_valid_for_a_lieutenant_but_not_for_a_mook()
    {
        var sheet = ValidMook() with
        {
            Stats = Stats(("INT", 5), ("REF", 7), ("DEX", 6), ("TECH", 3), ("COOL", 5), ("WILL", 5), ("MOVE", 4), ("BODY", 5), ("EMP", 3)),
        };

        Validate(sheet).Issues.Should().Contain(i => i.Code == "STAT_BUDGET_EXCEEDED");
        Validate(sheet with { Level = NpcLevel.Lieutenant }).Issues.Should().NotContain(i => i.Severity == NpcIssueSeverity.Error && i.Code.StartsWith("STAT_"));
    }
}
