using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Npcs.NpcTestData;

namespace CyberpunkDb.Tests.Unit.Npcs;

public class NpcRuleBookTests
{
    [Fact]
    public void Mook_template_matches_the_danger_gal_dossier()
    {
        var t = Rules.Template(NpcLevel.Mook, hardened: false);

        t.StatPoints.Value.Should().Be(33);
        t.MaxStat.Value.Should().Be(6);
        t.SkillPoints.Value.Should().Be(30);
        t.MaxSkill.Value.Should().Be(10);
        t.SuggestedHp.Value.Should().Be(new IntRange(20, 35));
        t.SuggestedSp.Value.Should().Be(new IntRange(4, 7));
        t.CombatSkillBase.Value.Should().Be(new IntRange(10, 12));
        t.NonCombatSkillBase.Value.Should().Be(new IntRange(8, 10));
        t.GearBudget.Value.Should().Be(300);
        t.CyberwareBudget.Value.Should().Be(500);
        t.StatPoints.Source.Should().Contain("DGD p.152");
    }

    [Fact]
    public void Hardening_adds_to_the_base_template()
    {
        var t = Rules.Template(NpcLevel.Mook, hardened: true);

        t.StatPoints.Value.Should().Be(39);
        t.MaxStat.Value.Should().Be(7);
        t.GearBudget.Value.Should().Be(500);
        t.CyberwareBudget.Value.Should().Be(1000);
        t.StatPoints.Source.Should().Contain("Hardened");
        Rules.Template(NpcLevel.Boss, hardened: true).CyberwareBudget.Unlimited.Should().BeTrue();
    }

    [Fact]
    public void Every_rule_value_is_traceable_to_a_book_or_marked_complementary()
    {
        var missing = new List<string>();
        Walk(Rules.Document, "rules", missing, depth: 0);

        missing.Should().BeEmpty("todo parâmetro precisa de fonte ou da marca de regra complementar");
    }

    [Fact]
    public void Skill_list_has_the_66_core_skills_with_x2_marks()
    {
        Rules.Skills.Should().HaveCount(66);
        Rules.Skills.Where(s => s.DoubleCost).Select(s => s.Id).Should().BeEquivalentTo(
            ["pilot-air-vehicle", "martial-arts", "autofire", "heavy-weapons", "demolitions", "electronics-security-tech", "paramedic"]);
        Rules.Skills.Should().OnlyContain(s => Rules.StatKeys.Contains(s.Stat));
    }

    [Fact]
    public void Profiles_reference_existing_skills_and_catalog_slugs()
    {
        var seed = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seed", "01-compendium.json")))!.AsArray();
        var slugs = seed.Select(n => n!["slug"]!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var p in Rules.Profiles)
        {
            p.KeyCombatSkills.Concat(p.KeyNonCombatSkills).Concat(p.SecondarySkills.Keys)
                .Should().OnlyContain(s => Rules.FindSkill(s) != null, p.Id);
            p.Cyberware.Keys.Concat(p.Gear.Keys).Concat(p.SignatureGear).Concat(p.SignatureCyberware)
                .Where(s => !slugs.Contains(s)).Should().BeEmpty($"o perfil {p.Id} só pode citar itens do catálogo");
            p.StatWeights.Keys.Should().OnlyContain(k => Rules.StatKeys.Contains(k));
        }
        Rules.Document.Cyberware.Overrides.Select(o => o.Slug).Concat(Rules.Document.Armor.CyberwareStoppingPower.Select(c => c.Slug))
            .Should().OnlyContain(s => slugs.Contains(s));
    }

    private static void Walk(object? node, string path, List<string> missing, int depth)
    {
        if (node is null || depth > 8) return;
        var type = node.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Ruled<>))
        {
            var source = (string?)type.GetProperty(nameof(Ruled<int>.Source))!.GetValue(node);
            var complementary = (bool)type.GetProperty(nameof(Ruled<int>.Complementary))!.GetValue(node)!;
            if (string.IsNullOrWhiteSpace(source) && !complementary) missing.Add(path);
            return;
        }
        if (node is string || type.IsPrimitive || type.IsEnum) return;
        if (node is IDictionary dict)
        {
            foreach (DictionaryEntry e in dict) Walk(e.Value, $"{path}.{e.Key}", missing, depth + 1);
            return;
        }
        if (node is IEnumerable list)
        {
            var i = 0;
            foreach (var item in list) Walk(item, $"{path}[{i++}]", missing, depth + 1);
            return;
        }
        if (!type.Namespace?.StartsWith("CyberpunkDb") ?? true) return;
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0))
            Walk(prop.GetValue(node), $"{path}.{prop.Name}", missing, depth + 1);
    }
}
