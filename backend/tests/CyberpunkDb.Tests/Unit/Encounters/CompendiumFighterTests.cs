using CyberpunkDb.Application.Encounters;
using CyberpunkDb.Application.Encounters.Combat;
using CyberpunkDb.Application.Encounters.Rules;
using CyberpunkDb.Application.Npcs.Compendium;
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
}
