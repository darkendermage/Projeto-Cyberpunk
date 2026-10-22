using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.NightMarkets;
using CyberpunkDb.Application.NightMarkets.Trading;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.NightMarkets.MarketTestData;

namespace CyberpunkDb.Tests.Unit.NightMarkets;

public class MarketOperationsTests
{
    private static MarketOperations Ops(params int[] dice) => new(Rules, Pricing, new FixedDice(dice.Length == 0 ? [5] : dice));

    private static MarketState Survival() =>
        Generate(new GenerateMarketRequest { Seed = 1, Goods = ["survival"], Lines = new() { ["survival"] = 10 } });

    private static MarketListing First(MarketState s, int minPrice = 0) =>
        s.Listings.First(l => l.ForSale && l.ItemId != null && l.BasePrice >= minPrice);

    [Fact]
    public void Purchase_goes_to_the_ledger_and_lowers_a_tracked_stock()
    {
        var change = Change(Survival());
        var listing = First(change.State);
        var ops = Ops();
        ops.UpdateListing(change, listing.Id, new UpdateListingRequest(0, null, ItemCondition.Normal, false, 3, true, null));

        ops.Purchase(change, new PurchaseRequest(0, listing.Id, 2, "Vex"));

        var sale = change.State.Sales.Single();
        sale.Should().Match<MarketSale>(s => s.Buyer == "Vex" && s.Quantity == 2 && s.Total == listing.BasePrice * 2 && s.PaidNow == s.Total);
        change.State.Listings.Single(l => l.Id == listing.Id).Stock.Should().Be(1);

        var tooMany = () => ops.Purchase(change, new PurchaseRequest(0, listing.Id, 2));
        tooMany.Should().Throw<RequestValidationException>();

        ops.UndoSale(change, sale.Id);
        change.State.Listings.Single(l => l.Id == listing.Id).Stock.Should().Be(3);
        change.State.Sales.Single().Undone.Should().BeTrue();
    }

    [Fact]
    public void Haggle_discount_applies_to_the_purchase()
    {
        var change = Change(Survival());
        var listing = First(change.State, minPrice: 50);

        Ops().Purchase(change, new PurchaseRequest(0, listing.Id, 1, "Fixer",
            new HaggleRequest(new HaggleSide(14, 1), new HaggleSide(10, 0), [9], [2]), "discount10"));

        change.State.Sales.Single().Total.Should().Be(listing.BasePrice - listing.BasePrice / 10);
    }

    [Fact]
    public void Lent_goods_cannot_be_bought()
    {
        var change = Change(Generate(new GenerateMarketRequest { PresetId = "toggle" }));
        var arsenal = change.State.Listings.First(l => l.Group == "Arsenal");

        var act = () => Ops().Purchase(change, new PurchaseRequest(0, arsenal.Id));

        act.Should().Throw<RequestValidationException>().Which.Errors["listingId"][0].Should().Contain("emprestadas");
    }

    [Fact]
    public void Door_prize_wheel_and_bonus()
    {
        var change = Change(Generate(new GenerateMarketRequest { PresetId = "bc-c", Seed = 3 }));

        var result = Ops().DoorPrize(change, new DoorPrizeRequest(0, "Lucky", 1));

        result.Prize.ItemName.Should().Be(Item("junk-ammunition").Name);
        result.Prize.Quantity.Should().Be(20);
        result.Bonus.Select(b => b.ItemId).Should().Equal(Item("piranha-smash").Id, Item("anti-smog-breathing-mask").Id);
        change.State.Sales.Should().HaveCount(3).And.OnlyContain(s => s.Kind == "prize" && s.Buyer == "Lucky");
    }

    [Fact]
    public void Mystery_box_reads_the_table_of_mystery()
    {
        var result = Ops().MysteryBox(new MysteryBoxRollRequest(5, 3), Catalog);

        result.Box.Should().Be("A box handsomely gift wrapped");
        result.Prize.Should().Match<PrizeResult>(p => p.ItemId == Item("medium-pistol").Id && p.Quality == WeaponQuality.Excellent);
        result.Source.Should().Be("IR4 p.27–28");
    }

    [Fact]
    public void Every_fiftieth_visitor_wins_the_door_prize_at_mollys()
    {
        var change = Change(Generate(new GenerateMarketRequest { PresetId = "bc-c", Seed = 3 }));

        Ops().Visitors(change, 120);

        change.State.Visitors.Should().Be(120);
        change.Notices.Should().HaveCount(2);
        change.Notices[1].Should().Contain("100");
    }

    [Fact]
    public void Gm_choice_slot_can_be_drawn()
    {
        var change = Change(Generate(new GenerateMarketRequest { Seed = 5, Midnight = true, Organizer = new MarketOrganizer { OperatorRank = 9 } }));
        var slot = change.State.OpenSlots.First(s => s.Origin == ListingOrigin.Midnight);

        Ops(1).FillSlot(change, slot.Id, new FillSlotRequest(0, null, true));

        change.State.OpenSlots.Should().NotContain(slot);
        var listing = change.State.Listings.Last();
        listing.Origin.Should().Be(ListingOrigin.Midnight);
        var item = Catalog.Find(listing.ItemId)!;
        (item.PriceCategory >= PriceCategory.Luxury || item.Exotic).Should().BeTrue();
    }

    [Fact]
    public void Gm_can_list_an_index_only_item_and_set_a_price()
    {
        var change = Change(Survival());
        var row = Rules.Index.Select((e, i) => (e, i)).First(x => x.e.Slug == null && x.e.PriceEb is > 0).i;
        var ops = Ops();

        ops.AddListing(change, new AddListingRequest(0, IndexRow: row, Stall: "N4"));

        var added = change.State.Listings.Last();
        added.Should().Match<MarketListing>(l => l.Origin == ListingOrigin.Index && l.Stall == "N4" && l.Name == Rules.Index[row].Name);
        change.State.Stalls.Should().Contain(s => s.Code == "N4");

        ops.SetPrice(change, added.Id, 1234);
        change.State.Listings.Last().Should().Match<MarketListing>(l => l.BasePrice == 1234 && l.BaseCategory == PriceCategory.VeryExpensive);
    }
}
