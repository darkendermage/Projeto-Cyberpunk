using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Generation;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Npcs.NpcTestData;

namespace CyberpunkDb.Tests.Unit.Npcs;

public class NpcGeneratorTests
{
    private static NpcGenerationResult Generate(NpcGenerationRequest request) => Generator.Generate(request, Catalog);

    public static TheoryData<NpcLevel, bool> AllTemplates()
    {
        var data = new TheoryData<NpcLevel, bool>();
        foreach (var level in Enum.GetValues<NpcLevel>())
        {
            data.Add(level, false);
            data.Add(level, true);
        }
        return data;
    }

    [Fact]
    public void Generated_mooks_respect_every_limit()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            var result = Generate(new NpcGenerationRequest { Level = NpcLevel.Mook, Seed = seed });

            result.Success.Should().BeTrue($"seed {seed}: {string.Join(" | ", result.Conflicts.Select(c => c.Message))}");
            var report = result.Report!;
            report.IsValid.Should().BeTrue();
            report.Status.Should().Be(NpcStatus.Valid);
            report.Stats.Used.Should().BeLessThanOrEqualTo(33);
            result.Sheet!.Stats.Values.Should().OnlyContain(v => v <= 6);
            report.Skills.Used.Should().BeLessThanOrEqualTo(30);
            result.Sheet.Skills.Values.Should().OnlyContain(v => v <= 10);
            report.Gear.Used.Should().BeLessThanOrEqualTo(300);
            report.Cyberware.Used.Should().BeLessThanOrEqualTo(500);
        }
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Every_template_generates_valid_sheets_with_its_own_parameters(NpcLevel level, bool hardened)
    {
        var template = Rules.Template(level, hardened);
        foreach (var profile in Rules.Profiles)
        {
            var result = Generate(new NpcGenerationRequest { Level = level, Hardened = hardened, ProfileId = profile.Id, Seed = 99 });

            result.Success.Should().BeTrue($"{template.DisplayName}/{profile.Id}: {string.Join(" | ", result.Conflicts.Select(c => c.Message))}");
            result.Report!.Stats.Total.Should().Be(template.StatPoints.Value);
            result.Report.Stats.Used.Should().Be(template.StatPoints.Value, "o gerador gasta todo o orçamento de STAT");
            result.Report.Skills.Total.Should().Be(template.SkillPoints.Value);
            result.Sheet!.Stats.Values.Should().OnlyContain(v => v <= template.MaxStat.Value);
            result.Sheet.Level.Should().Be(level);
            result.Sheet.Hardened.Should().Be(hardened);
        }
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Generated_hp_lands_in_the_suggested_range(NpcLevel level, bool hardened)
    {
        var range = Rules.Template(level, hardened).SuggestedHp.Value;
        for (var seed = 1; seed <= 30; seed++)
        {
            var report = Generate(new NpcGenerationRequest { Level = level, Hardened = hardened, Seed = seed }).Report!;
            report.Derived.Hp.Should().BeInRange(range.Min, range.Max, $"seed {seed}");
        }
    }

    [Fact]
    public void Consecutive_generations_vary()
    {
        var sheets = Enumerable.Range(1, 8)
            .Select(seed => Generate(new NpcGenerationRequest { Level = NpcLevel.Mook, ProfileId = "street-gunslinger", Seed = seed }).Sheet!)
            .ToList();

        sheets.Select(s => string.Join(",", s.Stats.OrderBy(kv => kv.Key).Select(kv => kv.Value))).Distinct().Count().Should().BeGreaterThan(4);
        sheets.Select(s => string.Join(",", s.Gear.Select(g => g.ItemId))).Distinct().Count().Should().BeGreaterThan(1);
    }

    [Fact]
    public void Same_seed_reproduces_the_same_sheet()
    {
        var a = Generate(new NpcGenerationRequest { Level = NpcLevel.Lieutenant, Seed = 1234 });
        var b = Generate(new NpcGenerationRequest { Level = NpcLevel.Lieutenant, Seed = 1234 });

        b.Sheet.Should().BeEquivalentTo(a.Sheet);
    }

    [Fact]
    public void Forbidden_cyberware_is_never_selected()
    {
        for (var seed = 1; seed <= 100; seed++)
        {
            var result = Generate(new NpcGenerationRequest
            {
                Level = NpcLevel.Boss,
                ProfileId = "corporate-enforcer",
                Seed = seed,
                CyberwareRules = [new(NeuralLink.Id, CyberwareRuleState.Forbidden), new(SubdermalArmor.Id, CyberwareRuleState.Forbidden)],
            });

            result.Success.Should().BeTrue();
            var ids = result.Sheet!.Cyberware.Select(c => c.ItemId).ToList();
            ids.Should().NotContain(NeuralLink.Id).And.NotContain(SubdermalArmor.Id);
            ids.Should().NotContain(Kerenzikov.Id, "Kerenzikov depende do Neural Link proibido");
        }
    }

    [Fact]
    public void Required_cyberware_is_installed_with_its_foundation()
    {
        for (var seed = 1; seed <= 20; seed++)
        {
            var result = Generate(new NpcGenerationRequest
            {
                Level = NpcLevel.Lieutenant,
                Seed = seed,
                CyberwareRules = [new(TargetingScope.Id, CyberwareRuleState.Required)],
            });

            result.Success.Should().BeTrue();
            var cyber = result.Sheet!.Cyberware;
            var scope = cyber.Should().ContainSingle(c => c.ItemId == TargetingScope.Id).Subject;
            cyber.Should().Contain(c => c.InstanceId == scope.ParentInstanceId && c.ItemId == Cybereye.Id);
        }
    }

    [Fact]
    public void Required_paired_option_gets_two_eyes()
    {
        var result = Generate(new NpcGenerationRequest
        {
            Level = NpcLevel.MiniBoss,
            Seed = 5,
            CyberwareRules = [new(LowLight.Id, CyberwareRuleState.Required)],
        });

        result.Success.Should().BeTrue();
        var units = result.Sheet!.Cyberware.Where(c => c.ItemId == LowLight.Id).ToList();
        units.Should().HaveCount(2);
        units.Select(u => u.ParentInstanceId).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void Required_cyberware_over_budget_is_reported_before_generating()
    {
        // Pain Editor needs a Chipware Socket, which needs a Neural Link: 2.000eb against the Mook's 500eb.
        var result = Generate(new NpcGenerationRequest
        {
            Level = NpcLevel.Mook,
            Seed = 1,
            CyberwareRules = [new(PainEditor.Id, CyberwareRuleState.Required)],
        });

        result.Success.Should().BeFalse();
        result.Attempts.Should().Be(0);
        result.Conflicts.Should().ContainSingle(c => c.Code == "CYBER_BUDGET_EXCEEDED")
            .Which.Message.Should().StartWith("O cyberware obrigatório excede o orçamento disponível").And.Contain("2.000eb");
    }

    [Fact]
    public void Impossible_required_combinations_are_explained()
    {
        var dependsOnForbidden = Generate(new NpcGenerationRequest
        {
            Level = NpcLevel.Boss,
            Seed = 1,
            CyberwareRules = [new(Kerenzikov.Id, CyberwareRuleState.Required), new(NeuralLink.Id, CyberwareRuleState.Forbidden)],
        });
        var incompatible = Generate(new NpcGenerationRequest
        {
            Level = NpcLevel.Boss,
            Seed = 1,
            CyberwareRules = [new(Kerenzikov.Id, CyberwareRuleState.Required), new(Sandevistan.Id, CyberwareRuleState.Required)],
        });
        var lockedStats = Generate(new NpcGenerationRequest
        {
            Level = NpcLevel.Mook,
            Seed = 1,
            Base = new NpcSheet
            {
                Stats = Stats(("REF", 6), ("DEX", 6), ("BODY", 6), ("WILL", 6), ("COOL", 6)),
                Locks = new NpcLocks { Stats = ["REF", "DEX", "BODY", "WILL", "COOL"] },
            },
        });

        dependsOnForbidden.Success.Should().BeFalse();
        dependsOnForbidden.Conflicts.Should().Contain(c => c.Code == "CYBER_REQUIRED_IMPOSSIBLE" && c.Message.Contains("proibido"));
        incompatible.Success.Should().BeFalse();
        incompatible.Conflicts.Should().Contain(c => c.Message.Contains("incompatibilidades"));
        lockedStats.Success.Should().BeFalse();
        lockedStats.Conflicts.Should().Contain(c => c.Code == "STAT_BUDGET_EXCEEDED" && c.Message.Contains("acima do orçamento de 33 em 3"));
    }

    [Fact]
    public void Semi_automatic_mode_keeps_locked_values()
    {
        var baseSheet = new NpcSheet
        {
            Name = "Cherub",
            Concept = "Policial corrupto",
            Stats = Stats(("REF", 6), ("EMP", 3)),
            Skills = Skills(("handgun", 6), ("interrogation", 4)),
            Gear = [new NpcGearEntry { ItemId = VeryHeavyPistol.Id, Quality = WeaponQuality.Poor, Locked = true }],
            Cyberware = [new NpcCyberwareEntry { InstanceId = "eye", ItemId = Cybereye.Id, Locked = true }],
            Locks = new NpcLocks { Stats = ["REF", "EMP"], Skills = ["handgun", "interrogation"] },
        };

        for (var seed = 1; seed <= 25; seed++)
        {
            var result = Generate(new NpcGenerationRequest { Level = NpcLevel.Mook, Seed = seed, Base = baseSheet });

            result.Success.Should().BeTrue($"seed {seed}: {string.Join(" | ", result.Conflicts.Select(c => c.Message))}");
            var sheet = result.Sheet!;
            sheet.Name.Should().Be("Cherub");
            sheet.Concept.Should().Be("Policial corrupto");
            sheet.Stats["REF"].Should().Be(6);
            sheet.Stats["EMP"].Should().Be(3);
            sheet.Skills["handgun"].Should().Be(6);
            sheet.Skills["interrogation"].Should().Be(4);
            sheet.Gear.Should().Contain(g => g.ItemId == VeryHeavyPistol.Id && g.Quality == WeaponQuality.Poor && g.Locked);
            sheet.Cyberware.Should().Contain(c => c.InstanceId == "eye" && c.ItemId == Cybereye.Id);
            result.Report!.Stats.Used.Should().Be(33, "os pontos restantes são distribuídos nos STATs livres");
        }
    }

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public void Raw_attempts_never_exceed_limits(NpcLevel level, bool hardened)
    {
        var t = Rules.Template(level, hardened);
        foreach (var profile in Rules.Profiles)
        {
            for (var seed = 1; seed <= 25; seed++)
            {
                // Before validation: every intermediate attempt must already respect budgets and maxima.
                var sheet = Generator.BuildAttempt(new NpcGenerationRequest { Level = level, Hardened = hardened }, profile, Catalog, seed);
                var report = Validator.Validate(sheet, Catalog);

                report.Stats.Used.Should().BeLessThanOrEqualTo(t.StatPoints.Value);
                sheet.Stats.Values.Should().OnlyContain(v => v <= t.MaxStat.Value);
                report.Skills.Used.Should().BeLessThanOrEqualTo(t.SkillPoints.Value);
                sheet.Skills.Values.Should().OnlyContain(v => v <= t.MaxSkill.Value);
                report.Gear.Used.Should().BeLessThanOrEqualTo(t.GearBudget.Value);
                if (t.CyberwareBudget.Value is { } cyber) report.Cyberware.Used.Should().BeLessThanOrEqualTo(cyber);
                report.Issues.Should().NotContain(i => i.Severity == NpcIssueSeverity.Error, $"{t.DisplayName}/{profile.Id}/seed {seed}");
            }
        }
    }

    [Fact]
    public void No_valid_combination_returns_the_reasons()
    {
        // An Excellent weapon locked on a Lieutenant passes the pre-check but fails every validation.
        var request = new NpcGenerationRequest
        {
            Level = NpcLevel.Lieutenant,
            Seed = 3,
            Base = new NpcSheet { Gear = [new NpcGearEntry { ItemId = ExcellentPistol.Id, Locked = true }] },
        };

        var result = Generate(request);

        result.Success.Should().BeFalse();
        result.Attempts.Should().Be(Rules.Document.Generator.MaxAttempts.Value);
        result.Conflicts.Select(c => c.Code).Should().Contain(["NO_VALID_COMBINATION", "WEAPON_EXCELLENT_NOT_ALLOWED"]);

        // With a justified GM exception the same request succeeds, flagged as such.
        var withException = Generate(request with
        {
            Base = request.Base! with { Exceptions = [new NpcRuleException("WEAPON_EXCELLENT_NOT_ALLOWED:0", "Arma de família do tenente.")] },
        });
        withException.Success.Should().BeTrue();
        withException.Report!.Status.Should().Be(NpcStatus.ValidWithExceptions);
    }

    [Fact]
    public void Unknown_profile_is_rejected()
    {
        var result = Generate(new NpcGenerationRequest { Level = NpcLevel.Mook, ProfileId = "dragon" });

        result.Success.Should().BeFalse();
        result.Conflicts.Should().ContainSingle(c => c.Code == "PROFILE_UNKNOWN");
    }

    [Fact]
    public void Emp_guideline_is_applied_only_when_requested()
    {
        var off = Generate(new NpcGenerationRequest { Level = NpcLevel.Boss, ProfileId = "corporate-enforcer", Seed = 8 });
        var on = Generate(new NpcGenerationRequest { Level = NpcLevel.Boss, ProfileId = "corporate-enforcer", Seed = 8, ApplyEmpGuideline = true });

        var pieces = on.Sheet!.Cyberware.Count;
        on.Sheet.Stats["EMP"].Should().Be(Math.Max(0, off.Sheet!.Stats["EMP"] - pieces / 5));
    }
}
