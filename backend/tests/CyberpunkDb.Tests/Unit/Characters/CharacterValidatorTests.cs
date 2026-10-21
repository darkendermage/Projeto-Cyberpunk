using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

public class CharacterValidatorTests
{
    private static CharacterValidationReport Validate(CharacterSheet sheet) => Validator.Validate(sheet, Catalog);

    private static IEnumerable<string> Errors(CharacterValidationReport r) =>
        r.Issues.Where(i => i.Severity == CharacterIssueSeverity.Error).Select(i => i.Code);

    private static CharacterSheet WithStat(CharacterSheet s, string key, int value) =>
        s with { Stats = new Dictionary<string, int>(s.Stats, StringComparer.OrdinalIgnoreCase) { [key] = value } };

    private static CharacterSheet WithSkill(CharacterSheet s, string key, int value) =>
        s with { Skills = new Dictionary<string, int>(s.Skills, StringComparer.OrdinalIgnoreCase) { [key] = value } };

    // 1–2. The three methods and their limits

    [Fact]
    public void Reference_sheets_of_the_three_methods_are_valid()
    {
        foreach (var sheet in new[] { CompletePackage(), Streetrat(), Edgerunner() })
        {
            var report = Validate(sheet);
            Errors(report).Should().BeEmpty(sheet.Creation.Method.ToString());
            report.Status.Should().Be(CharacterStatus.Creating);
            report.CanFinalize.Should().BeTrue();
        }
    }

    [Fact]
    public void Complete_package_budgets_are_reported()
    {
        var report = Validate(CompletePackage());

        report.Stats!.Used.Should().Be(62);
        report.Stats.Total.Should().Be(62);
        report.Skills!.Used.Should().Be(86, "o idioma de origem não custa pontos e Autofire (x2) custa o dobro");
        report.Gear!.Total.Should().Be(2550);
        report.Fashion!.Total.Should().Be(800);
    }

    [Fact]
    public void Complete_package_rejects_stats_outside_two_to_eight_and_over_budget()
    {
        Errors(Validate(WithStat(CompletePackage(), "REF", 9))).Should().Contain(["STAT_ABOVE_MAX", "STAT_BUDGET_EXCEEDED"]);
        Errors(Validate(WithStat(WithStat(CompletePackage(), "TECH", 1), "REF", 8))).Should().Contain("STAT_BELOW_MIN");

        var under = Validate(WithStat(CompletePackage(), "TECH", 4));
        Errors(under).Should().BeEmpty();
        under.Issues.Should().Contain(i => i.Code == "STAT_BUDGET_UNSPENT");
    }

    [Fact]
    public void Double_cost_skills_count_twice_against_the_skill_budget()
    {
        // Tactics 6 → 5 frees 1 point; Autofire (x2) 6 → 7 would be over max anyway, so raise Heavy Weapons (x2) to 1.
        var sheet = WithSkill(WithSkill(CompletePackage(), "tactics", 5), "heavy-weapons", 1);
        var report = Validate(sheet);

        report.Skills!.Used.Should().Be(87);
        Errors(report).Should().Contain("SKILL_BUDGET_EXCEEDED");
    }

    [Fact]
    public void Creation_caps_skills_at_six_and_basic_skills_at_two_or_more()
    {
        Errors(Validate(WithSkill(CompletePackage(), "handgun", 7))).Should().Contain("SKILL_ABOVE_MAX");
        Errors(Validate(WithSkill(CompletePackage(), "brawling", 1))).Should().Contain("BASIC_SKILL_BELOW_MIN");
    }

    [Fact]
    public void Native_language_needs_four_levels_and_they_are_free()
    {
        Errors(Validate(WithSkill(CompletePackage(), "language:English", 2))).Should().Contain("NATIVE_LANGUAGE_LEVEL");
        Errors(Validate(CompletePackage() with { Identity = CompletePackage().Identity with { NativeLanguage = null } }))
            .Should().Contain("NATIVE_LANGUAGE_REQUIRED");
    }

    // 3. Templates and Role rules

    [Fact]
    public void Streetrat_stats_and_skills_must_match_the_template()
    {
        Errors(Validate(WithStat(Streetrat(), "REF", 8))).Should().Contain("STAT_TEMPLATE_MISMATCH");
        Errors(Validate(WithSkill(Streetrat(), "handgun", 5))).Should().Contain("SKILL_TEMPLATE_MISMATCH");
        Errors(Validate(WithSkill(Streetrat(), "accounting", 2))).Should().Contain("SKILL_NOT_IN_TEMPLATE");
        Errors(Validate(Streetrat() with { Creation = Streetrat().Creation with { TemplateRow = null } })).Should().Contain("TEMPLATE_ROW_REQUIRED");
    }

    [Fact]
    public void Streetrat_and_edgerunner_need_one_choice_per_starting_alternative()
    {
        var sheet = Streetrat() with { Creation = Streetrat().Creation with { StartingChoices = new(StringComparer.OrdinalIgnoreCase) } };
        Validate(sheet).Issues.Count(i => i.Code == "STARTING_CHOICE_REQUIRED").Should().Be(2);
    }

    [Fact]
    public void Edgerunner_stats_must_match_each_roll_and_skills_stay_in_the_role_list()
    {
        Errors(Validate(WithStat(Edgerunner(), "INT", 7))).Should().Contain("STAT_ROLL_MISMATCH");
        Errors(Validate(WithSkill(WithSkill(Edgerunner(), "tactics", 2), "accounting", 2))).Should().Contain("SKILL_NOT_IN_ROLE");
        Errors(Validate(WithSkill(WithSkill(Edgerunner(), "tactics", 1), "handgun", 6))).Should().Contain("SKILL_BELOW_MIN");
    }

    [Fact]
    public void Roles_start_at_rank_four_and_cannot_multiclass_during_creation()
    {
        var sheet = CompletePackage();
        Errors(Validate(sheet with { Roles = [new CharacterRole { Role = "Solo", Rank = 5 }] })).Should().Contain("ROLE_START_RANK");
        Errors(Validate(sheet with { Roles = [.. sheet.Roles, new CharacterRole { Role = "Nomad", Rank = 1 }] })).Should().Contain("MULTICLASS_AT_CREATION");
        Errors(Validate(sheet with { Roles = [] })).Should().Contain("ROLE_REQUIRED");
    }

    // 4. Creation money budgets

    [Fact]
    public void Fashion_budget_pays_only_for_fashion_and_fashionware()
    {
        var sheet = CompletePackage() with
        {
            Inventory =
            [
                new InventoryEntry { InstanceId = "t", ItemId = LeisureTop.Id, PaidFrom = BudgetPool.CreationFashion, PricePaid = 20 },
                new InventoryEntry { InstanceId = "a", ItemId = Agent.Id, PaidFrom = BudgetPool.CreationFashion, PricePaid = 100 },
            ],
            Cyberware = [new CharacterCyberware { InstanceId = "b", ItemId = Biomonitor.Id, PaidFrom = BudgetPool.CreationFashion, PricePaid = 100 }],
        };

        var report = Validate(sheet);
        report.Issues.Where(i => i.Code == "FASHION_BUDGET_MISUSE").Should().ContainSingle().Which.Message.Should().Contain("Agent");
        report.Fashion!.Used.Should().Be(220);
    }

    [Fact]
    public void General_budget_cannot_be_exceeded()
    {
        var sheet = CompletePackage() with
        {
            Cyberware = [new CharacterCyberware { InstanceId = "f", ItemId = DragoonPlating.Id, PaidFrom = BudgetPool.CreationGear, PricePaid = 5000 }],
        };
        Errors(Validate(sheet)).Should().Contain("GEAR_BUDGET_EXCEEDED");
    }

    [Fact]
    public void Campaign_overrides_replace_limits_and_are_flagged()
    {
        var sheet = WithStat(CompletePackage(), "REF", 8) with
        {
            Creation = CompletePackage().Creation with { Overrides = new CampaignOverrides { StatPoints = 70 } },
        };

        var report = Validate(sheet);
        Errors(report).Should().BeEmpty();
        report.Stats!.Total.Should().Be(70);
        report.Stats.CampaignVariant.Should().BeTrue();
        report.Stats.Source.Should().Contain("Variante de campanha");
    }

    // Status after creation

    [Fact]
    public void Finalized_characters_skip_creation_limits_but_keep_structural_checks()
    {
        var sheet = WithSkill(CompletePackage(), "handgun", 8) with
        {
            Creation = CompletePackage().Creation with { Finalized = true },
        };
        var report = Validate(sheet);
        report.Status.Should().Be(CharacterStatus.Complete);
        report.CanFinalize.Should().BeFalse();

        var broken = Validate(sheet with { Wallet = new Wallet { Balance = -10 } });
        broken.Status.Should().Be(CharacterStatus.NeedsAttention);
        Errors(broken).Should().Contain("BALANCE_NEGATIVE");
    }
}
