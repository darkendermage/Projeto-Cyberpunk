using System.Text.Json.Nodes;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Tests.Unit.Npcs;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

public class CharacterRuleBookTests
{
    private static readonly string[] AllRoles = ["Rockerboy", "Solo", "Netrunner", "Tech", "Medtech", "Media", "Exec", "Lawman", "Fixer", "Nomad"];

    [Fact]
    public void Every_rule_value_is_traceable_to_a_book_or_marked_complementary()
    {
        var missing = new List<string>();
        NpcRuleBookTests.Walk(Rules.Document, "rules", missing, depth: 0);

        missing.Should().BeEmpty("todo parâmetro precisa de fonte ou da marca de regra complementar");
    }

    [Fact]
    public void Complete_package_parameters_match_the_core_rulebook()
    {
        var cp = Rules.Document.Creation.Methods.CompletePackage;

        cp.StatPoints.Value.Should().Be(62);
        cp.StatMin.Value.Should().Be(2);
        cp.StatMax.Value.Should().Be(8);
        cp.SkillPoints.Value.Should().Be(86);
        cp.SkillMax.Value.Should().Be(6);
        cp.GearBudget.Value.Should().Be(2550);
        cp.FashionBudget.Value.Should().Be(800);
        cp.UnspentFashionKept.Value.Should().BeFalse();
        cp.StatPresets.Value.Select(p => p.Points).Should().Equal(50, 62, 70, 75, 80);
        cp.StatPoints.Source.Should().Contain("p.78");
    }

    [Fact]
    public void Edgerunner_and_streetrat_parameters_match_the_core_rulebook()
    {
        var m = Rules.Document.Creation.Methods;

        m.Edgerunner.SkillPoints.Value.Should().Be(86);
        m.Edgerunner.SkillMin.Value.Should().Be(2);
        m.Edgerunner.SkillMax.Value.Should().Be(6);
        m.Edgerunner.RoleSkillsOnly.Value.Should().BeTrue();
        m.Edgerunner.StartingCash.Value.Should().Be(500);
        m.Streetrat.StartingCash.Value.Should().Be(500);
        Rules.Document.Creation.RoleAbilityStartRank.Value.Should().Be(4);
        Rules.Document.Creation.OriginLanguageLevel.Value.Should().Be(4);
        Rules.Document.Creation.BasicSkills.Value.Should().HaveCount(13);
    }

    [Fact]
    public void Every_role_has_a_ten_by_ten_stat_table_and_twenty_known_skills()
    {
        Rules.Roles.Select(r => r.Id).Should().BeEquivalentTo(AllRoles);
        Rules.StatOrder.Should().Equal("INT", "REF", "DEX", "TECH", "COOL", "WILL", "LUCK", "MOVE", "BODY", "EMP");

        foreach (var role in Rules.Roles)
        {
            role.StatTable.Rows.Should().HaveCount(10, role.Id);
            role.StatTable.Rows.Should().OnlyContain(r => r.Length == 10 && r.All(v => v >= 2 && v <= 8), role.Id);
            role.Skills.Levels.Should().HaveCount(20, role.Id);
            role.Skills.Levels.Keys.Where(k => Rules.FindSkill(SkillKey.Parse(k).Id) is null).Should().BeEmpty(role.Id);
            var roleKeys = Rules.RoleSkillLevels(role.Id, "Watson");
            Rules.BasicSkillKeys("Watson").Where(k => !roleKeys.ContainsKey(k)).Should().BeEmpty(role.Id);
            role.Ability.Name.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void Solo_template_row_six_matches_the_book_example()
    {
        // CP:R p.73: Jay rolls a 6 on the Solo table.
        Rules.Role("Solo").StatTable.Rows[5].Should().Equal(7, 7, 6, 5, 7, 6, 6, 7, 7, 5);
        Rules.Role("Solo").Ability.Name.Should().Be("Combat Awareness");
        Rules.Role("Solo").Ability.Page.Should().Be(146);
    }

    [Fact]
    public void Starting_packages_reference_catalog_items_and_match_the_preset_humanity_loss()
    {
        var seed = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seed", "01-compendium.json")))!.AsArray();
        var bySlug = seed.ToDictionary(n => n!["slug"]!.GetValue<string>(), n => n!, StringComparer.OrdinalIgnoreCase);

        foreach (var role in Rules.Roles)
        {
            var sections = new[] { role.Starting.Weapons, role.Starting.Outfit, role.Starting.Cyberware };
            sections.SelectMany(s => s.Groups).SelectMany(g => g).SelectMany(o => o)
                .Where(i => !bySlug.ContainsKey(i.Slug)).Select(i => i.Slug)
                .Should().BeEmpty($"o pacote inicial de {role.Id} só pode citar itens do catálogo");

            // CP:R p.117 lists one Humanity Loss per Role: every combination of alternatives must reach it.
            foreach (var combo in Combinations(role.Starting.Cyberware.Groups))
            {
                var hl = combo.Sum(i => bySlug[i.Slug]["attributes"]?["humanityLoss"]?.GetValue<int>() ?? 0);
                hl.Should().Be(role.Starting.Cyberware.HumanityLoss, $"HL inicial de {role.Id}");
            }
        }
    }

    [Fact]
    public void Meat_arm_options_and_lifestyle_tables_are_loaded()
    {
        Rules.IsMeatArmOption("wolvers").Should().BeTrue();
        Rules.IsMeatArmOption("popup-melee-weapon").Should().BeFalse();
        Rules.FindLifestyle("kibble")!.Monthly.Should().Be(100);
        Rules.FindHousing("cargo-container")!.Rent.Should().Be(1000);
        Rules.StartingHousingFor("Exec").Housing.Should().Be("corporate-conapt");
        Rules.StartingHousingFor("Solo").FirstMonthFree.Should().BeTrue();
    }

    [Fact]
    public void Improvement_point_costs_follow_the_core_tables()
    {
        Rules.SkillImprovementCost(Rules.FindSkill("handgun")!, 5).Should().Be(100);
        Rules.SkillImprovementCost(Rules.FindSkill("autofire")!, 5).Should().Be(200);
        Rules.RoleImprovementCost(5).Should().Be(300);
    }

    [Fact]
    public void Critical_injury_tables_have_eleven_rows_each()
    {
        Rules.Document.Combat.CriticalInjuries.Body.Select(i => i.Roll).Should().Equal(Enumerable.Range(2, 11));
        Rules.Document.Combat.CriticalInjuries.Head.Select(i => i.Roll).Should().Equal(Enumerable.Range(2, 11));
        Rules.FindCriticalInjury(InjuryLocation.Body, "Collapsed Lung")!.DeathSave.Should().BeTrue();
        Rules.FindCriticalInjury(InjuryLocation.Body, "Broken Ribs")!.DeathSave.Should().BeFalse();
    }

    [Fact]
    public void Skill_keys_resolve_home_and_player_choices()
    {
        CharacterRuleBook.ResolveKey("local-expert:@home", "Kabuki").Should().Be("local-expert:Kabuki");
        CharacterRuleBook.ResolveKey("science:@choose", "Kabuki").Should().Be("science:@choose");
        CharacterRuleBook.ResolveKey("science:@choose", "Kabuki", new Dictionary<string, string> { ["science"] = "Chemistry" })
            .Should().Be("science:Chemistry");
        Rules.RoleChoiceSkills("Rockerboy").Should().Equal("play-instrument");
        Rules.Skills.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.NamePt));
    }

    [Fact]
    public void History_entries_cite_only_the_page()
    {
        CharacterRuleBook.Page("CP:R p.105 (if you aren't an Exec, you'll start off living in a rented Cargo Container)").Should().Be("CP:R p.105");
        CharacterRuleBook.Page("CP:R p.74").Should().Be("CP:R p.74");
        CharacterRuleBook.Page(null).Should().BeEmpty();
    }

    private static IEnumerable<List<StartingItem>> Combinations(StartingItem[][][] groups)
    {
        IEnumerable<List<StartingItem>> acc = [[]];
        foreach (var group in groups)
            acc = acc.SelectMany(prefix => group.Select(option => prefix.Concat(option).ToList()));
        return acc;
    }
}
