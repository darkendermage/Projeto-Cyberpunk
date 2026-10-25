using CyberpunkDb.Application.NetArchitectures.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Tests.Unit.Npcs;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.NetArchitectures.NetTestData;

namespace CyberpunkDb.Tests.Unit.NetArchitectures;

public class NetArchitectureRuleBookTests
{
    [Fact]
    public void Every_rule_value_is_traceable_to_a_book_or_marked_complementary()
    {
        var missing = new List<string>();
        NpcRuleBookTests.Walk(Rules.Document, "rules", missing, depth: 0);

        missing.Should().BeEmpty("todo parâmetro precisa de fonte ou da marca de regra complementar");
    }

    [Fact]
    public void Difficulty_table_matches_the_core_book()
    {
        Rules.Difficulties.Select(d => (d.Id, d.Dv, d.FightingChance)).Should().Equal(
            (NetDifficulty.Basic, 6, 2), (NetDifficulty.Standard, 8, 4), (NetDifficulty.Uncommon, 10, 6), (NetDifficulty.Advanced, 12, 8));
    }

    [Fact]
    public void Lobby_is_a_d6_and_every_body_table_covers_3d6()
    {
        Rules.Document.LobbyTable.Value.Select(r => r.Roll).Should().Equal(1, 2, 3, 4, 5, 6);
        foreach (var d in Enum.GetValues<NetDifficulty>())
            Rules.Table(d.ToString()).Select(r => r.Roll).Should().Equal(Enumerable.Range(3, 16), d.ToString());
    }

    [Fact]
    public void Body_table_dvs_follow_the_difficulty_and_rows_9_to_12_are_the_features()
    {
        foreach (var d in Rules.Difficulties)
        {
            var rows = Rules.Table(d.Id.ToString());
            rows.SelectMany(r => r.Contents).Where(c => c.Dv is not null).Should().OnlyContain(c => c.Dv == d.Dv, d.Label);
            rows.Where(r => r.Roll is >= 9 and <= 12).Select(r => r.Contents.Single().Kind)
                .Should().Equal(NetContentKind.Password, NetContentKind.File, NetContentKind.ControlNode, NetContentKind.Password);
            rows.Where(r => r.Roll is < 9 or > 12).Should().OnlyContain(r => r.Contents.All(c => c.Kind == NetContentKind.BlackIce) && r.Contents.Length <= 3);
        }
    }

    [Fact]
    public void Every_black_ice_in_the_tables_is_in_the_catalog()
    {
        var slugs = Rules.Document.LobbyTable.Value.Concat(Rules.Document.BodyTables.Value.SelectMany(t => t.Rows))
            .SelectMany(r => r.Contents).Where(c => c.Kind == NetContentKind.BlackIce).Select(c => c.Slug).Distinct().ToList();

        slugs.Should().HaveCount(12);
        slugs.Where(s => Ice.Find(s) is null).Should().BeEmpty();
    }

    [Fact]
    public void Demons_match_the_core_book()
    {
        Rules.Demons.Select(d => (d.Name, d.Rez, d.Interface, d.NetActions, d.CombatNumber, d.Cost)).Should().Equal(
            ("Imp", 15, 3, 2, 14, 1000), ("Efreet", 25, 4, 3, 14, 5000), ("Balron", 30, 7, 4, 14, 10000));
    }

    [Fact]
    public void Defense_prices_follow_the_dv_to_counter()
    {
        Rules.Defenses.Should().HaveCount(18);
        Rules.Defenses.Where(d => Rules.DefenseCost(d.CounterDv) is null).Should().BeEmpty();
        Rules.Defenses.Where(d => d.LoadoutPicks > d.LoadoutOptions.Length).Should().BeEmpty();
        Rules.Defenses.Where(d => d.RequiresDemon).Select(d => d.Group).Should().OnlyContain(g => g == DefenseGroup.Active);
        Rules.DefenseCost(Rules.FindDefense("automated-turret")!.CounterDv).Should().Be(5000, "CP:R p.218: a torreta do exemplo custa 5.000eb");
    }

    [Fact]
    public void Locations_only_reference_existing_defenses_and_devices()
    {
        foreach (var l in Rules.Locations)
        {
            l.Defenses.Keys.Where(k => Rules.FindDefense(k) is null).Should().BeEmpty(l.Id);
            l.Devices.Keys.Where(k => Rules.FindDevice(k) is null).Should().BeEmpty(l.Id);
            l.FileIdeas.Should().NotBeEmpty(l.Id);
        }
    }

    [Theory]
    [InlineData(3, 1, true, 2)]
    [InlineData(6, 1, true, 2)]
    [InlineData(7, 2, false, 3)]
    [InlineData(12, 2, false, 3)]
    [InlineData(13, 3, false, null)]
    [InlineData(18, 3, false, null)]
    public void Purchase_bands_and_demon_limit(int floors, int demons, bool portable, int? nodes)
    {
        Rules.MaxDemons(floors).Should().Be(demons);
        Rules.Band(floors).Portable.Should().Be(portable);
        Rules.Band(floors).MaxControlNodes.Should().Be(nodes);
    }
}
