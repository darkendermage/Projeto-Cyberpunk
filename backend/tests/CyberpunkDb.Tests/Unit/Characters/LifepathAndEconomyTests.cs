using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Creation;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Application.Common;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

public class LifepathAndEconomyTests
{
    private static readonly EconomyOperations Economy = new(Calculator);
    private static readonly DateTime Now = new(2045, 3, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private static CharacterChange Change(CharacterSheet s) => new(s, Catalog, Now);

    private static CharacterSheet Living(DateOnly? paidUntil, int balance = 5000) => CompletePackage() with
    {
        Creation = CompletePackage().Creation with { Finalized = true },
        Housing = new HousingState { HousingId = "cargo-container", LifestyleId = "kibble", PaidUntil = paidUntil },
        Wallet = new Wallet { Balance = balance },
    };

    // ------------------------------------------------------------ languages (7.1)

    [Fact]
    public void Streetslang_as_native_language_gives_no_free_levels()
    {
        var sheet = CompletePackage() with { Identity = CompletePackage().Identity with { NativeLanguage = "Streetslang" } };
        sheet = (sheet with { Skills = new Dictionary<string, int>(sheet.Skills, StringComparer.OrdinalIgnoreCase) { ["language:English"] = 0 } }).Normalize();

        CreationCosts.NativeLanguageKey(sheet).Should().BeNull();
        CreationCosts.SkillPoints(Rules, sheet).Should().Be(86 - 0, "Streetslang 2 continua custando 2 pontos");
        Validator.Validate(sheet, Catalog).Issues.Should().Contain(i => i.Code == "NATIVE_LANGUAGE_STREETSLANG");
    }

    [Fact]
    public void Native_language_outside_the_cultural_region_is_allowed_with_a_note()
    {
        // English is listed in every region, so the check uses Yoruba (Sub-Saharan African) in an East Asian origin.
        var skills = new Dictionary<string, int>(CompletePackage().Skills, StringComparer.OrdinalIgnoreCase) { ["language:English"] = 0, ["language:Yoruba"] = 4 };
        var sheet = (CompletePackage() with
        {
            Identity = CompletePackage().Identity with { NativeLanguage = "Yoruba" },
            Skills = skills,
            Lifepath = new LifepathState { CulturalRegion = "East Asian" },
        }).Normalize();
        var report = Validator.Validate(sheet, Catalog);
        report.Issues.Should().ContainSingle(i => i.Code == "NATIVE_LANGUAGE_OUTSIDE_REGION").Which.Severity.Should().Be(CharacterIssueSeverity.Info);

        var inRegion = sheet with { Lifepath = new LifepathState { CulturalRegion = "Sub-Saharan African" } };
        Validator.Validate(inRegion, Catalog).Issues.Should().NotContain(i => i.Code == "NATIVE_LANGUAGE_OUTSIDE_REGION");
    }

    // ------------------------------------------------------------ relationships (7.2)

    [Fact]
    public void Relationships_get_unique_ids_and_blank_answers_are_dropped()
    {
        var lifepath = new LifepathState
        {
            Answers = new() { ["personality"] = "Stable and serious", ["lifeGoal"] = "  " },
            Relationships =
            [
                new Relationship { Kind = RelationshipKind.Enemy, Name = " Booster ganger ", Cause = "Caused the loss of lover, friend, or relative." },
                new Relationship { Id = "x", Kind = RelationshipKind.Friend, Name = "Vendedor de pipoca" },
                new Relationship { Id = "x", Kind = RelationshipKind.Ally, Name = "Duplicado" },
            ],
        }.Normalize();

        lifepath.Relationships.Select(r => r.Id).Should().OnlyHaveUniqueItems().And.NotContain(id => string.IsNullOrWhiteSpace(id));
        lifepath.Relationships[0].Name.Should().Be("Booster ganger");
        lifepath.Answers.Should().ContainKey("personality").And.NotContainKey("lifeGoal");
    }

    // ------------------------------------------------------------ housing and lifestyle (7.5, 11.2)

    [Fact]
    public void Monthly_cost_is_rent_plus_lifestyle_and_status_follows_the_one_week_grace()
    {
        var paid = Calculator.ComputeHousing(Living(Today.AddDays(3)), Today);
        paid.MonthlyTotal.Should().Be(1000 + 100);
        paid.Status.Should().Be(PaymentStatus.Paid);

        var grace = Calculator.ComputeHousing(Living(Today.AddDays(-5)), Today);
        grace.Status.Should().Be(PaymentStatus.Pending);
        grace.DaysOverdue.Should().Be(5);

        Calculator.ComputeHousing(Living(Today.AddDays(-8)), Today).Status.Should().Be(PaymentStatus.Overdue);

        var deal = Living(Today) with { Housing = Living(Today).Housing with { RentOverride = 400 } };
        Calculator.ComputeHousing(deal, Today).Should().Match<Application.Characters.Calculation.HousingReport>(h => h.Rent == 400 && !h.RentFromTable);
    }

    [Fact]
    public void Paying_months_charges_the_balance_and_extends_from_the_previous_date()
    {
        var overdue = Living(Today.AddDays(-20));
        var change = Change(overdue);
        var paid = Economy.PayMonths(overdue, new PayMonthsRequest(0, 2), change);

        paid.Wallet.Balance.Should().Be(5000 - 2 * 1100);
        paid.Housing.PaidUntil.Should().Be(Today.AddDays(-20).AddMonths(2));
        change.Events.Should().ContainSingle(e => e.Action == "monthly-payment" && e.Amount == -2200);
    }

    [Fact]
    public void Paying_more_than_the_balance_is_refused()
    {
        var act = () => Economy.PayMonths(Living(Today, balance: 500), new PayMonthsRequest(0), Change(Living(Today, 500)));
        act.Should().Throw<RequestValidationException>();
    }

    // ------------------------------------------------------------ selling and discarding (9.4)

    private static CharacterSheet WithGear() => Living(Today) with
    {
        Inventory =
        [
            new InventoryEntry { InstanceId = "a", ItemId = Agent.Id, Quantity = 2 },
            new InventoryEntry { InstanceId = "t", ItemId = LeisureTop.Id },
        ],
        Style = new StyleState { OutfitItems = ["t"] },
    };

    [Fact]
    public void Selling_part_of_a_stack_is_a_manual_ledger_entry()
    {
        var change = Change(WithGear());
        var sold = EconomyOperations.RemoveItem(WithGear(), new RemoveItemRequest(0, "a", 1, 40, "Vendido ao Fixer"), change);

        sold.Inventory.Single(i => i.InstanceId == "a").Quantity.Should().Be(1);
        sold.Wallet.Balance.Should().Be(5040);
        change.Events.Should().ContainSingle(e => e.Action == "sale" && e.IsManual && e.Amount == 40);
    }

    [Fact]
    public void Discarding_an_outfit_piece_unlinks_it_from_the_style()
    {
        var discarded = EconomyOperations.RemoveItem(WithGear(), new RemoveItemRequest(0, "t"), Change(WithGear()));
        discarded.Inventory.Should().NotContain(i => i.InstanceId == "t");
        discarded.Style.OutfitItems.Should().BeEmpty();
        discarded.Wallet.Balance.Should().Be(5000);
    }

    [Fact]
    public void A_sale_needs_a_reason_and_creation_uses_undo_instead()
    {
        var noReason = () => EconomyOperations.RemoveItem(WithGear(), new RemoveItemRequest(0, "a", 1, 40), Change(WithGear()));
        noReason.Should().Throw<RequestValidationException>();

        var creating = WithGear() with { Creation = WithGear().Creation with { Finalized = false } };
        var duringCreation = () => EconomyOperations.RemoveItem(creating, new RemoveItemRequest(0, "a"), Change(creating));
        duringCreation.Should().Throw<RequestValidationException>();
    }
}
