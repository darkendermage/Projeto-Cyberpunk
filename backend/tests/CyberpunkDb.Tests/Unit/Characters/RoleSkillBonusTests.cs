using System.Text.Json;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Common;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

// Role Abilities that add to Skills: Moto (CP:R p.161), Field Expertise (p.147) and Threat Detection (p.146).
public class RoleSkillBonusTests
{
    private static readonly ProgressionOperations Progression = new(Rules);

    private static CharacterReport Calc(CharacterSheet sheet) => Calculator.Calculate(sheet.Normalize(), Catalog);

    private static CharacterSheet As(string role, int rank, Dictionary<string, int>? split = null) =>
        CompletePackage() with { Roles = [new CharacterRole { Role = role, Rank = rank, Split = split ?? new() }] };

    private static CharacterSkillLine Skill(CharacterReport r, string id) => r.Skills.First(s => s.SkillId == id && s.Specialization is null);

    [Fact]
    public void Nomad_rank_4_adds_4_to_drive_land_vehicle()
    {
        var plain = Skill(Calc(As("Solo", 4)), "drive-land-vehicle");
        var nomad = Skill(Calc(As("Nomad", 4)), "drive-land-vehicle");

        nomad.Total.Should().Be(plain.Total + 4);
        nomad.ModifierParts.Should().ContainSingle(p => p.Label == "Moto (Rank 4)" && p.Value == 4);
        Skill(Calc(As("Nomad", 4)), "air-vehicle-tech").ModifierParts.Should().Contain(p => p.Label == "Moto (Rank 4)");
    }

    [Fact]
    public void Tech_field_2_adds_2_to_weaponstech()
    {
        var plain = Skill(Calc(As("Tech", 2)), "weaponstech");
        var field = Skill(Calc(As("Tech", 2, new() { ["field"] = 2, ["upgrade"] = 2 })), "weaponstech");

        field.Total.Should().Be(plain.Total + 2);
        field.ModifierParts.Should().ContainSingle(p => p.Label == "Field Expertise 2");
        plain.ModifierParts.Should().NotContain(p => p.Label.StartsWith("Field Expertise"));
    }

    [Fact]
    public void Solo_threat_detection_adds_to_perception()
    {
        var plain = Skill(Calc(As("Solo", 4)), "perception");
        var aware = Skill(Calc(As("Solo", 4, new() { ["threatDetection"] = 1, ["precisionAttack"] = 3 })), "perception");

        aware.Total.Should().Be(plain.Total + 1);
        aware.ModifierParts.Should().Contain(p => p.Label == "Threat Detection 1");
    }

    [Fact]
    public void Tech_split_over_twice_rank_is_refused()
    {
        var sheet = As("Tech", 2);
        var act = () => Progression.SetRoleSplit(sheet, new RoleSplitRequest(0, "Tech", new() { ["field"] = 2, ["upgrade"] = 2, ["fabrication"] = 1 }),
            new CharacterChange(sheet, Catalog, DateTime.UtcNow));

        act.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Tech_specialty_over_rank_is_refused()
    {
        var sheet = As("Tech", 2);
        var act = () => Progression.SetRoleSplit(sheet, new RoleSplitRequest(0, "Tech", new() { ["field"] = 3 }),
            new CharacterChange(sheet, Catalog, DateTime.UtcNow));

        act.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Solo_fumble_recovery_must_be_0_or_4()
    {
        var sheet = As("Solo", 6);
        var change = new CharacterChange(sheet, Catalog, DateTime.UtcNow);

        var bad = () => Progression.SetRoleSplit(sheet, new RoleSplitRequest(0, "Solo", new() { ["fumbleRecovery"] = 2 }), change);
        bad.Should().Throw<RequestValidationException>();

        var ok = Progression.SetRoleSplit(sheet, new RoleSplitRequest(0, "Solo", new() { ["fumbleRecovery"] = 4, ["precisionAttack"] = 2 }), change);
        ok.Roles[0].Split.Should().Contain("fumbleRecovery", 4);
    }

    [Fact]
    public void Solo_split_over_rank_is_refused()
    {
        var sheet = As("Solo", 4);
        var act = () => Progression.SetRoleSplit(sheet, new RoleSplitRequest(0, "Solo", new() { ["precisionAttack"] = 3, ["spotWeakness"] = 2 }),
            new CharacterChange(sheet, Catalog, DateTime.UtcNow));

        act.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Old_sheet_json_loads()
    {
        // A sheet saved before this round: its roles have no "split".
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(As("Tech", 3), web))!;
        foreach (var role in node["roles"]!.AsArray()) role!.AsObject().Remove("split");
        var sheet = node.Deserialize<CharacterSheet>(web)!.Normalize();

        sheet.Roles[0].Split.Should().BeEmpty();
        Calc(sheet).Skills.Should().NotContain(s => s.ModifierParts.Any(p => p.Label.StartsWith("Field Expertise")));
    }
}
