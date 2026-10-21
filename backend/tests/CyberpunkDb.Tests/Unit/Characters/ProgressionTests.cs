using System.Text.Json;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Characters.Roles;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Application.Common;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

public class ProgressionTests
{
    private static readonly ProgressionOperations Progression = new(Rules);

    private static CharacterSheet Veteran(int ip) => CompletePackage() with
    {
        Creation = CompletePackage().Creation with { Finalized = true },
        Progress = new ProgressState { Ip = ip, IpEarned = ip },
    };

    private static CharacterChange Change(CharacterSheet s) => new(s, Catalog, DateTime.UtcNow);

    [Fact]
    public void Awarding_ip_is_logged_with_its_reason()
    {
        var change = Change(Veteran(0));
        var sheet = Progression.AwardIp(Veteran(0), new AwardIpRequest(0, 120, "Sessão 3: resgate em Kabuki"), change);
        sheet.Progress.Ip.Should().Be(120);
        sheet.Progress.IpEarned.Should().Be(120);
        change.Events.Should().ContainSingle(e => e.Action == "award-ip" && e.IsManual);

        var act = () => Progression.AwardIp(Veteran(10), new AwardIpRequest(0, -20, "correção"), Change(Veteran(10)));
        act.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Skills_cost_twenty_per_next_level_and_forty_when_marked_x2()
    {
        // Handgun 6 → 7 costs 140; Autofire (x2) 6 → 7 costs 280 (CP:R p.410).
        var raised = Progression.ImproveSkill(Veteran(500), new ImproveSkillRequest(0, "handgun"), Change(Veteran(500)));
        raised.Skills["handgun"].Should().Be(7);
        raised.Progress.Ip.Should().Be(360);

        var autofire = Progression.ImproveSkill(Veteran(500), new ImproveSkillRequest(0, "autofire"), Change(Veteran(500)));
        autofire.Progress.Ip.Should().Be(220);

        var newSkill = Progression.ImproveSkill(Veteran(500), new ImproveSkillRequest(0, "language:Japanese"), Change(Veteran(500)));
        newSkill.Skills["language:Japanese"].Should().Be(1);
        newSkill.Progress.Ip.Should().Be(480);

        var poor = () => Progression.ImproveSkill(Veteran(100), new ImproveSkillRequest(0, "handgun"), Change(Veteran(100)));
        poor.Should().Throw<RequestValidationException>();
        var noSpec = () => Progression.ImproveSkill(Veteran(500), new ImproveSkillRequest(0, "language"), Change(Veteran(500)));
        noSpec.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Role_ranks_cost_sixty_per_next_rank()
    {
        var sheet = Progression.ImproveRole(Veteran(400), new ImproveRoleRequest(0, "Solo"), Change(Veteran(400)));
        sheet.Roles.Single().Rank.Should().Be(5);
        sheet.Roles.Single().IpInvested.Should().Be(300);
        sheet.Progress.Ip.Should().Be(100);
    }

    [Fact]
    public void Multiclassing_needs_rank_four_and_makes_the_new_role_current()
    {
        var sheet = Progression.AddRole(Veteran(100), new AddRoleRequest(0, "Nomad"), Change(Veteran(100)));
        sheet.Roles.Select(r => (r.Role, r.Rank)).Should().Equal(("Nomad", 1), ("Solo", 4));
        sheet.Progress.Ip.Should().Be(40);
        Validator.Validate(sheet, Catalog).Status.Should().Be(Domain.Enums.CharacterStatus.Complete);

        var tooEarly = () => Progression.AddRole(sheet with { Progress = new ProgressState { Ip = 500 } }, new AddRoleRequest(0, "Fixer"), Change(sheet));
        tooEarly.Should().Throw<RequestValidationException>("Nomad ainda está no Rank 1");
        var twice = () => Progression.AddRole(Veteran(500), new AddRoleRequest(0, "Solo"), Change(Veteran(500)));
        twice.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Multiclassing_keeps_the_previous_role_lifepath()
    {
        var veteran = Veteran(100) with
        {
            RoleLifepath = new(StringComparer.OrdinalIgnoreCase) { ["Solo"] = new() { ["type"] = "Bodyguard" } },
        };
        var sheet = Progression.AddRole(veteran, new AddRoleRequest(0, "Nomad"), Change(veteran));
        sheet.RoleLifepath["Solo"]["type"].Should().Be("Bodyguard");
        sheet.RoleLifepath.Should().NotContainKey("Nomad", "o da Role nova é opcional");
        Rules.Document.Progression.RoleLifepathPerRole.Complementary.Should().BeTrue("o Core não define Lifepath no multiclasse");
    }

    [Fact]
    public void Improvement_points_are_only_spent_after_creation()
    {
        var creating = CompletePackage() with { Progress = new ProgressState { Ip = 500 } };
        var act = () => Progression.ImproveSkill(creating, new ImproveSkillRequest(0, "handgun"), Change(creating));
        act.Should().Throw<RequestValidationException>();
    }

    // ------------------------------------------------------------ Role modules (11.3)

    private sealed class MediaModule : IRoleModule
    {
        public string Role => "Solo";
        public IEnumerable<CharacterIssue> Validate(CharacterSheet sheet, JsonElement data) =>
            data.TryGetProperty("credibility", out var c) && c.GetInt32() > 10
                ? [new CharacterIssue("TEST_MODULE", CharacterIssueSeverity.Warning, "Credibilidade alta demais.", null, null, "roleData")]
                : [];
    }

    [Fact]
    public void Role_modules_validate_their_own_data_and_nothing_else()
    {
        var validator = new CharacterValidator(Rules, new RoleModuleRegistry([new MediaModule()]));
        var sheet = Veteran(0) with
        {
            RoleData = new(StringComparer.OrdinalIgnoreCase) { ["Solo"] = JsonDocument.Parse("""{ "credibility": 12 }""").RootElement },
        };

        validator.Validate(sheet, Catalog).Issues.Should().ContainSingle(i => i.Code == "TEST_MODULE");
        validator.Validate(Veteran(0), Catalog).Issues.Should().NotContain(i => i.Code == "TEST_MODULE");
        Validator.Validate(sheet, Catalog).Issues.Should().NotContain(i => i.Code == "TEST_MODULE", "sem módulo registrado nada muda");
    }
}
