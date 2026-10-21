using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Npcs;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

public class CyberwareOperationsTests
{
    private sealed class FixedDice(int value) : IDiceRoller
    {
        public int Roll(int sides) => Math.Min(value, sides);
    }

    // Every die shows 2: 2d6 → 4, 1d6 → 2, 4d6 → 8, 1d6/2 → 1.
    private static readonly IDiceRoller Twos = new FixedDice(2);
    private static readonly CyberwareInstaller Installer = new(Rules, Twos);
    private static readonly CyberwareOperations Ops = new(Rules, Installer, Calculator, Twos);
    private static readonly InventoryOperations Inventory = new(Rules, Ops);

    /// <summary>A finished Complete Package Solo (EMP 6, Humanity 60) with money and nothing installed.</summary>
    private static CharacterSheet Chromeless(int balance = 20_000) => CompletePackage() with
    {
        Creation = CompletePackage().Creation with { Finalized = true },
        Wallet = new Wallet { Balance = balance },
    };

    private static CharacterChange Change(CharacterSheet s) => new(s, Catalog, DateTime.UtcNow);
    private static CharacterSheet Install(CharacterSheet s, CatalogItem item, bool meatLimb = false, string? host = null, bool ack = false) =>
        Ops.Install(s, new InstallCyberwareRequest(0, item.Id, null, meatLimb, host, ack), Change(s));
    private static Func<CharacterSheet> Trying(CharacterSheet s, CatalogItem item, bool meatLimb = false, string? host = null, bool ack = false) =>
        () => Install(s, item, meatLimb, host, ack);
    private static CharacterReport Calc(CharacterSheet s) => Calculator.Calculate(s, Catalog);

    // ------------------------------------------------------------ 15. money and Humanity

    [Fact]
    public void Installing_charges_the_balance_and_rolls_humanity_loss_after_creation()
    {
        var sheet = Install(Chromeless(), NeuralLink);

        sheet.Wallet.Balance.Should().Be(20_000 - 500);
        sheet.Cyberware.Single().HumanityLossApplied.Should().Be(4, "2d6 com dados 2");
        sheet.Cyberware.Single().HumanityLossRolled.Should().BeTrue();
        Calc(sheet).Humanity.Current.Should().Be(56);
        Calc(sheet).Humanity.TherapyCap.Should().Be(58);
    }

    [Fact]
    public void During_creation_humanity_loss_is_preset()
    {
        var creating = Chromeless() with { Creation = CompletePackage().Creation };
        var sheet = Install(creating, NeuralLink);
        sheet.Cyberware.Single().HumanityLossApplied.Should().Be(7);
        sheet.Cyberware.Single().HumanityLossRolled.Should().BeFalse();
    }

    // ------------------------------------------------------------ 13. dependencies and compatibility

    [Fact]
    public void Options_bring_their_foundation_and_charge_it()
    {
        var sheet = Install(Chromeless(), Sandevistan);

        sheet.Cyberware.Select(c => c.ItemId).Should().Equal(NeuralLink.Id, Sandevistan.Id);
        sheet.Cyberware[1].ParentInstanceId.Should().Be(sheet.Cyberware[0].InstanceId);
        sheet.Wallet.Balance.Should().Be(20_000 - 1000);

        var scoped = Install(Chromeless(), TargetingScope);
        scoped.Cyberware.Select(c => c.ItemId).Should().Equal(Cybereye.Id, TargetingScope.Id);
        scoped.Cyberware[0].Side.Should().Be(BodySide.Left);
    }

    [Fact]
    public void Only_one_piece_of_speedware_and_one_neural_link()
    {
        var sheet = Install(Chromeless(), Sandevistan);
        Trying(sheet, Kerenzikov).Should().Throw<RequestValidationException>();
        Trying(sheet, NeuralLink).Should().Throw<RequestValidationException>();
    }

    // ------------------------------------------------------------ 12. slots

    [Fact]
    public void A_full_cybereye_refuses_more_options()
    {
        var sheet = Chromeless();
        foreach (var option in new[] { TargetingScope, Microoptics, Teleoptics }) sheet = Install(sheet, option);
        sheet.Cyberware.Count(c => c.ItemId == Cybereye.Id).Should().Be(1);

        // The first eye is full (3/3): the planner opens the second eye instead.
        sheet = Install(sheet, ImageEnhance);
        sheet.Cyberware.Count(c => c.ItemId == Cybereye.Id).Should().Be(2);
        sheet.Cyberware.Single(c => c.ItemId == ImageEnhance.Id).Side.Should().Be(BodySide.Right);

        // Two eyes is the body's limit; with both full there is no room left.
        foreach (var option in new[] { TargetingScope, Microoptics }) sheet = Install(sheet, option);
        Trying(sheet, Teleoptics).Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Pieces_go_to_the_side_the_player_picked()
    {
        var arm = Ops.Install(Chromeless(), new InstallCyberwareRequest(0, Cyberarm.Id, Side: BodySide.Right), Change(Chromeless()));
        arm.Cyberware.Single().Side.Should().Be(BodySide.Right);

        var eyes = Install(Install(Chromeless(), Cybereye), Cybereye); // left, then right
        var scoped = Ops.Install(eyes, new InstallCyberwareRequest(0, TargetingScope.Id, Side: BodySide.Right), Change(eyes));
        var right = scoped.Cyberware.Single(c => c.ItemId == Cybereye.Id && c.Side == BodySide.Right).InstanceId;
        scoped.Cyberware.Single(c => c.ItemId == TargetingScope.Id).ParentInstanceId.Should().Be(right);
    }

    // ------------------------------------------------------------ 14. pairs

    [Fact]
    public void Paired_options_go_in_two_eyes_as_one_operation()
    {
        var sheet = Install(Chromeless(), LowLight);

        sheet.Cyberware.Count(c => c.ItemId == Cybereye.Id).Should().Be(2);
        sheet.Cyberware.Count(c => c.ItemId == LowLight.Id).Should().Be(2);
        sheet.Cyberware.Where(c => c.ItemId == LowLight.Id).Select(c => c.Side).Should().BeEquivalentTo([BodySide.Left, BodySide.Right]);
        sheet.Wallet.Balance.Should().Be(20_000 - (2 * 100 + 2 * 500));
    }

    // ------------------------------------------------------------ meat limbs and options of any limb

    [Fact]
    public void Meat_limb_pieces_take_one_free_arm_or_leg_each()
    {
        var sheet = Install(Chromeless(), Wolvers, meatLimb: true);
        sheet.Cyberware.Should().ContainSingle().Which.MeatLimb.Should().BeTrue();

        sheet = Install(sheet, Wolvers, meatLimb: true);
        Trying(sheet, Wolvers, meatLimb: true).Should().Throw<RequestValidationException>("só há dois braços");
        Trying(Chromeless(), TargetingScope, meatLimb: true).Should().Throw<RequestValidationException>("não é opção de membro de carne");

        var leg = Install(Chromeless(), TalonFoot, meatLimb: true);
        Calc(leg).Regions.Single(r => r.Key == "leg-left").Pieces.Should().ContainSingle();
    }

    [Fact]
    public void Coverings_need_a_cyberlimb_take_no_slot_and_only_one_per_limb()
    {
        Trying(Chromeless(), PlasticCovering).Should().Throw<RequestValidationException>("precisa de Cyberarm ou Cyberleg");

        var arm = Install(Chromeless(), Cyberarm);
        var armId = arm.Cyberware.Single().InstanceId;
        var covered = Install(arm, PlasticCovering, host: armId);
        covered.Cyberware.Single(c => c.ItemId == PlasticCovering.Id).ParentInstanceId.Should().Be(armId);
        Calc(covered).Cyberware.Single(c => c.InstanceId == armId).SlotsUsed.Should().Be(0);

        Trying(covered, RealskinnCovering, host: armId).Should().Throw<RequestValidationException>("uma cobertura por membro");

        var leg = Install(covered, Cyberleg);
        var shielded = Install(leg, HardenedShielding, host: leg.Cyberware.Single(c => c.ItemId == Cyberleg.Id).InstanceId);
        Calc(shielded).Cyberware.Single(c => c.ItemId == Cyberleg.Id).SlotsUsed.Should().Be(1);
    }

    [Fact]
    public void Standalone_pools_report_their_seven_slots()
    {
        var report = Calc(Install(Install(Chromeless(), Biomonitor), SubdermalArmor));
        report.Regions.Single(r => r.Key == "fashion").Should().Match<CyberRegionReport>(r => r.SlotsTotal == 7 && r.SlotsUsed == 1);
        report.Regions.Single(r => r.Key == "external").Should().Match<CyberRegionReport>(r => r.SlotsTotal == 7 && r.SlotsUsed == 1);
        report.Regions.Single(r => r.Key == "internal").SlotsTotal.Should().Be(7);
    }

    // ------------------------------------------------------------ requirements only in text

    [Fact]
    public void Requirements_in_text_must_be_confirmed()
    {
        Trying(Chromeless(), HeavyPlating).Should().Throw<RequestValidationException>().Which.Errors.Should().ContainKey("acknowledge");
        Install(Chromeless(), HeavyPlating, ack: true).Cyberware.Should().ContainSingle();
    }

    // ------------------------------------------------------------ Full Body Conversion (IR3 p.93)

    [Fact]
    public void In_an_fbc_body_listed_foundations_cost_no_humanity_and_slots_double()
    {
        var body = Install(Install(Chromeless(50_000), Biosystem), FbcBody, ack: true);
        Calc(body).FullBodyConversion.Should().BeTrue();

        var arm = Install(body, Cyberarm);
        arm.Cyberware.Single(c => c.ItemId == Cyberarm.Id).HumanityLossApplied.Should().Be(0);
        var report = Calc(arm);
        report.Cyberware.Single(c => c.ItemId == Cyberarm.Id).SlotsProvided.Should().Be(8);
        report.Regions.Single(r => r.Key == "internal").SlotsTotal.Should().Be(14);
        report.Humanity.BorgwareLoweringCap.Should().Be(2, "Biosystem e o corpo; o Cyberarm não conta");
    }

    // ------------------------------------------------------------ 16. removal and therapy

    [Fact]
    public void Removal_keeps_the_humanity_lost_raises_the_cap_and_returns_the_piece()
    {
        var sheet = Install(Chromeless(), Sandevistan);
        var lost = Calc(sheet).Humanity.Current;
        var link = sheet.Cyberware.Single(c => c.ItemId == NeuralLink.Id).InstanceId;

        var change = Change(sheet);
        var removed = Ops.Remove(sheet, new RemoveCyberwareRequest(0, link), change);

        removed.Cyberware.Should().BeEmpty("a Sandevistan sai junto com o Neural Link");
        removed.Inventory.Select(i => i.ItemId).Should().BeEquivalentTo([NeuralLink.Id, Sandevistan.Id]);
        Calc(removed).Humanity.Current.Should().Be(lost, "a Humanidade não volta com a remoção");
        Calc(removed).Humanity.TherapyCap.Should().Be(60);
        change.Events.Should().Contain(e => e.Action == "remove-cyberware");
    }

    [Fact]
    public void Therapy_recovers_up_to_the_cap_and_is_paid()
    {
        var sheet = Install(Install(Chromeless(), NeuralLink), Sandevistan); // lost 4 + 4 → 52; cap 56
        var change = Change(sheet);
        var treated = Ops.Therapy(sheet, new TherapyRequest(0, "extreme", "clinic", 20), change);

        Calc(treated).Humanity.Current.Should().Be(56, "4d6 = 20 limitado pelo teto");
        treated.Wallet.Balance.Should().Be(sheet.Wallet.Balance - 1000);
        change.Events.Should().Contain(e => e.Action == "therapy" && e.Amount == 4);

        var atCap = () => Ops.Therapy(treated, new TherapyRequest(0, "standard"), Change(treated));
        atCap.Should().Throw<RequestValidationException>();

        var badRoll = () => Ops.Therapy(sheet, new TherapyRequest(0, "standard", "medtech", 13), Change(sheet));
        badRoll.Should().Throw<RequestValidationException>("2d6 vai até 12");
        Ops.Therapy(sheet, new TherapyRequest(0, "standard", "medtech", 3), Change(sheet)).Wallet.Balance
            .Should().Be(sheet.Wallet.Balance - 100, "o Medtech paga só os materiais");
    }

    // ------------------------------------------------------------ 17. nothing partial

    [Fact]
    public void A_refused_installation_changes_nothing()
    {
        var poor = Chromeless(balance: 600);
        var change = Change(poor);
        var act = () => Ops.Install(poor, new InstallCyberwareRequest(0, Sandevistan.Id), change);

        act.Should().Throw<RequestValidationException>();
        change.Events.Should().BeEmpty();
        poor.Cyberware.Should().BeEmpty();
        poor.Wallet.Balance.Should().Be(600);
    }

    [Fact]
    public void Replacing_is_one_operation_that_fails_whole()
    {
        var sheet = Install(Chromeless(1100), Sandevistan); // 100 eb left
        var sandevistan = sheet.Cyberware.Single(c => c.ItemId == Sandevistan.Id).InstanceId;

        var tooExpensive = () => Ops.Replace(sheet, new ReplaceCyberwareRequest(0, sandevistan, Kerenzikov.Id), Change(sheet));
        tooExpensive.Should().Throw<RequestValidationException>();

        var rich = sheet with { Wallet = new Wallet { Balance = 1000 } };
        var swapped = Ops.Replace(rich, new ReplaceCyberwareRequest(0, sandevistan, Kerenzikov.Id), Change(rich));
        swapped.Cyberware.Select(c => c.ItemId).Should().Equal(NeuralLink.Id, Kerenzikov.Id);
        swapped.Inventory.Should().ContainSingle(i => i.ItemId == Sandevistan.Id);
    }

    [Fact]
    public void Installing_from_the_inventory_charges_only_missing_pieces()
    {
        var owned = Chromeless(5000) with
        {
            Inventory = [new InventoryEntry { InstanceId = "s1", ItemId = Sandevistan.Id }],
        };
        var installed = Ops.Install(owned, new InstallCyberwareRequest(0, null, "s1"), Change(owned));
        installed.Wallet.Balance.Should().Be(4500, "só o Neural Link é comprado");
        installed.Inventory.Should().BeEmpty();
        installed.Cyberware.Single(c => c.ItemId == Sandevistan.Id).PricePaid.Should().Be(0);
    }

    [Fact]
    public void Preview_shows_cost_humanity_range_and_problems_without_changing_anything()
    {
        var preview = Ops.Preview(Chromeless(800), new InstallCyberwareRequest(0, Sandevistan.Id), Catalog);
        preview.Pieces.Select(p => p.Name).Should().Equal("neural-link", "sandevistan");
        preview.Price.Should().Be(1000);
        preview.CanInstall.Should().BeFalse();
        preview.HumanityLossMin.Should().Be(4);
        preview.HumanityLossMax.Should().Be(24);
        preview.EmpAfterWorst.Should().Be(3);

        Ops.Preview(Chromeless(), new InstallCyberwareRequest(0, Kerenzikov.Id), Catalog).Error.Should().BeNull();
        Ops.Preview(Install(Chromeless(), Sandevistan), new InstallCyberwareRequest(0, Kerenzikov.Id), Catalog).Error.Should().NotBeNull();
    }

    // ------------------------------------------------------------ purchases (9.3): nothing installed or equipped by itself

    [Fact]
    public void After_creation_cyberware_is_bought_into_the_inventory_unless_installing_is_asked()
    {
        var bought = Inventory.Purchase(Chromeless(), new PurchaseRequest(0, NeuralLink.Id), Change(Chromeless()));
        bought.Cyberware.Should().BeEmpty();
        bought.Inventory.Should().ContainSingle(i => i.ItemId == NeuralLink.Id);

        var installed = Inventory.Purchase(Chromeless(), new PurchaseRequest(0, NeuralLink.Id, Install: true), Change(Chromeless()));
        installed.Cyberware.Should().ContainSingle();

        var armor = Inventory.Purchase(Chromeless(), new PurchaseRequest(0, LightArmorjack.Id), Change(Chromeless()));
        armor.Inventory.Single().State.Should().Be(ItemState.Carried);
        var worn = Inventory.Purchase(Chromeless(), new PurchaseRequest(0, LightArmorjack.Id, Equip: true, Worn: ArmorSlot.Body), Change(Chromeless()));
        worn.Inventory.Single().Should().Match<InventoryEntry>(e => e.State == ItemState.Equipped && e.Worn == ArmorSlot.Body);
    }

    [Fact]
    public void Items_received_from_other_players_cost_nothing_and_are_logged()
    {
        var change = Change(Chromeless());
        var gift = Inventory.Purchase(Chromeless(), new PurchaseRequest(0, LightArmorjack.Id, Received: ItemOrigin.Trade, Note: "da Vex"), change);
        gift.Wallet.Balance.Should().Be(20_000);
        gift.Inventory.Single().Should().Match<InventoryEntry>(e => e.Origin == ItemOrigin.Trade && e.PricePaid == 0 && e.Notes == "da Vex");
        change.Events.Should().ContainSingle(e => e.Action == "receive" && e.IsManual);

        // The received piece is free; its Neural Link is still paid and the Humanity Loss still applies.
        var chrome = Inventory.Purchase(Chromeless(), new PurchaseRequest(0, Sandevistan.Id, Install: true, Received: ItemOrigin.Gift), Change(Chromeless()));
        chrome.Inventory.Should().BeEmpty();
        chrome.Cyberware.Should().Contain(c => c.ItemId == Sandevistan.Id && c.Origin == ItemOrigin.Gift && c.PricePaid == 0);
        chrome.Wallet.Balance.Should().Be(20_000 - NeuralLink.Price!.Value);
        chrome.Humanity.Lost.Should().BeGreaterThan(0);

        var creating = CompletePackage();
        var duringCreation = () => Inventory.Purchase(creating, new PurchaseRequest(0, LightArmorjack.Id, Received: ItemOrigin.Gift), Change(creating));
        duringCreation.Should().Throw<RequestValidationException>("o orçamento da criação não pode ser contornado");
        var bought = () => Inventory.Purchase(Chromeless(), new PurchaseRequest(0, LightArmorjack.Id, Received: ItemOrigin.Purchase), Change(Chromeless()));
        bought.Should().Throw<RequestValidationException>();
    }

    // ------------------------------------------------------------ 20. weapon attachments

    private static CharacterSheet Armory() => Chromeless() with
    {
        Inventory =
        [
            new InventoryEntry { InstanceId = "rifle", ItemId = AssaultRifle.Id },
            new InventoryEntry { InstanceId = "pistol", ItemId = HeavyPistol.Id },
            new InventoryEntry { InstanceId = "stun", ItemId = ExoticPistol.Id },
            new InventoryEntry { InstanceId = "smart", ItemId = SmartgunLink.Id, Quantity = 2 },
            new InventoryEntry { InstanceId = "under", ItemId = Underbarrel.Id },
            new InventoryEntry { InstanceId = "scope", ItemId = SnipingScope.Id },
            new InventoryEntry { InstanceId = "sear", ItemId = PistolAutosear.Id },
        ],
    };

    private static CharacterSheet Attach(CharacterSheet s, string weapon, string attachment) =>
        Inventory.Attach(s, new AttachRequest(0, weapon, attachment), Change(s));

    [Fact]
    public void Attachments_check_class_slots_and_exotic_weapons()
    {
        var sheet = Attach(Armory(), "rifle", "smart");
        sheet.Inventory.Single(i => i.InstanceId == "smart").Quantity.Should().Be(1, "um Smartgun Link sai da pilha");
        var rifleAttachments = sheet.Inventory.Single(i => i.InstanceId == "rifle").Weapon!.Attachments;
        rifleAttachments.Should().ContainSingle();

        var act = () => Attach(sheet, "rifle", "under");
        act.Should().Throw<RequestValidationException>("2 + 2 passa dos 3 Attachment Slots");
        Attach(sheet, "rifle", "scope").Inventory.Single(i => i.InstanceId == "rifle").Weapon!.Attachments.Should().HaveCount(2);

        var wrongClass = () => Attach(Armory(), "pistol", "under");
        wrongClass.Should().Throw<RequestValidationException>();
        var exotic = () => Attach(Armory(), "stun", "scope");
        exotic.Should().Throw<RequestValidationException>();
        Attach(Armory(), "stun", "sear").Inventory.Single(i => i.InstanceId == "stun").Weapon!.Attachments.Should().ContainSingle("Internals aceita exóticas e não ocupa slot");

        var detached = Inventory.Detach(sheet, new AttachRequest(0, "rifle", rifleAttachments[0]), Change(sheet));
        detached.Inventory.Single(i => i.InstanceId == "rifle").Weapon!.Attachments.Should().BeEmpty();
    }
}
