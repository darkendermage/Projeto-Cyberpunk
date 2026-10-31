using System.Text.RegularExpressions;
using CyberpunkDb.Application.Npcs.Compendium;
using CyberpunkDb.Application.Npcs.Rules;
using FluentAssertions;

namespace CyberpunkDb.Tests.Compendium;

public class NpcCompendiumDataTests
{
    private static readonly string[] StatKeys = ["INT", "REF", "DEX", "TECH", "COOL", "WILL", "LUCK", "MOVE", "BODY", "EMP"];

    private static readonly NpcCompendium Compendium = NpcCompendium.LoadEmbedded();

    [Fact]
    public void Ids_are_unique_slugs()
    {
        Compendium.All.Should().NotBeEmpty();
        Compendium.All.Should().OnlyContain(n => Regex.IsMatch(n.Id, "^[a-z0-9]+(-[a-z0-9]+)*$"));
        Compendium.All.Select(n => n.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Names_are_unique() =>
        Compendium.All.Select(n => n.Name).Should().OnlyHaveUniqueItems();

    [Fact]
    public void Every_entry_cites_a_printed_page() =>
        Compendium.All.Should().OnlyContain(n =>
            n.Sources.Count > 0 && n.Sources.All(s => s.Page > 0 && !string.IsNullOrWhiteSpace(s.Book)));

    [Fact]
    public void Every_entry_has_both_languages() =>
        Compendium.All.Should().OnlyContain(n =>
            !string.IsNullOrWhiteSpace(n.Summary.Pt) && !string.IsNullOrWhiteSpace(n.Summary.En) && n.Summary.Pt != n.Summary.En);

    [Fact]
    public void Sheet_matches_kind() =>
        Compendium.All.Should().OnlyContain(n => n.Kind == CompendiumKind.Sheet ? n.Sheet != null : n.Sheet == null);

    [Fact]
    public void Sheets_are_usable()
    {
        foreach (var sheet in Compendium.All.Select(n => n.Sheet).OfType<CompendiumSheet>())
        {
            sheet.Hp.Should().BeGreaterThan(0);
            sheet.Stats.Keys.Should().BeSubsetOf(StatKeys);
            sheet.ArmoredStats.Keys.Should().BeSubsetOf(StatKeys);
        }
    }

    [Fact]
    public void Weapon_damage_rolls_in_the_combat_engine()
    {
        foreach (var weapon in Compendium.All.Select(n => n.Sheet).OfType<CompendiumSheet>().SelectMany(s => s.Weapons))
        {
            weapon.Damage.Should().MatchRegex(@"^\d+d\d+([+-]\d+)?$", weapon.Name);
            weapon.Rof.Should().BeGreaterThanOrEqualTo(1, weapon.Name);
        }
    }

    [Fact]
    public void Autofire_multiplier_and_melee_sp_are_sane()
    {
        var sheets = Compendium.All.Select(n => n.Sheet).OfType<CompendiumSheet>().ToList();
        foreach (var weapon in sheets.SelectMany(s => s.Weapons).Where(w => w.AutofireMultiplier != null))
            weapon.AutofireMultiplier.Should().BeGreaterThanOrEqualTo(2, weapon.Name);
        foreach (var armor in sheets.SelectMany(s => s.Armor).Where(a => a.SpVsMelee != null))
            armor.SpVsMelee.Should().BeInRange(0, armor.Sp, armor.Name);
    }

    [Fact]
    public void Find_returns_entries_by_id()
    {
        Compendium.Find("adam-smasher")!.Name.Should().Be("Adam Smasher");
        Compendium.Find("nobody").Should().BeNull();
    }

    [Fact]
    public void Ranged_weapons_have_a_class()
    {
        foreach (var weapon in Compendium.All.Select(n => n.Sheet).OfType<CompendiumSheet>().SelectMany(s => s.Weapons).Where(w => !w.Melee))
            weapon.Class.Should().NotBeNull($"{weapon.Name} needs a range table row to attack");
    }

    [Fact]
    public void Skills_map_to_the_rule_book()
    {
        // Printed skills the rule book has no entry for; none today.
        string[] knownExceptions = [];
        var rules = NpcRuleBook.LoadEmbedded();
        foreach (var sheet in Compendium.All.Select(n => n.Sheet).OfType<CompendiumSheet>())
            foreach (var name in sheet.Skills.Keys.Concat(sheet.Weapons.Select(w => w.Skill)).Except(knownExceptions))
                NpcCompendium.SkillKey(name, rules).Should().NotBeNull(name);
    }

    [Fact]
    public void Skill_keys_follow_the_id_and_specialization_format()
    {
        var rules = NpcRuleBook.LoadEmbedded();
        NpcCompendium.SkillKey("handgun", rules).Should().Be("handgun");
        NpcCompendium.SkillKey("Language (English)", rules).Should().Be("language:English");
        NpcCompendium.SkillKey("Nothing Like This", rules).Should().BeNull();
    }
}
