using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Application.Npcs.Generation;
using CyberpunkDb.Application.Npcs.Rules;
using CyberpunkDb.Application.Npcs.Validation;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Tests.Unit.Npcs;

/// <summary>Small hand-built catalog (Core Book prices) so validator and generator tests control every number.</summary>
internal static class NpcTestData
{
    public static readonly NpcRuleBook Rules = NpcRuleBook.LoadEmbedded();
    public static readonly NpcValidator Validator = new(Rules);
    public static readonly NpcGenerator Generator = new(Rules, Validator);

    private const string Core = "Core Book";
    private static int _id;
    private static readonly List<CatalogItem> Items = [];

    // Weapons
    public static readonly CatalogItem MediumPistol = Weapon("medium-pistol", 50, WeaponClass.MediumPistol, WeaponSkill.Handgun, [Caliber.MediumPistol]);
    public static readonly CatalogItem HeavyPistol = Weapon("heavy-pistol", 100, WeaponClass.HeavyPistol, WeaponSkill.Handgun, [Caliber.HeavyPistol]);
    public static readonly CatalogItem VeryHeavyPistol = Weapon("very-heavy-pistol", 100, WeaponClass.VeryHeavyPistol, WeaponSkill.Handgun, [Caliber.VeryHeavyPistol]);
    public static readonly CatalogItem Smg = Weapon("smg", 100, WeaponClass.Smg, WeaponSkill.Handgun, [Caliber.MediumPistol]);
    public static readonly CatalogItem AssaultRifle = Weapon("assault-rifle", 500, WeaponClass.AssaultRifle, WeaponSkill.ShoulderArms, [Caliber.Rifle]);
    public static readonly CatalogItem SniperRifle = Weapon("sniper-rifle", 500, WeaponClass.SniperRifle, WeaponSkill.ShoulderArms, [Caliber.Rifle]);
    public static readonly CatalogItem Shotgun = Weapon("shotgun", 500, WeaponClass.Shotgun, WeaponSkill.ShoulderArms, [Caliber.Slug, Caliber.ShotgunShell]);
    public static readonly CatalogItem LightMelee = Weapon("light-melee-weapon", 50, WeaponClass.LightMelee, WeaponSkill.MeleeWeapon, []);
    public static readonly CatalogItem MediumMelee = Weapon("medium-melee-weapon", 50, WeaponClass.MediumMelee, WeaponSkill.MeleeWeapon, []);
    public static readonly CatalogItem HeavyMelee = Weapon("heavy-melee-weapon", 100, WeaponClass.HeavyMelee, WeaponSkill.MeleeWeapon, []);
    public static readonly CatalogItem VeryHeavyMelee = Weapon("very-heavy-melee-weapon", 500, WeaponClass.VeryHeavyMelee, WeaponSkill.MeleeWeapon, [], minBody: 6);
    public static readonly CatalogItem Malorian = Weapon("malorian-arms-3516", 10000, WeaponClass.VeryHeavyPistol, WeaponSkill.Handgun, [Caliber.VeryHeavyPistol], WeaponQuality.Excellent, exotic: true);
    public static readonly CatalogItem ExcellentPistol = Weapon("excellent-heavy-pistol", 500, WeaponClass.HeavyPistol, WeaponSkill.Handgun, [Caliber.HeavyPistol], WeaponQuality.Excellent);
    public static readonly CatalogItem StunGun = Weapon("stun-gun", 100, WeaponClass.HeavyPistol, WeaponSkill.Handgun, [Caliber.HeavyPistol], exotic: true);

    // Armor
    public static readonly CatalogItem Leathers = Armor("leathers", 20, 4);
    public static readonly CatalogItem Kevlar = Armor("kevlar", 50, 7);
    public static readonly CatalogItem LightArmorjack = Armor("light-armorjack", 100, 11);
    public static readonly CatalogItem MediumArmorjack = Armor("medium-armorjack", 100, 12, penalty: 2);
    public static readonly CatalogItem HeavyArmorjack = Armor("heavy-armorjack", 500, 13, penalty: 2);
    public static readonly CatalogItem Flak = Armor("flak-armor", 500, 15, penalty: 4);
    public static readonly CatalogItem Shield = Add(new CatalogItem(++_id, "bulletproof-shield", "Bulletproof Shield", ItemCategory.Armor, 100, Core, 350,
        Armor: new ArmorInfo(ArmorType.Shield, null, 0, 0, 0, null, false, null)));
    public static readonly CatalogItem ImplantedFrame = Add(new CatalogItem(++_id, "implanted-linear-frame-sigma", "Implanted Linear Frame Sigma", ItemCategory.Armor, 1000, Core, 367,
        Armor: new ArmorInfo(ArmorType.LinearFrame, null, 0, 0, 0, 12, true, "BODY 6 e Grafted Muscle and Bone Lace"),
        Cyber: new CyberInfo(CyberwareType.Borgware, BodyLocation.FullBody, false, null, null, null, false, InstallLocation.Hospital, "BODY 6 e Grafted Muscle and Bone Lace")));

    // Ammo and gear
    public static readonly CatalogItem BasicAmmo = Add(new CatalogItem(++_id, "basic-ammo", "Basic Ammo", ItemCategory.Ammunition, 10, Core, 344,
        Ammo: new AmmoInfo(AmmoKind.Round, AmmoEffect.Lethal, [Caliber.MediumPistol, Caliber.HeavyPistol, Caliber.VeryHeavyPistol, Caliber.Rifle, Caliber.Slug, Caliber.ShotgunShell], 10)));
    public static readonly CatalogItem Agent = GearItem("agent", 100);
    public static readonly CatalogItem TechBag = GearItem("tech-bag", 500);
    public static readonly CatalogItem Techtool = GearItem("techtool", 100);
    public static readonly CatalogItem CyberdeckStandard = GearItem("cyberdeck-standard", 500);
    public static readonly CatalogItem CyberdeckPoor = GearItem("cyberdeck-poor", 100);
    public static readonly CatalogItem Phone = GearItem("disposable-cellphone", 50);
    public static readonly CatalogItem Flashlight = GearItem("flashlight", 20);

    // Cyberware
    public static readonly CatalogItem NeuralLink = Cyber("neural-link", 500, CyberwareType.Neuralware, BodyLocation.NervousSystem, foundational: true, provides: 5, install: InstallLocation.Clinic);
    public static readonly CatalogItem Kerenzikov = Cyber("kerenzikov", 500, CyberwareType.Neuralware, BodyLocation.NervousSystem, requires: NeuralLink, slots: 1);
    public static readonly CatalogItem Sandevistan = Cyber("sandevistan", 500, CyberwareType.Neuralware, BodyLocation.NervousSystem, requires: NeuralLink, slots: 1);
    public static readonly CatalogItem InterfacePlugs = Cyber("interface-plugs", 500, CyberwareType.Neuralware, BodyLocation.NervousSystem, requires: NeuralLink, slots: 1);
    public static readonly CatalogItem ChipwareSocket = Cyber("chipware-socket", 500, CyberwareType.Neuralware, BodyLocation.NervousSystem, foundational: true, requires: NeuralLink, slots: 0);
    public static readonly CatalogItem PainEditor = Cyber("pain-editor", 1000, CyberwareType.Neuralware, BodyLocation.NervousSystem, requires: ChipwareSocket);
    public static readonly CatalogItem Cybereye = Cyber("cybereye", 100, CyberwareType.Cyberoptics, BodyLocation.Eyes, foundational: true, provides: 3, install: InstallLocation.Clinic);
    public static readonly CatalogItem TargetingScope = Cyber("targeting-scope", 500, CyberwareType.Cyberoptics, BodyLocation.Eyes, requires: Cybereye, slots: 1, install: InstallLocation.Mall);
    public static readonly CatalogItem LowLight = Cyber("low-light-infrared-uv", 500, CyberwareType.Cyberoptics, BodyLocation.Eyes, requires: Cybereye, slots: 2, pair: true, install: InstallLocation.Mall, requirements: "Mall (Requer Par)");
    public static readonly CatalogItem Microoptics = Cyber("microoptics", 100, CyberwareType.Cyberoptics, BodyLocation.Eyes, requires: Cybereye, slots: 1, install: InstallLocation.Mall);
    public static readonly CatalogItem Teleoptics = Cyber("teleoptics", 500, CyberwareType.Cyberoptics, BodyLocation.Eyes, requires: Cybereye, slots: 1, install: InstallLocation.Mall);
    public static readonly CatalogItem CyberaudioSuite = Cyber("cyberaudio-suite", 500, CyberwareType.Cyberaudio, BodyLocation.Ears, foundational: true, provides: 3, install: InstallLocation.Clinic);
    public static readonly CatalogItem AmplifiedHearing = Cyber("amplified-hearing", 100, CyberwareType.Cyberaudio, BodyLocation.Ears, requires: CyberaudioSuite, slots: 1);
    public static readonly CatalogItem Cyberarm = Cyber("cyberarm", 500, CyberwareType.Cyberlimb, BodyLocation.Arm, foundational: true, provides: 4, install: InstallLocation.Hospital);
    public static readonly CatalogItem Wolvers = Cyber("wolvers", 500, CyberwareType.LimbOption, BodyLocation.Arm, requires: Cyberarm);
    public static readonly CatalogItem BigKnucks = Cyber("big-knucks", 100, CyberwareType.LimbOption, BodyLocation.Arm, requires: Cyberarm);
    public static readonly CatalogItem SubdermalArmor = Cyber("subdermal-armor", 1000, CyberwareType.ExternalBody, BodyLocation.Skin, install: InstallLocation.Hospital);
    public static readonly CatalogItem SkinWeave = Cyber("skin-weave", 500, CyberwareType.ExternalBody, BodyLocation.Skin, install: InstallLocation.Hospital);
    public static readonly CatalogItem HiddenHolster = Cyber("hidden-holster", 500, CyberwareType.ExternalBody, BodyLocation.Skin);
    public static readonly CatalogItem GraftedMuscle = Cyber("grafted-muscle-and-bone-lace", 1000, CyberwareType.InternalBody, BodyLocation.Internal, install: InstallLocation.Hospital);
    public static readonly CatalogItem NasalFilters = Cyber("nasal-filters", 100, CyberwareType.InternalBody, BodyLocation.Internal);
    public static readonly CatalogItem ToxinBinders = Cyber("toxin-binders", 100, CyberwareType.InternalBody, BodyLocation.Internal);
    public static readonly CatalogItem CombatJaw = Cyber("combat-jaw", 500, CyberwareType.ExternalBody, BodyLocation.Head, slots: 2);
    public static readonly CatalogItem DragoonPlating = Cyber("dragoon-plating", 5000, CyberwareType.Borgware, BodyLocation.FullBody, install: InstallLocation.Hospital, requirements: "BODY 16 ou Full Body Conversion; Hospital");
    public static readonly CatalogItem Chemskin = Fashion("chemskin", 100);
    public static readonly CatalogItem Techhair = Fashion("techhair", 100);
    public static readonly CatalogItem LightTattoo = Fashion("light-tattoo", 100);
    public static readonly CatalogItem EmpThreading = Fashion("emp-threading", 10);
    public static readonly CatalogItem Biomonitor = Fashion("biomonitor", 100);

    public static readonly NpcCatalog Catalog = new(Items);

    // ------------------------------------------------------------ sheets

    /// <summary>
    /// A hand-built Mook that respects every limit: 31/33 STAT points, 30/30 skill points, 220/300eb of gear
    /// and 500/500eb of cyberware.
    /// </summary>
    public static NpcSheet ValidMook() => new()
    {
        Name = "Pitbull",
        Level = NpcLevel.Mook,
        NativeLanguage = "English",
        Home = "Heywood",
        Stats = Stats(("INT", 3), ("REF", 6), ("DEX", 5), ("TECH", 2), ("COOL", 4), ("WILL", 4), ("MOVE", 3), ("BODY", 2), ("EMP", 2)),
        Skills = Skills(("handgun", 5), ("evasion", 7), ("streetwise", 5), ("perception", 6), ("brawling", 4), ("stealth", 4), ("athletics", 2),
            ("interrogation", 3), ("drive-land-vehicle", 4)),
        KeySkills = ["handgun", "evasion", "streetwise", "perception"],
        Gear =
        [
            new NpcGearEntry { ItemId = HeavyPistol.Id, Quality = WeaponQuality.Poor },
            new NpcGearEntry { ItemId = Kevlar.Id, Worn = ArmorSlot.Body },
            new NpcGearEntry { ItemId = Kevlar.Id, Worn = ArmorSlot.Head },
            new NpcGearEntry { ItemId = BasicAmmo.Id, Quantity = 2 },
            new NpcGearEntry { ItemId = Phone.Id },
        ],
        Cyberware =
        [
            new NpcCyberwareEntry { InstanceId = "eye1", ItemId = Cybereye.Id },
            new NpcCyberwareEntry { InstanceId = "micro", ItemId = Microoptics.Id, ParentInstanceId = "eye1" },
            new NpcCyberwareEntry { InstanceId = "skin", ItemId = Chemskin.Id },
            new NpcCyberwareEntry { InstanceId = "hair", ItemId = Techhair.Id },
            new NpcCyberwareEntry { InstanceId = "bio", ItemId = Biomonitor.Id },
        ],
    };

    public static Dictionary<string, int> Stats(params (string Key, int Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, int> Skills(params (string Key, int Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    // ------------------------------------------------------------ builders

    private static CatalogItem Add(CatalogItem item)
    {
        Items.Add(item);
        return item;
    }

    private static CatalogItem Weapon(string slug, int price, WeaponClass cls, WeaponSkill skill, Caliber[] calibers,
        WeaponQuality quality = WeaponQuality.Standard, bool exotic = false, int? minBody = null) =>
        Add(new CatalogItem(++_id, slug, Title(slug), ItemCategory.Weapon, price, Core, 341,
            Weapon: new WeaponInfo(cls, skill, quality, exotic, minBody, "3d6", 2, 1, true, 8, calibers)));

    private static CatalogItem Armor(string slug, int price, int sp, int penalty = 0) =>
        Add(new CatalogItem(++_id, slug, Title(slug), ItemCategory.Armor, price, Core, 350,
            Armor: new ArmorInfo(ArmorType.BodyArmor, sp, penalty, penalty, penalty, null, false, null)));

    private static CatalogItem GearItem(string slug, int price) =>
        Add(new CatalogItem(++_id, slug, Title(slug), ItemCategory.Gear, price, Core, 352, Gear: new GearInfo(GearType.Tool, null)));

    private static CatalogItem Cyber(string slug, int price, CyberwareType type, BodyLocation location, bool foundational = false,
        int? provides = null, CatalogItem? requires = null, int? slots = null, bool pair = false,
        InstallLocation install = InstallLocation.Clinic, string? requirements = null) =>
        Add(new CatalogItem(++_id, slug, Title(slug), ItemCategory.Cyberware, price, Core, 359,
            Cyber: new CyberInfo(type, location, foundational, provides, slots, requires?.Id, pair, install, requirements ?? install.ToString())));

    private static CatalogItem Fashion(string slug, int price) =>
        Add(new CatalogItem(++_id, slug, Title(slug), ItemCategory.Cyberware, price, Core, 358,
            Cyber: new CyberInfo(CyberwareType.Fashionware, BodyLocation.Skin, false, null, null, null, false, InstallLocation.Mall, null)));

    private static string Title(string slug) =>
        string.Join(' ', slug.Split('-').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
}
