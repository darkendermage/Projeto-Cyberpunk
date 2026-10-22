using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.NightMarkets;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.NightMarkets.MarketTestData;

namespace CyberpunkDb.Tests.Unit.NightMarkets;

public class MarketPricingTests
{
    private static MarketListing AssaultRifle(ItemCondition condition, bool hidden = false) => new()
    {
        Id = 1, Name = "Assault Rifle", BasePrice = 500, BaseCategory = PriceCategory.Expensive, Condition = condition, ConditionHidden = hidden,
    };

    [Fact]
    public void Damaged_sells_one_category_lower_and_destroyed_two_like_the_example_of_ir5()
    {
        Pricing.Value(AssaultRifle(ItemCondition.Damaged)).Should().Be((100, PriceCategory.Premium));
        Pricing.Value(AssaultRifle(ItemCondition.Destroyed)).Should().Be((50, PriceCategory.Costly));
        Pricing.Value(AssaultRifle(ItemCondition.BeyondRepair)).Price.Should().Be(0);
    }

    [Fact]
    public void Nothing_goes_below_cheap()
    {
        Pricing.Value(10, PriceCategory.Cheap, ItemCondition.Destroyed).Should().Be((10, PriceCategory.Cheap));
        Pricing.Value(20, PriceCategory.Everyday, ItemCondition.Destroyed).Should().Be((10, PriceCategory.Cheap));
    }

    [Fact]
    public void A_scam_asks_the_full_price_but_keeps_the_real_value()
    {
        var scam = AssaultRifle(ItemCondition.Damaged, hidden: true);
        Pricing.Asked(scam).Should().Be((500, PriceCategory.Expensive));
        Pricing.Value(scam).Should().Be((100, PriceCategory.Premium));
        Pricing.Warnings(scam, new MarketOrganizer()).Should().Contain(w => w.Contains("Golpe"));
    }

    [Fact]
    public void Haggle_ties_go_to_the_seller()
    {
        var result = Pricing.Haggle(new HaggleRequest(new HaggleSide(12, 3), new HaggleSide(13, 2), [5], [5]), new FixedDice(5));

        result.BuyerTotal.Should().Be(20);
        result.SellerTotal.Should().Be(20);
        result.Success.Should().BeFalse();
        result.Deals.Should().OnlyContain(d => !d.Applies);
    }

    [Fact]
    public void Natural_ten_adds_and_natural_one_subtracts_a_second_die()
    {
        var result = Pricing.Haggle(new HaggleRequest(new HaggleSide(10, 1), new HaggleSide(10, 0), [10, 4], [1, 3]), new FixedDice(5));

        result.BuyerRolls.Should().Equal(10, 4);
        result.BuyerTotal.Should().Be(10 + 1 + 14);
        result.SellerTotal.Should().Be(10 + 0 - 2);
    }

    [Fact]
    public void Only_a_fixer_gets_a_deal()
    {
        var result = Pricing.Haggle(new HaggleRequest(new HaggleSide(20, 0), new HaggleSide(5, 0), [9], [1, 1]), new FixedDice(5));

        result.Success.Should().BeFalse();
        result.Deals.Should().BeEmpty();
    }

    [Fact]
    public void A_rank_one_fixer_gets_ten_percent_and_a_rank_nine_up_to_twenty()
    {
        var rank1 = Pricing.Haggle(new HaggleRequest(new HaggleSide(14, 1), new HaggleSide(10, 0), [8], [2]), new FixedDice(5));
        rank1.Deals.Select(d => d.Id).Should().Equal("discount10");
        Pricing.Apply(rank1, "discount10", 500, PriceCategory.Expensive, 1).Should().Match<Application.NightMarkets.Trading.DealOutcome>(o => o.Total == 450);

        var rank9 = Pricing.Haggle(new HaggleRequest(new HaggleSide(14, 9), new HaggleSide(10, 0), [8], [2]), new FixedDice(5));
        rank9.Deals.Where(d => d.Applies).Select(d => d.Id).Should().BeEquivalentTo("discount10", "discount20");
        rank9.Deals.Single(d => d.Id == "jobPay20").Reason.Should().Contain("Job");
        Pricing.Apply(rank9, "discount20", 500, PriceCategory.Expensive, 2).Total.Should().Be(800);
    }

    [Fact]
    public void Buy_five_get_one_and_split_payment_have_their_conditions()
    {
        var haggle = Pricing.Haggle(new HaggleRequest(new HaggleSide(14, 7), new HaggleSide(10, 0), [8], [2]), new FixedDice(5), PriceCategory.Premium, 5);
        Pricing.Apply(haggle, "buyFiveGetOne", 100, PriceCategory.Premium, 5).Delivered.Should().Be(6);
        haggle.Deals.Single(d => d.Id == "splitPayment").Applies.Should().BeFalse();

        var four = Pricing.Haggle(new HaggleRequest(new HaggleSide(14, 3), new HaggleSide(10, 0), [8], [2]), new FixedDice(5), PriceCategory.Premium, 4);
        var act = () => Pricing.Apply(four, "buyFiveGetOne", 100, PriceCategory.Premium, 4);
        act.Should().Throw<RequestValidationException>();

        var luxury = Pricing.Haggle(new HaggleRequest(new HaggleSide(14, 7), new HaggleSide(10, 0), [8], [2]), new FixedDice(5), PriceCategory.Luxury);
        Pricing.Apply(luxury, "splitPayment", 5000, PriceCategory.Luxury, 1).Should()
            .Match<Application.NightMarkets.Trading.DealOutcome>(o => o.PaidNow == 2500 && o.DueLater == 2500);
    }

    [Fact]
    public void A_deal_above_the_rank_or_without_winning_is_refused()
    {
        var lost = Pricing.Haggle(new HaggleRequest(new HaggleSide(10, 9), new HaggleSide(20, 5), [2], [9]), new FixedDice(5));
        var act = () => Pricing.Apply(lost, "discount20", 500, PriceCategory.Expensive, 1);
        act.Should().Throw<RequestValidationException>();

        var rank2 = Pricing.Haggle(new HaggleRequest(new HaggleSide(14, 2), new HaggleSide(10, 0), [8], [2]), new FixedDice(5));
        var tooHigh = () => Pricing.Apply(rank2, "discount20", 500, PriceCategory.Expensive, 1);
        tooHigh.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Selling_damaged_gear_depends_on_what_the_fixer_knows()
    {
        var dice = new FixedDice(5);
        Pricing.SellQuote("Assault Rifle", 500, PriceCategory.Expensive, new SellQuoteRequest(Condition: ItemCondition.Damaged), dice)
            .Offer.Should().Be(100);
        Pricing.SellQuote("Assault Rifle", 500, PriceCategory.Expensive,
                new SellQuoteRequest(Condition: ItemCondition.Damaged, Disclosure: ConditionDisclosure.Silent, FixerRank: 4), dice)
            .Should().Match<SellQuote>(q => q.Offer == 100 && q.FixerNoticesDamage);
        Pricing.SellQuote("Assault Rifle", 500, PriceCategory.Expensive,
                new SellQuoteRequest(Condition: ItemCondition.Damaged, Disclosure: ConditionDisclosure.Silent, FixerRank: 3), dice)
            .Offer.Should().Be(500);
        Pricing.SellQuote("Assault Rifle", 500, PriceCategory.Expensive,
                new SellQuoteRequest(Condition: ItemCondition.Damaged, Disclosure: ConditionDisclosure.Disguised, FixerRank: 9), dice)
            .Offer.Should().Be(500);
    }

    [Fact]
    public void A_fixer_selling_gets_ten_percent_more_with_haggle()
    {
        var quote = Pricing.SellQuote("Heavy Pistol", 100, PriceCategory.Premium,
            new SellQuoteRequest(Haggle: new HaggleRequest(new HaggleSide(14, 2), new HaggleSide(10, 4), [9], [2]), DealId: "discount10"),
            new FixedDice(5));

        quote.Offer.Should().Be(110);
    }
}
