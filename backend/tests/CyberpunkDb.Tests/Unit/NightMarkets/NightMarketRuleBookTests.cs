using System.Text.RegularExpressions;
using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Tests.Unit.Npcs;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.NightMarkets.MarketTestData;

namespace CyberpunkDb.Tests.Unit.NightMarkets;

public class NightMarketRuleBookTests
{
    [Fact]
    public void Every_rule_value_is_traceable_to_a_book_or_marked_complementary()
    {
        var missing = new List<string>();
        NpcRuleBookTests.Walk(Rules.Document, "rules", missing, 0);
        missing.Should().BeEmpty();
        Rules.Presets.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Source));
        Rules.Presets.SelectMany(p => p.Pools).Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Source));
    }

    [Fact]
    public void Price_ladder_has_the_eight_categories_of_the_core()
    {
        Rules.Ladder.Select(t => t.Price).Should().Equal(10, 20, 50, 100, 500, 1000, 5000, 10000);
        Rules.Ladder.Select(t => t.Category).Should().Equal(Enum.GetValues<PriceCategory>());
        Rules.CategoryOf(550).Should().Be(PriceCategory.Expensive);
        Rules.CategoryOf(16000).Should().Be(PriceCategory.SuperLuxury);
    }

    [Fact]
    public void Generator_has_six_kinds_of_goods_with_twenty_d100_rows_each()
    {
        Rules.Goods.Select(g => g.Roll).Should().Equal(1, 2, 3, 4, 5, 6);
        Rules.Goods.Should().OnlyContain(g => g.Table.Length == 20);
        Rules.Goods.SelectMany(g => g.Table).Should().OnlyContain(e =>
            (e.Slugs != null ? 1 : 0) + (e.Local != null ? 1 : 0) + (e.Query != null && !e.GmChoice ? 1 : 0) + (e.GmChoice ? 1 : 0) == 1);
    }

    [Fact]
    public void Operator_reach_and_deals_follow_the_core()
    {
        Rules.Reach(1).Should().Be(PriceCategory.Everyday);
        Rules.Reach(4).Should().Be(PriceCategory.Expensive);
        Rules.Reach(8).Should().Be(PriceCategory.VeryExpensive);
        Rules.Reach(9).Should().Be(PriceCategory.Luxury);
        Rules.Reach(10).Should().Be(PriceCategory.SuperLuxury);
        Rules.Reach(0).Should().BeNull();
        Rules.Document.Operator.Deals.Value.Select(d => d.MinRank).Should().Equal(1, 3, 5, 7, 9, 10);
    }

    [Fact]
    public void Mystery_box_table_is_complete()
    {
        var boxes = Rules.Document.MysteryBoxes.Boxes.Value;
        boxes.Select(b => b.Roll).Should().Equal(Enumerable.Range(1, 10));
        boxes.Should().OnlyContain(b => b.Contents.Select(c => c.Roll).SequenceEqual(new int?[] { 1, 2, 3, 4, 5, 6 }));
    }

    [Fact]
    public void Every_slug_in_the_rules_exists_in_the_catalog()
    {
        var slugs = Rules.Goods.SelectMany(g => g.Table).SelectMany(e => e.Slugs ?? [])
            .Concat(Rules.Goods.SelectMany(g => g.Related).SelectMany(q => q.Slugs ?? []))
            .Concat(Rules.Presets.SelectMany(p => p.Pools).SelectMany(p => p.Query.Slugs ?? []))
            .Concat(Rules.Document.MysteryBoxes.Boxes.Value.SelectMany(b => b.Contents).SelectMany(c => new[] { c.Slug, c.ExtraSlug }))
            .Concat(Rules.Document.DoorPrize.Slices.Value.Append(Rules.Document.DoorPrize.Example.Value).Concat(Rules.Document.DoorPrize.Bonus.Value)
                .Select(s => s.Slug))
            .OfType<string>();

        slugs.Should().OnlyContain(s => Catalog.Find(s) != null);
    }

    [Fact]
    public void Black_chrome_booths_point_to_catalog_items_of_the_six_markets()
    {
        var booth = new Regex("^[A-F][0-9]$");
        Rules.Booths.Should().NotBeEmpty();
        Rules.Booths.Keys.Should().OnlyContain(slug => Catalog.Find(slug) != null);
        Rules.Booths.Values.SelectMany(c => c).Should().OnlyContain(c => booth.IsMatch(c));
        // Every listing of the book except the Apps has a booth (BC p.7).
        Catalog.Items.Where(i => i.Book == "Black Chrome" && !i.Slug.StartsWith("app-"))
            .Should().OnlyContain(i => Rules.Booths.ContainsKey(i.Slug));
        Rules.Booths["the-dirk-combat-jacket"].Should().Equal("E1", "F3");
        Rules.Booths["neo-soviet-cyberarm"].Should().Equal("B6", "C3");
    }

    [Fact]
    public void Night_market_index_rows_are_well_formed()
    {
        Rules.Index.Should().HaveCountGreaterThan(1000);
        Rules.Index.Select(e => e.Section).Distinct().Should().BeEquivalentTo(
            "weapons", "armor", "gear", "cyberware", "fashion", "vehicles", "net", "lifestyle", "entertainment", "elflines");
        Rules.Index.Where(e => e.Slug != null).Should().OnlyContain(e => Catalog.Find(e.Slug) != null);
        Rules.Index.Should().OnlyContain(e => e.Name.Length > 0 && e.Sources.Length > 0);
        Rules.Index.Count(e => e.Slug != null).Should().BeGreaterThan(900);
        Rules.Index.First(e => e.Name == "Medium Pistol").Should().Match<IndexEntry>(e => e.PriceEb == 50 && e.PriceCategory == PriceCategory.Costly);
    }

    [Fact]
    public void Presets_cover_the_black_chrome_markets_and_the_interface_vendors()
    {
        Rules.Presets.Where(p => p.IsBlackChrome).Select(p => p.Letter).Should().Equal("A", "B", "C", "D", "E", "F");
        Rules.Preset("bc-c").Features.Should().Contain(MarketFeature.DoorPrize);
        Rules.Preset("mr-amaze").Features.Should().Contain(MarketFeature.MysteryBoxes);
        Rules.Preset("bc-d").Kind.Should().Be(MarketKind.MidnightMarket);
        Rules.Preset("toggle").Admission!.Price.Should().Be(20);
        Rules.Presets.SelectMany(p => p.SuggestedGoods.Concat(p.Stalls.SelectMany(s => s.Goods)))
            .Should().OnlyContain(g => Rules.Goods.Any(x => x.Id == g));
    }
}
