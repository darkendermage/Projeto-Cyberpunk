using System.Text.RegularExpressions;
using CyberpunkDb.Application.Npcs.Compendium;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Domain.Enums;
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
    public void Sheets_print_the_core_stats()
    {
        // EMP and LUCK may be absent or null; every other stat must be printed.
        string[] core = ["INT", "REF", "DEX", "TECH", "COOL", "WILL", "MOVE", "BODY"];
        foreach (var npc in Compendium.All.Where(n => n.Sheet != null))
            foreach (var key in core)
            {
                npc.Sheet!.Stats.Should().ContainKey(key, npc.Id);
                npc.Sheet.Stats[key].Should().NotBeNull($"{npc.Id} {key}");
            }
    }

    [Fact]
    public void Names_have_no_markup() =>
        Compendium.All.Should().OnlyContain(n => !n.Name.Contains('*'));

    [Fact]
    public void Mission_Kit_pages_exist() =>
        Compendium.All.SelectMany(n => n.Sources)
            .Where(s => s.Book.StartsWith("Edgerunners Mission Kit:"))
            .Should().OnlyContain(s => s.Page <= 40);

    [Fact]
    public void Armored_skills_are_printed_skills()
    {
        foreach (var sheet in Compendium.All.Select(n => n.Sheet).OfType<CompendiumSheet>())
            foreach (var (name, armored) in sheet.ArmoredSkills)
            {
                sheet.Skills.Should().ContainKey(name);
                armored.Should().BeLessThanOrEqualTo(sheet.Skills[name], name);
            }
    }

    [Fact]
    public void Weapon_damage_rolls_in_the_combat_engine()
    {
        foreach (var weapon in Compendium.All.Select(n => n.Sheet).OfType<CompendiumSheet>().SelectMany(s => s.Weapons))
        {
            weapon.Damage.Should().MatchRegex(@"^\d+d\d+$", weapon.Name);
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
    public void Automatic_weapons_have_the_multiplier_of_their_class()
    {
        // CP:R p.171 and p.173: Autofire 3 for SMGs and Heavy SMGs, 4 for Assault Rifles.
        var expected = new Dictionary<WeaponClass, int> { [WeaponClass.Smg] = 3, [WeaponClass.HeavySmg] = 3, [WeaponClass.AssaultRifle] = 4 };
        var weapons = Compendium.All.Select(n => n.Sheet).OfType<CompendiumSheet>().SelectMany(s => s.Weapons)
            .Where(w => w.Class is { } c && expected.ContainsKey(c)).ToList();
        weapons.Should().NotBeEmpty();
        foreach (var weapon in weapons)
            weapon.AutofireMultiplier.Should().Be(expected[weapon.Class!.Value], weapon.Name);
    }

    [Fact]
    public void Autofire_only_weapons_are_flagged_and_have_a_multiplier()
    {
        var weapons = Compendium.All.Select(n => n.Sheet).OfType<CompendiumSheet>().SelectMany(s => s.Weapons).ToList();
        foreach (var weapon in weapons.Where(w => w.AutofireOnly == true))
            weapon.AutofireMultiplier.Should().NotBeNull(weapon.Name);
        foreach (var weapon in weapons.Where(w => w.Notes is { } n && Regex.IsMatch(n, @"Autofire.*\bonly\b")))
            weapon.AutofireOnly.Should().BeTrue(weapon.Name);
    }

    [Fact]
    public void Printed_melee_sp_is_half_or_all_of_the_sp()
    {
        // The combat snapshot only knows these two cases: it halves SP, or a flag keeps it whole.
        foreach (var armor in Compendium.All.Select(n => n.Sheet).OfType<CompendiumSheet>().SelectMany(s => s.Armor).Where(a => a.SpVsMelee != null))
            armor.SpVsMelee.Should().BeOneOf([(armor.Sp + 1) / 2, armor.Sp], armor.Name);
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
    public void Printed_range_tables_have_one_value_per_band()
    {
        foreach (var weapon in Compendium.All.Select(n => n.Sheet).OfType<CompendiumSheet>().SelectMany(s => s.Weapons).Where(w => w.RangeDv != null))
            weapon.RangeDv.Should().HaveCount(8, weapon.Name);
    }

    [Fact]
    public void Skills_map_to_the_rule_book()
    {
        // Printed skills the rule book has no entry for.
        // "Medical Tech" and "Surgery": printed on Danger Gal Dossier Medtech sheets; the rule book lists only Paramedic.
        // "Melee Weapons": printed (plural) on Interface RED Volume 2 sheets; the rule book skill is Melee Weapon.
        string[] knownExceptions = ["Medical Tech", "Surgery", "Melee Weapons"];
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
