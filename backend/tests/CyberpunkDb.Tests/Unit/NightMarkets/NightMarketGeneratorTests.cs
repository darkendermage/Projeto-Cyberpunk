using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.NightMarkets;
using CyberpunkDb.Application.NightMarkets.Rules;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.NightMarkets.MarketTestData;

namespace CyberpunkDb.Tests.Unit.NightMarkets;

public class NightMarketGeneratorTests
{
    [Fact]
    public void Same_seed_gives_the_same_market()
    {
        var a = Generate(new GenerateMarketRequest { Seed = 42 });
        var b = Generate(new GenerateMarketRequest { Seed = 42 });

        a.Listings.Select(l => (l.Name, l.Stall, l.BasePrice)).Should().Equal(b.Listings.Select(l => (l.Name, l.Stall, l.BasePrice)));
        a.Goods.Should().BeEquivalentTo(b.Goods);
    }

    [Fact]
    public void Core_market_rolls_two_kinds_of_goods_and_distinct_d100_rows()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            var market = Generate(new GenerateMarketRequest { Seed = seed });

            market.Goods.Should().HaveCount(2);
            market.Goods.Select(g => g.Roll).Should().OnlyHaveUniqueItems();
            foreach (var goods in market.Goods)
            {
                goods.Lines.Should().BeInRange(1, 10);
                goods.Rolls.Should().HaveCount(goods.Lines);
                goods.Rolls.Select(r => r.Band).Should().OnlyHaveUniqueItems();
                goods.Rolls.Should().OnlyContain(r => r.Band == (r.Roll - 1) / 5);
            }
            // Only the Core Rulebook by default.
            market.Listings.Where(l => l.ItemId != null && l.Origin == ListingOrigin.CoreTable)
                .Should().OnlyContain(l => Catalog.Find(l.ItemId)!.Book == "Core Book");
        }
    }

    [Fact]
    public void Typed_goods_and_lines_are_respected()
    {
        var market = Generate(new GenerateMarketRequest { Seed = 7, Goods = ["survival"], Lines = new() { ["survival"] = 10 } });

        market.Goods.Should().ContainSingle().Which.Should().Match<RolledGoods>(g => g.Id == "survival" && g.Rolls.Count == 10 && g.Roll == null);
        market.Stalls.Should().ContainSingle(s => s.Code == "N1" && s.Label == "Survival Gear");
    }

    [Fact]
    public void Generic_weapons_come_in_every_quality()
    {
        var market = Generate(new GenerateMarketRequest { Seed = 3, Goods = ["weapons"], Lines = new() { ["weapons"] = 10 } });
        var withQualities = market.Listings.Where(l => l.Slug == "medium-pistol" || l.Slug == "heavy-pistol").ToList();
        for (var seed = 4; withQualities.Count == 0 && seed < 60; seed++)
        {
            market = Generate(new GenerateMarketRequest { Seed = seed, Goods = ["weapons"], Lines = new() { ["weapons"] = 10 } });
            withQualities = market.Listings.Where(l => l.Slug == "medium-pistol" || l.Slug == "heavy-pistol").ToList();
        }

        var bySlug = withQualities.GroupBy(l => l.Slug).First();
        bySlug.Select(l => l.Quality).Should().BeEquivalentTo(new WeaponQuality?[] { WeaponQuality.Poor, WeaponQuality.Standard, WeaponQuality.Excellent });
        if (bySlug.Key == "medium-pistol")
            bySlug.OrderBy(l => l.Quality).Select(l => l.BasePrice).Should().Equal(20, 50, 100);
    }

    [Fact]
    public void An_option_brings_its_foundational_piece()
    {
        for (var seed = 1; seed <= 30; seed++)
        {
            var market = Generate(new GenerateMarketRequest { Seed = seed, Goods = ["cyberware"], Lines = new() { ["cyberware"] = 10 }, Books = ["Core Book", "Black Chrome"] });
            foreach (var listing in market.Listings.Where(l => l.ItemId != null))
                if (Catalog.Find(listing.ItemId)!.RequiresItemId is { } required)
                    market.Listings.Should().Contain(l => l.ItemId == required, $"{listing.Name} precisa da peça-base (seed {seed})");
        }
    }

    [Fact]
    public void Organizer_needs_rank_five_for_a_night_market_and_nine_for_a_midnight_market()
    {
        var low = () => Generate(new GenerateMarketRequest { Organizer = new MarketOrganizer { Name = "Padre", OperatorRank = 4 } });
        low.Should().Throw<RequestValidationException>().Which.Errors.Should().ContainKey("organizer.operatorRank");

        var midnight = () => Generate(new GenerateMarketRequest { Midnight = true, Organizer = new MarketOrganizer { OperatorRank = 6 } });
        midnight.Should().Throw<RequestValidationException>().Which.Errors.Should().ContainKey("midnight");

        var ok = Generate(new GenerateMarketRequest { Seed = 5, Midnight = true, Organizer = new MarketOrganizer { OperatorRank = 9 } });
        ok.OpenSlots.Count(s => s.Origin == ListingOrigin.Midnight).Should().BeInRange(6, 15);
    }

    [Fact]
    public void Black_chrome_market_lists_the_books_items_at_their_booths()
    {
        var market = Generate(new GenerateMarketRequest { PresetId = "bc-c", Seed = 11 });

        var expected = Rules.Booths.Where(b => b.Value.Any(c => c.StartsWith('C'))).Select(b => b.Key).ToList();
        market.Listings.Where(l => l.Origin == ListingOrigin.BlackChrome).Select(l => l.Slug).Should().BeEquivalentTo(expected);
        market.Listings.Single(l => l.Slug == "neo-soviet-cyberarm").Stall.Should().Be("C3");
        market.Stalls.Single(s => s.Code == "C3").Label.Should().Be("Cyberware");
        // Molly's own stock from Black Chrome+ goes to "Other Junk".
        market.Listings.Where(l => l.Origin == ListingOrigin.Preset).Should().HaveCount(12).And.OnlyContain(l => l.Stall == "C6");
        market.Organizer.Should().Match<MarketOrganizer>(o => o.Name == "Molly Anderson" && o.OperatorRank == 6 && o.Trading == 13);
        // Suggested goods of the book: weapons and cyberware.
        market.Goods.Select(g => g.Id).Should().Equal("weapons", "cyberware");
    }

    [Fact]
    public void Toggle_lends_the_weapons_and_charges_admission()
    {
        var market = Generate(new GenerateMarketRequest { PresetId = "toggle" });

        market.Listings.Where(l => l.Group == "Arsenal").Should().NotBeEmpty().And.OnlyContain(l => !l.ForSale);
        market.Listings.Single(l => l.Origin == ListingOrigin.Admission).Should().Match<MarketListing>(l => l.BasePrice == 20 && l.ForSale);
        market.Listings.Where(l => l.CategoryKey == "ammunition").Should().OnlyContain(l => l.ForSale);
    }

    [Fact]
    public void Interface_vendors_bring_their_own_stock()
    {
        Generate(new GenerateMarketRequest { PresetId = "hornet" }).Listings.Should().HaveCount(10);
        Generate(new GenerateMarketRequest { PresetId = "woodchipper" }).Listings.Should().HaveCount(14);
        Generate(new GenerateMarketRequest { PresetId = "mr-amaze" }).Listings.Select(l => l.Slug).Should().Contain(["neutongue", "holo-projector-palm"]);
        Generate(new GenerateMarketRequest { PresetId = "upload" }).Listings.Should().Contain(l => l.Slug == "sgi-technologies-kerberos" && l.Stall == "U1");
    }

    [Fact]
    public void Second_hand_goods_can_be_damaged_and_hidden()
    {
        var market = Generate(new GenerateMarketRequest
        {
            PresetId = "bc-c", Seed = 2, Options = new MarketOptions { DamagedPercent = 100, Scam = true },
        });

        market.Listings.Where(l => l.ItemId != null && l.BasePrice > 0)
            .Should().OnlyContain(l => l.Condition == ItemCondition.Damaged && l.ConditionHidden);
    }

    [Fact]
    public void Gm_choice_rows_become_open_slots()
    {
        var found = false;
        for (var seed = 1; seed <= 40 && !found; seed++)
        {
            var market = Generate(new GenerateMarketRequest { Seed = seed, Goods = ["cyberware"], Lines = new() { ["cyberware"] = 10 } });
            var slots = market.OpenSlots.Where(s => s.Origin == ListingOrigin.GmChoice).ToList();
            if (slots.Count == 0) continue;
            found = true;
            slots.Should().OnlyContain(s => s.Label.Contains("GM's Choice") && s.Stall == "N1");
        }
        found.Should().BeTrue();
    }
}
