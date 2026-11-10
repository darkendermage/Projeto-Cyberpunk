using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Operations;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using FluentAssertions;
using static CyberpunkDb.Tests.Unit.Characters.CharacterTestData;

namespace CyberpunkDb.Tests.Unit.Characters;

// The sheet side of weapon-rules.json: extra magazines (shells, batteries), modes that stay on, destroyed weapons.
public class WeaponModeSheetTests
{
    private sealed class FixedDice(int value) : IDiceRoller
    {
        public int Roll(int sides) => Math.Min(value, sides);
    }

    private static readonly IDiceRoller Twos = new FixedDice(2);
    private static readonly CyberwareOperations Cyber = new(Rules, new CyberwareInstaller(Rules, Twos), Calculator, Twos);
    private static readonly InventoryOperations Inventory = new(Rules, Cyber);

    private static readonly CatalogItem Tommyknocker = new(90_001, "tommyknocker", "Tommyknocker", ItemCategory.Weapon, 1000, "Black Chrome", 123,
        Weapon: new WeaponInfo(WeaponClass.VeryHeavyPistol, WeaponSkill.Handgun, WeaponQuality.Standard, true, 10, "4d6 / 5d6", 1, 1, false, 8,
            [Caliber.VeryHeavyPistol, Caliber.Slug, Caliber.ShotgunShell]));
    private static readonly CatalogItem PublicDefender = new(90_002, "e-tack-public-defender", "E-TACK Public Defender", ItemCategory.Weapon, 500, "Black Chrome", 115,
        Weapon: new WeaponInfo(WeaponClass.HeavyPistol, WeaponSkill.Handgun, WeaponQuality.Poor, true, null, "3d6", 2, 1, true, 8, [Caliber.HeavyPistol]));
    private static readonly CatalogItem MicroCutie = new(90_003, "micro-cutie-sanroo", "MicroCutie", ItemCategory.Weapon, 500, "Danger Gal Dossier", 145,
        Weapon: new WeaponInfo(WeaponClass.MediumPistol, WeaponSkill.Handgun, WeaponQuality.Excellent, true, null, "2d6 / 4d6", 2, 1, true, 10, [Caliber.MediumPistol]));
    private static readonly CatalogItem Shells = new(90_004, "basic-ammo-shell", "Basic Shotgun Shells", ItemCategory.Ammunition, 10, "Core Book", 344,
        Ammo: new AmmoInfo(AmmoKind.Round, AmmoEffect.Lethal, [Caliber.ShotgunShell], 10));
    private static readonly CatalogItem PistolRounds = new(90_005, "basic-ammo-vhp", "Basic VHP Ammo", ItemCategory.Ammunition, 10, "Core Book", 344,
        Ammo: new AmmoInfo(AmmoKind.Round, AmmoEffect.Lethal, [Caliber.VeryHeavyPistol], 10));

    private static readonly NpcCatalog Items = new([.. Catalog.Items, Tommyknocker, PublicDefender, MicroCutie, Shells, PistolRounds]);

    private static CharacterSheet Sheet() => CompletePackage() with
    {
        Inventory =
        [
            new InventoryEntry { InstanceId = "tk", ItemId = Tommyknocker.Id, State = ItemState.Equipped },
            new InventoryEntry { InstanceId = "pd", ItemId = PublicDefender.Id, State = ItemState.Equipped },
            new InventoryEntry { InstanceId = "mc", ItemId = MicroCutie.Id, State = ItemState.Equipped },
            new InventoryEntry { InstanceId = "sh", ItemId = Shells.Id, Caliber = Caliber.ShotgunShell, Rounds = 10 },
            new InventoryEntry { InstanceId = "vhp", ItemId = PistolRounds.Id, Caliber = Caliber.VeryHeavyPistol, Rounds = 20 },
        ],
    };

    private static CharacterChange Change(CharacterSheet sheet) => new(sheet, Items, DateTime.UtcNow);

    [Fact]
    public void Extra_magazine_takes_its_own_ammo_and_the_main_one_skips_it()
    {
        var sheet = Sheet();
        sheet = Inventory.Reload(sheet, new ReloadRequest(0, "tk", Magazine: "shotgun"), Change(sheet));
        sheet = Inventory.Reload(sheet, new ReloadRequest(0, "tk"), Change(sheet));

        var tk = sheet.Inventory.Single(e => e.InstanceId == "tk").Weapon!;
        tk.Magazines["shotgun"].Should().Be(2);
        tk.LoadedRounds.Should().Be(8);
        tk.LoadedAmmoItemId.Should().Be(PistolRounds.Id, "shells belong to the shotgun half");
        sheet.Inventory.Single(e => e.InstanceId == "sh").Rounds.Should().Be(8);

        var line = Calculator.Calculate(sheet.Normalize(), Items).Weapons.Single(w => w.InstanceId == "tk");
        line.ExtraMagazines.Should().ContainSingle(m => m.Id == "shotgun" && m.Rounds == 2 && m.Capacity == 2);
    }

    [Fact]
    public void Firing_from_an_extra_magazine_leaves_the_main_one_alone()
    {
        var sheet = Sheet();
        sheet = Inventory.Reload(sheet, new ReloadRequest(0, "tk", Magazine: "shotgun"), Change(sheet));
        sheet = Inventory.Reload(sheet, new ReloadRequest(0, "tk"), Change(sheet));

        sheet = Inventory.Fire(sheet, new FireRequest(0, "tk", FireMode.Single, 1, "shotgun"), Change(sheet));

        var tk = sheet.Inventory.Single(e => e.InstanceId == "tk").Weapon!;
        tk.Magazines["shotgun"].Should().Be(1);
        tk.LoadedRounds.Should().Be(8);
    }

    [Fact]
    public void Battery_starts_charged_and_recharges_without_items()
    {
        var sheet = Sheet();
        sheet = Inventory.Fire(sheet, new FireRequest(0, "pd", FireMode.Single, 1, "battery"), Change(sheet));
        sheet.Inventory.Single(e => e.InstanceId == "pd").Weapon!.Magazines["battery"].Should().Be(7);

        sheet = Inventory.Reload(sheet, new ReloadRequest(0, "pd", Magazine: "battery"), Change(sheet));
        sheet.Inventory.Single(e => e.InstanceId == "pd").Weapon!.Magazines["battery"].Should().Be(8);

        var full = () => Inventory.Reload(sheet, new ReloadRequest(0, "pd", Magazine: "battery"), Change(sheet));
        full.Should().Throw<RequestValidationException>();
    }

    [Fact]
    public void Switched_mode_is_saved_on_the_weapon()
    {
        var sheet = Sheet();
        sheet = InventoryOperations.SetMode(sheet, "mc", "hypurrburst", Change(sheet));

        sheet.Inventory.Single(e => e.InstanceId == "mc").Weapon!.Mode.Should().Be("hypurrburst");
        var line = Calculator.Calculate(sheet.Normalize(), Items).Weapons.Single(w => w.InstanceId == "mc");
        line.Mode.Should().Be("hypurrburst");
        line.SwitchModes!.Select(m => m.Id).Should().Equal("normal", "hypurrburst");

        var free = () => InventoryOperations.SetMode(sheet, "tk", "shotgun", Change(sheet));
        free.Should().Throw<RequestValidationException>("Tommyknocker modes are picked on each shot");
    }

    [Fact]
    public void Destroyed_weapon_can_be_marked_as_repaired()
    {
        var sheet = Sheet();
        sheet = InventoryOperations.SetDestroyed(sheet, "pd", true, Change(sheet));
        Calculator.Calculate(sheet.Normalize(), Items).Weapons.Single(w => w.InstanceId == "pd").Destroyed.Should().BeTrue();

        sheet = InventoryOperations.SetDestroyed(sheet, "pd", false, Change(sheet));
        sheet.Inventory.Single(e => e.InstanceId == "pd").Weapon!.Destroyed.Should().BeFalse();
    }
}
