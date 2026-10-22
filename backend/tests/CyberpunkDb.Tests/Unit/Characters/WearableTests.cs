using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Npcs;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

/// <summary>Cyberware options in worn hosts: Smart Glasses, Smart Lens, Battleglove and Smart Gloves.</summary>
public class WearableTests
{
    private sealed class FixedDice(int value) : IDiceRoller
    {
        public int Roll(int sides) => Math.Min(value, sides);
    }

    private static readonly IDiceRoller Twos = new FixedDice(2);
    private static readonly CyberwareOperations Cyber = new(Rules, new CyberwareInstaller(Rules, Twos), Calculator, Twos);
    private static readonly InventoryOperations Inventory = new(Rules, Cyber);
    private static readonly WearableOperations Wearables = new(Rules, Inventory);

    private static CharacterSheet Chromeless(int balance = 20_000) => CompletePackage() with
    {
        Creation = CompletePackage().Creation with { Finalized = true },
        Wallet = new Wallet { Balance = balance },
    };

    private static CharacterChange Change(CharacterSheet s) => new(s, Catalog, DateTime.UtcNow);

    /// <summary>Buys a host and puts it on.</summary>
    private static (CharacterSheet Sheet, string Host) Wearing(CharacterSheet sheet, CatalogItem host)
    {
        sheet = Inventory.Purchase(sheet, new PurchaseRequest(0, host.Id), Change(sheet));
        var id = sheet.Inventory.Last(e => e.ItemId == host.Id).InstanceId;
        sheet = Inventory.Equip(sheet, new EquipRequest(0, id, ItemState.Equipped), Change(sheet));
        return (sheet, id);
    }

    private static CharacterSheet Put(CharacterSheet sheet, string host, CatalogItem option) =>
        Wearables.Install(sheet, new WearableInstallRequest(0, host, option.Id), Change(sheet));

    private static Func<CharacterSheet> Trying(CharacterSheet sheet, string host, CatalogItem option) => () => Put(sheet, host, option);

    [Fact]
    public void Smart_glasses_take_cybereye_options_without_a_cybereye_and_without_humanity_loss()
    {
        var (sheet, glasses) = Wearing(Chromeless(), SmartGlasses);
        sheet = Put(sheet, glasses, ImageEnhance);

        sheet.Cyberware.Should().BeEmpty("nada foi implantado");
        sheet.Wallet.Balance.Should().Be(20_000 - 500 - 500);
        var report = Calculator.Calculate(sheet, Catalog);
        report.Humanity.Current.Should().Be(60);
        report.Wearables.Single().Should().Match<WearableLine>(w => w.Slots == 2 && w.SlotsUsed == 1 && w.Options.Count == 1);
        report.CyberwareEffects.Should().Contain(e => e.Item.Contains("image-enhance"));
    }

    [Fact]
    public void Options_only_work_while_the_host_is_worn()
    {
        var (sheet, glasses) = Wearing(Chromeless(), SmartGlasses);
        sheet = Put(sheet, glasses, ImageEnhance);
        sheet = Inventory.Equip(sheet, new EquipRequest(0, glasses, ItemState.Carried), Change(sheet));

        Calculator.Calculate(sheet, Catalog).CyberwareEffects.Should().NotContain(e => e.Item.Contains("image-enhance"));
    }

    [Fact]
    public void A_paired_option_takes_one_purchase_in_smart_glasses_but_is_refused_by_a_smart_lens()
    {
        var (sheet, glasses) = Wearing(Chromeless(), SmartGlasses);
        sheet = Put(sheet, glasses, LowLight);
        sheet.Wallet.Balance.Should().Be(20_000 - 500 - 500, "conta como par e custa uma instalação (CP:R p.354)");

        var (lensSheet, lens) = Wearing(Chromeless(), SmartLens);
        Trying(lensSheet, lens, LowLight).Should().Throw<RequestValidationException>().Which.Errors["itemId"][0].Should().Contain("par");
    }

    [Fact]
    public void Slots_kind_and_duplicates_are_checked()
    {
        var (sheet, lens) = Wearing(Chromeless(), SmartLens);
        sheet = Put(sheet, lens, Microoptics);

        Trying(sheet, lens, Teleoptics).Should().Throw<RequestValidationException>().Which.Errors["itemId"][0].Should().Contain("Option Slots");
        Trying(sheet, lens, Wolvers).Should().Throw<RequestValidationException>().Which.Errors["itemId"][0].Should().Contain("Cybereye");
        var (glassesSheet, glasses) = Wearing(Chromeless(), SmartGlasses);
        glassesSheet = Put(glassesSheet, glasses, Microoptics);
        Trying(glassesSheet, glasses, Microoptics).Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void A_battleglove_holds_arm_options_and_covers_the_arm_it_is_worn_on()
    {
        var sheet = Chromeless();
        sheet = Cyber.Install(sheet, new InstallCyberwareRequest(0, Cyberarm.Id, null, false, null, false, Side: BodySide.Left), Change(sheet));
        sheet = Cyber.Install(sheet, new InstallCyberwareRequest(0, Wolvers.Id, null, false, null, false, Side: BodySide.Left), Change(sheet));
        var (worn, glove) = Wearing(sheet, Battleglove);
        worn = Put(worn, glove, MedscannerArm);
        worn = Wearables.SetSide(worn, new WearableSideRequest(0, glove, BodySide.Left), Change(worn));

        var report = Calculator.Calculate(worn, Catalog);
        report.Cyberware.Single(c => c.Slug == "wolvers").CoveredBy.Should().Be("battleglove");
        report.Cyberware.Single(c => c.Slug == "cyberarm").CoveredBy.Should().BeNull();
        report.CyberwareEffects.Should().Contain(e => e.Item.Contains("medscanner-integrated"), "a Option da luva funciona");
        report.Wearables.Single().Covered.Should().Equal("wolvers");

        var right = Wearables.SetSide(worn, new WearableSideRequest(0, glove, BodySide.Right), Change(worn));
        Calculator.Calculate(right, Catalog).Cyberware.Should().OnlyContain(c => c.CoveredBy == null);
    }

    [Fact]
    public void Finger_options_keep_working_under_a_cyberdude_glove()
    {
        var sheet = Chromeless();
        sheet = Cyber.Install(sheet, new InstallCyberwareRequest(0, Cyberarm.Id, null, false, null, false, Side: BodySide.Left), Change(sheet));
        sheet = Cyber.Install(sheet, new InstallCyberwareRequest(0, Scratchers.Id, null, false, null, false, Side: BodySide.Left), Change(sheet));
        sheet = Cyber.Install(sheet, new InstallCyberwareRequest(0, Wolvers.Id, null, false, null, false, Side: BodySide.Left), Change(sheet));
        var (worn, glove) = Wearing(sheet, CyberDudeGlove);
        worn = Wearables.SetSide(worn, new WearableSideRequest(0, glove, BodySide.Left), Change(worn));

        var report = Calculator.Calculate(worn, Catalog);
        report.Cyberware.Single(c => c.Slug == "scratchers").CoveredBy.Should().BeNull();
        report.Cyberware.Single(c => c.Slug == "wolvers").CoveredBy.Should().Be("cyberdude-smart-glove");
        report.Wearables.Single().BuiltIn.Should().Equal("subdermal-grip");
    }

    [Fact]
    public void Taking_an_option_out_keeps_it_in_the_inventory_and_selling_the_host_frees_its_options()
    {
        var (sheet, glasses) = Wearing(Chromeless(), SmartGlasses);
        sheet = Put(sheet, glasses, ImageEnhance);
        var option = sheet.Inventory.Single(e => e.ItemId == ImageEnhance.Id).InstanceId;

        var removed = Wearables.Remove(sheet, new WearableOptionRequest(0, glasses, option), Change(sheet));
        removed.Inventory.Single(e => e.InstanceId == option).State.Should().Be(ItemState.Carried);
        removed.Inventory.Single(e => e.InstanceId == glasses).Wearable!.Options.Should().BeEmpty();

        var sold = EconomyOperations.RemoveItem(sheet, new RemoveItemRequest(0, glasses, null, 100, "vendido ao Fixer"), Change(sheet));
        sold.Inventory.Should().NotContain(e => e.InstanceId == glasses);
        sold.Inventory.Single(e => e.InstanceId == option).State.Should().Be(ItemState.Carried);
    }

    [Fact]
    public void Every_wearable_host_and_built_in_option_is_in_the_catalog_with_its_page()
    {
        var seed = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seed", "01-compendium.json")));
        var slugs = seed.RootElement.EnumerateArray().Select(e => e.GetProperty("slug").GetString()).ToHashSet();
        var hosts = Rules.Document.Cyberware.Wearables.Value;

        hosts.Select(h => h.Slug).Should().Contain(["smart-glasses", "smart-lens", "battleglove", "smart-ears"]);
        hosts.Should().OnlyContain(h => slugs.Contains(h.Slug) && h.Source.Length > 0 && h.Slots > 0);
        hosts.SelectMany(h => h.BuiltIn).Concat(Rules.Document.Cyberware.FingerOptions.Value).Should().OnlyContain(s => slugs.Contains(s));
        Rules.Document.Cyberware.WearableHumanityLoss.Complementary.Should().BeTrue();
    }

    [Fact]
    public void An_option_in_a_host_cannot_be_implanted_until_taken_out()
    {
        var (sheet, glasses) = Wearing(Chromeless(), SmartGlasses);
        sheet = Put(sheet, glasses, ImageEnhance);
        var option = sheet.Inventory.Single(e => e.ItemId == ImageEnhance.Id).InstanceId;

        var act = () => Cyber.Install(sheet, new InstallCyberwareRequest(0, null, option, false, null, false), Change(sheet));
        act.Should().Throw<RequestValidationException>().Which.Errors["fromInventory"][0].Should().Contain("retire");
    }
}
