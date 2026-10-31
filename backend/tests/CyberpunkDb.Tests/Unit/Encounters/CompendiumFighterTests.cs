using System.Text.Json;
using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Npcs.Compendium;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Tests.Unit.Characters;
using CyberpunkDb.Tests.Unit.Npcs;
using FluentAssertions;

namespace CyberpunkDb.Tests.Unit.Encounters;

public class CompendiumFighterTests
{
    private static readonly FighterViews Views = new(CharacterTestData.Rules, CharacterTestData.Calculator, NpcTestData.Rules, CombatRuleBook.LoadEmbedded());

    [Fact]
    public void Incomplete_sheet_still_fights()
    {
        var npc = new CompendiumNpc
        {
            Id = "bare", Name = "Bare", Kind = CompendiumKind.Sheet, Sources = [new CompendiumSource("Book", 9)],
            Sheet = new CompendiumSheet { Hp = 20, Stats = new() { ["BODY"] = 6, ["REF"] = 7, ["EMP"] = null } },
        };

        var snapshot = Views.Compendium(npc);

        snapshot.MaxSpBody.Should().Be(0);
        snapshot.MaxSpHead.Should().Be(0);
        snapshot.Weapons.Should().BeEmpty();
        snapshot.Skills.Should().BeEmpty();
        snapshot.Stats.Should().NotContainKey("EMP");
        snapshot.Source.Should().Be("Book p.9");
        snapshot.Body.Should().Be(6);
        snapshot.Stats.GetValueOrDefault("DEX").Should().Be(0);

        var fighter = Views.ForNpc(new Combatant { Id = "A", Name = "Bare", Hp = 20, Snapshot = snapshot });
        fighter.Evasion.Should().Be(0);
        fighter.SpBody.Should().Be(0);
    }

    [Fact]
    public void Armored_skill_wins_over_the_plain_one()
    {
        var npc = new CompendiumNpc
        {
            Id = "armored", Name = "Armored", Kind = CompendiumKind.Sheet, Sources = [new CompendiumSource("Book", 9)],
            Sheet = new CompendiumSheet
            {
                Hp = 40, Stats = new() { ["BODY"] = 6, ["REF"] = 8 },
                Skills = new() { ["Evasion"] = 14, ["Handgun"] = 14 },
                ArmoredSkills = new() { ["Evasion"] = 12 },
            },
        };

        var snapshot = Views.Compendium(npc);

        snapshot.Skills["evasion"].Should().Be(12);
        snapshot.Skills["handgun"].Should().Be(14);
    }

    [Fact]
    public void A_sheet_without_the_Autofire_skill_rolls_REF_alone()
    {
        // Ripper prints an Assault Rifle but no Autofire Skill Base.
        var npc = NpcCompendium.LoadEmbedded().Find("ripper")!;
        npc.Sheet!.Skills.Should().NotContainKey("Autofire");

        var fighter = Views.ForNpc(new Combatant { Id = "A", Name = npc.Name, Hp = npc.Sheet.Hp, Snapshot = Views.Compendium(npc) });

        var rifle = fighter.Weapons.Single(w => w.Class == WeaponClass.AssaultRifle);
        rifle.AutofireMultiplier.Should().Be(4);
        rifle.AutofireTotal.Should().Be(npc.Sheet.Stats["REF"]);
    }

    [Fact]
    public void A_saved_snapshot_without_the_new_fields_fights_as_before()
    {
        const string saved = """
            { "maxHp": 40, "body": 6, "maxSpBody": 11, "maxSpHead": 11, "weapons": [
              { "key": "w0", "name": "Brawling Attack", "skill": "brawling", "damage": "2d6", "rof": 2, "melee": true, "attackBase": 10 },
              { "key": "w1", "name": "Pistol", "skill": "handgun", "damage": "3d6", "rof": 2, "attackBase": 10 } ] }
            """;
        var snapshot = JsonSerializer.Deserialize<CombatSnapshot>(saved, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var fighter = Views.ForNpc(new Combatant { Id = "A", Name = "Old", Hp = 40, SpBody = 11, SpHead = 11, Snapshot = snapshot });

        fighter.Weapon("w0")!.HalvesArmor.Should().BeTrue("old snapshots follow Melee");
        fighter.Weapon("w1")!.HalvesArmor.Should().BeFalse();
        fighter.Weapon("w1")!.AutofireOnly.Should().BeNull();
        new NpcCombat(CharacterTestData.Rules, CharacterTestData.Calculator)
            .Damage(new Combatant { Id = "A", Name = "Old", Hp = 40, SpBody = 11, SpHead = 11, Snapshot = snapshot }, 20, InjuryLocation.Body, true, false, true)
            .Sp.Should().Be(6);
    }

    [Theory]
    [InlineData(InjuryLocation.Body)]
    [InlineData(InjuryLocation.Head)]
    public void Melee_flag_keeps_the_location_hit_whole(InjuryLocation location)
    {
        var snapshot = Views.Compendium(NpcCompendium.LoadEmbedded().Find("adam-smasher")!);
        snapshot.FullSpVsMeleeBody.Should().BeTrue();
        snapshot.FullSpVsMeleeHead.Should().BeTrue();

        var outcome = new NpcCombat(CharacterTestData.Rules, CharacterTestData.Calculator)
            .Damage(new Combatant { Id = "A", Name = "Adam Smasher", Hp = 75, SpBody = 18, SpHead = 18, Snapshot = snapshot }, 25, location, true, false, true);

        outcome.Sp.Should().Be(18);
    }

    [Fact]
    public void Halved_printed_rows_set_no_flag()
    {
        var snapshot = Views.Compendium(NpcCompendium.LoadEmbedded().Find("falco")!);
        snapshot.FullSpVsMeleeBody.Should().BeNull();
        snapshot.FullSpVsMeleeHead.Should().BeNull();
    }
}
