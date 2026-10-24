using System.Text.Json;
using System.Text.Json.Nodes;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Generation;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Npcs.NpcTestData;

namespace CyberpunkDb.Tests.Unit.Npcs;

public class NpcGangTests
{
    private static NpcGenerationResult Generate(NpcGenerationRequest request) => Generator.Generate(request, Catalog);

    private static NpcGang Gang(string id) => Rules.FindGang(id) ?? throw new InvalidOperationException(id);

    [Fact]
    public void Gangs_cover_both_eras_with_sources()
    {
        Rules.Gangs.Select(g => g.Era).Distinct().Should().BeEquivalentTo(["2045", "2077"]);
        Rules.Gangs.Select(g => g.Id).Should().OnlyHaveUniqueItems();
        foreach (var g in Rules.Gangs)
        {
            g.Description.Source.Should().NotBeNullOrWhiteSpace(g.Id);
            g.Aesthetic.Source.Should().NotBeNullOrWhiteSpace(g.Id);
            g.Bias.Complementary.Should().BeTrue($"o viés de {g.Id} é interpretação da aplicação");
            g.Loadout.Where(l => string.IsNullOrWhiteSpace(l.Source)).Should().BeEmpty(g.Id);
        }
    }

    [Fact]
    public void Gangs_reference_existing_profiles_skills_homes_languages_and_catalog_slugs()
    {
        var seed = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seed", "01-compendium.json")))!.AsArray();
        var slugs = seed.Select(n => n!["slug"]!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var g = Rules.Document.Generator;

        foreach (var gang in Rules.Gangs)
        {
            var bias = gang.Bias.Value;
            bias.Profiles.Keys.Where(p => Rules.FindProfile(p) is null).Should().BeEmpty(gang.Id);
            bias.Profiles.Should().NotBeEmpty(gang.Id);
            bias.SecondarySkills.Keys.Where(s => Rules.FindSkill(SkillKey.Parse(s).Id) is null).Should().BeEmpty(gang.Id);
            bias.StatWeights.Keys.Where(k => !Rules.StatKeys.Contains(k)).Should().BeEmpty(gang.Id);
            bias.Cyberware.Keys.Concat(bias.Gear.Keys).Concat(bias.ForbiddenCyberware)
                .Where(s => !slugs.Contains(s)).Should().BeEmpty($"a gangue {gang.Id} só pode citar itens do catálogo");
            gang.Territory.Value.Where(h => !g.Homes.Contains(h)).Should().BeEmpty(gang.Id);
            gang.Languages.Value.Keys.Where(l => !g.Languages.Contains(l)).Should().BeEmpty(gang.Id);
            gang.Handles.Value.Should().NotBeEmpty(gang.Id);
        }
    }

    [Fact]
    public void Overlay_keeps_the_profile_and_adds_the_gang_weights()
    {
        var melee = Rules.FindProfile("melee-solo")!;
        var result = GangOverlay.Apply(melee, Gang("maelstrom"));

        result.Id.Should().Be("melee-solo");
        result.Role.Should().Be(melee.Role);
        result.KeyCombatSkills.Should().Equal(melee.KeyCombatSkills);
        result.Cyberware["wolvers"].Should().Be(melee.Cyberware["wolvers"] + Gang("maelstrom").Bias.Value.Cyberware["wolvers"]);
        result.StatWeights["BODY"].Should().BeApproximately(melee.StatWeights["BODY"] * 1.3, 1e-9);
        result.ArmorStyle.Should().Be(ArmorStyle.Medium);
    }

    [Fact]
    public void Overlay_puts_favored_weapon_classes_first_only_when_the_skill_fits()
    {
        var gunslinger = Rules.FindProfile("street-gunslinger")!;
        var result = GangOverlay.Apply(gunslinger, Gang("iron-sights"));

        // Iron Sights favor SMGs (Handgun): they lead the primary list; melee secondary is untouched.
        result.Weapons.Primary!.Classes.Take(2).Should().Equal(WeaponClass.Smg, WeaponClass.HeavySmg);
        result.Weapons.Secondary!.Classes.Should().Equal(gunslinger.Weapons.Secondary!.Classes);

        // Prime-Time Players favor shotguns (Shoulder Arms), which a Handgun profile cannot fire.
        GangOverlay.Apply(gunslinger, Gang("prime-time-players")).Weapons.Primary!.Classes
            .Should().Equal(gunslinger.Weapons.Primary!.Classes);
    }

    [Fact]
    public void Inquisitors_never_pick_cyberware_on_their_own()
    {
        foreach (var level in Enum.GetValues<NpcLevel>())
        for (var seed = 1; seed <= 8; seed++)
        {
            var result = Generate(new NpcGenerationRequest { Level = level, Hardened = true, GangId = "inquisitors", Seed = seed });

            result.Success.Should().BeTrue($"{level}/{seed}: {string.Join(" | ", result.Conflicts.Select(c => c.Message))}");
            result.Sheet!.Cyberware.Should().BeEmpty($"{level}/{seed}");
            result.Sheet.GangId.Should().Be("inquisitors");
        }
    }

    [Fact]
    public void Required_implants_win_over_the_gang_taboo()
    {
        var result = Generate(new NpcGenerationRequest
        {
            Level = NpcLevel.Lieutenant, GangId = "inquisitors", Seed = 3,
            CyberwareRules = [new NpcCyberwareRule(NeuralLink.Id, CyberwareRuleState.Required)],
        });

        result.Success.Should().BeTrue();
        result.Sheet!.Cyberware.Select(c => c.ItemId).Should().Equal(NeuralLink.Id);
    }

    [Fact]
    public void Animals_never_get_strength_or_combat_cyberware()
    {
        var banned = Gang("animals").Bias.Value.ForbiddenCyberware.Select(s => Catalog.Find(s)?.Id).OfType<int>().ToHashSet();
        banned.Should().NotBeEmpty();
        for (var seed = 1; seed <= 20; seed++)
        {
            var result = Generate(new NpcGenerationRequest { Level = NpcLevel.MiniBoss, Hardened = true, GangId = "animals", ProfileId = "melee-solo", Seed = seed });

            result.Success.Should().BeTrue();
            result.Sheet!.Cyberware.Select(c => c.ItemId).Should().NotIntersectWith(banned, $"seed {seed}");
        }
    }

    [Fact]
    public void Maelstrom_spends_more_on_cyberware_than_an_unaffiliated_npc()
    {
        int Spent(string? gang) => Enumerable.Range(1, 20).Sum(seed =>
            Generate(new NpcGenerationRequest { Level = NpcLevel.Lieutenant, ProfileId = "melee-solo", GangId = gang, Seed = seed }).Report!.Cyberware.Used);

        Spent("maelstrom").Should().BeGreaterThan(Spent(null));
    }

    [Fact]
    public void Identity_follows_the_gang_turf_language_and_name()
    {
        var gang = Gang("tyger-claws");
        var sheets = Enumerable.Range(1, 30)
            .Select(seed => Generate(new NpcGenerationRequest { Level = NpcLevel.Mook, GangId = gang.Id, Seed = seed }).Sheet!)
            .ToList();

        sheets.Should().OnlyContain(s => gang.Territory.Value.Contains(s.Home));
        sheets.GroupBy(s => s.NativeLanguage).MaxBy(g => g.Count())!.Key.Should().Be("Japanese");
        sheets.Should().OnlyContain(s => s.Concept!.StartsWith("Tyger Claws · "));
        sheets.Should().Contain(s => gang.Handles.Value.Contains(s.Name));
    }

    [Fact]
    public void Random_profile_is_drawn_among_the_ones_that_suit_the_gang()
    {
        var suited = Gang("voodoo-boys-2077").Bias.Value.Profiles.Keys.ToList();
        var picked = Enumerable.Range(1, 30)
            .Select(seed => Generate(new NpcGenerationRequest { Level = NpcLevel.Mook, GangId = "voodoo-boys-2077", Seed = seed }).ProfileId)
            .ToList();

        picked.Should().OnlyContain(p => suited.Contains(p!));
        picked.GroupBy(p => p).MaxBy(g => g.Count())!.Key.Should().Be("netrunner");
    }

    [Fact]
    public void Same_seed_and_gang_give_the_same_sheet()
    {
        var request = new NpcGenerationRequest { Level = NpcLevel.MiniBoss, GangId = "maelstrom-2077", Seed = 1234 };

        JsonSerializer.Serialize(Generate(request).Sheet).Should().Be(JsonSerializer.Serialize(Generate(request).Sheet));
    }

    [Fact]
    public void Unknown_gang_is_rejected_by_the_generator_and_noted_by_the_validator()
    {
        var result = Generate(new NpcGenerationRequest { Level = NpcLevel.Mook, GangId = "jets", Seed = 1 });
        result.Success.Should().BeFalse();
        result.Conflicts.Should().ContainSingle(c => c.Code == "GANG_UNKNOWN");

        var sheet = Generate(new NpcGenerationRequest { Level = NpcLevel.Mook, Seed = 1 }).Sheet! with { GangId = "jets" };
        var report = Validator.Validate(sheet, Catalog);
        report.Issues.Should().Contain(i => i.Code == "GANG_UNKNOWN" && i.Severity == NpcIssueSeverity.Info);
        report.IsValid.Should().BeTrue("a gangue não muda a validade da ficha");
    }
}
