using CyberpunkDb.Application.Characters;
using CyberpunkDb.Application.Characters.Calculation;
using CyberpunkDb.Application.Characters.Rules;
using CyberpunkDb.Application.Characters.Validation;
using CyberpunkDb.Application.Npcs;
using CyberpunkDb.Domain.Enums;
using CyberpunkDb.Tests.Unit.Npcs;

namespace CyberpunkDb.Tests.Unit.Characters;

/// <summary>Hand-built catalog (Core Book values) and reference sheets for the three creation methods.</summary>
internal static class CharacterTestData
{
    public static readonly CharacterRuleBook Rules = CharacterRuleBook.LoadEmbedded(NpcTestData.Rules);
    public static readonly CharacterCalculator Calculator = new(Rules);
    public static readonly CharacterValidator Validator = new(Rules);

    private const string Core = "Core Book";
    private static int _id = 10_000;
    private static readonly List<CatalogItem> Items = [];

    public static readonly CatalogItem LightArmorjack = Armor("light-armorjack", 100, 11);
    public static readonly CatalogItem MediumArmorjack = Armor("medium-armorjack", 100, 12, penalty: 2);
    public static readonly CatalogItem HeavyArmorjack = Armor("heavy-armorjack", 500, 13, penalty: 2);
    public static readonly CatalogItem Flak = Armor("flak-armor", 500, 15, penalty: 4);
    public static readonly CatalogItem ImplantedFrame = Add(new CatalogItem(++_id, "implanted-linear-frame-sigma", "Implanted Linear Frame Sigma",
        ItemCategory.Armor, 1000, Core, 367,
        Armor: new ArmorInfo(ArmorType.LinearFrame, null, 0, 0, 0, 12, true, "BODY 6"),
        Cyber: new CyberInfo(CyberwareType.Borgware, BodyLocation.FullBody, false, null, null, null, false, InstallLocation.Hospital, "BODY 6", 14, "4d6")));
    public static readonly CatalogItem LeisureTop = Add(new CatalogItem(++_id, "roupa-leisurewear-top", "Leisurewear Top", ItemCategory.Armor, 20, Core, 102,
        Armor: new ArmorInfo(ArmorType.Clothing, null, 0, 0, 0, null, false, null)));
    public static readonly CatalogItem Agent = Add(new CatalogItem(++_id, "agent", "Agent", ItemCategory.Gear, 100, Core, 352, Gear: new GearInfo(GearType.Tool, null)));

    public static readonly CatalogItem NeuralLink = Cyber("neural-link", 500, CyberwareType.Neuralware, BodyLocation.NervousSystem, 7, "2d6", foundational: true, provides: 5);
    public static readonly CatalogItem Sandevistan = Cyber("sandevistan", 500, CyberwareType.Neuralware, BodyLocation.NervousSystem, 7, "2d6", requires: NeuralLink, slots: 1);
    public static readonly CatalogItem Biomonitor = Cyber("biomonitor", 100, CyberwareType.Fashionware, BodyLocation.Skin, 0, null);
    public static readonly CatalogItem SubdermalArmor = Cyber("subdermal-armor", 1000, CyberwareType.ExternalBody, BodyLocation.Skin, 14, "4d6");
    public static readonly CatalogItem DragoonPlating = Cyber("dragoon-plating", 5000, CyberwareType.Borgware, BodyLocation.FullBody, 14, "4d6");

    // Phase 4: limbs, options, pairs, FBC and weapon attachments
    public static readonly CatalogItem Kerenzikov = Cyber("kerenzikov", 500, CyberwareType.Neuralware, BodyLocation.NervousSystem, 14, "4d6", requires: NeuralLink, slots: 1);
    public static readonly CatalogItem BoneLace = Cyber("grafted-muscle-and-bone-lace", 1000, CyberwareType.InternalBody, BodyLocation.Internal, 7, "2d6");
    public static readonly CatalogItem MedscannerArm = Cyber("medscanner-integrated", 500, CyberwareType.LimbOption, BodyLocation.Arm, 7, "2d6");
    public static readonly CatalogItem Chemskin = Cyber("chemskin", 100, CyberwareType.Fashionware, BodyLocation.Skin, 0, null);
    public static readonly CatalogItem Techhair = Cyber("techhair", 100, CyberwareType.Fashionware, BodyLocation.Hair, 0, null);
    public static readonly CatalogItem Cybereye = Cyber("cybereye", 100, CyberwareType.Cyberoptics, BodyLocation.Eyes, 7, "2d6", foundational: true, provides: 3);
    public static readonly CatalogItem TargetingScope = Cyber("targeting-scope", 500, CyberwareType.Cyberoptics, BodyLocation.Eyes, 3, "1d6", requires: Cybereye, slots: 1);
    public static readonly CatalogItem Microoptics = Cyber("microoptics", 100, CyberwareType.Cyberoptics, BodyLocation.Eyes, 2, "1d6/2", requires: Cybereye, slots: 1);
    public static readonly CatalogItem Teleoptics = Cyber("teleoptics", 500, CyberwareType.Cyberoptics, BodyLocation.Eyes, 3, "1d6", requires: Cybereye, slots: 1);
    public static readonly CatalogItem ImageEnhance = Cyber("image-enhance", 500, CyberwareType.Cyberoptics, BodyLocation.Eyes, 3, "1d6", requires: Cybereye, slots: 1);
    public static readonly CatalogItem LowLight = Cyber("low-light-infrared-uv", 500, CyberwareType.Cyberoptics, BodyLocation.Eyes, 3, "1d6", requires: Cybereye, slots: 1, pair: true);
    public static readonly CatalogItem Cyberarm = Cyber("cyberarm", 500, CyberwareType.Cyberlimb, BodyLocation.Arm, 7, "2d6", foundational: true, provides: 4);
    public static readonly CatalogItem Cyberleg = Cyber("cyberleg", 100, CyberwareType.Cyberlimb, BodyLocation.Leg, 3, "1d6", foundational: true, provides: 3);
    public static readonly CatalogItem Wolvers = Cyber("wolvers", 500, CyberwareType.LimbOption, BodyLocation.Arm, 7, "2d6", requires: Cyberarm);
    public static readonly CatalogItem TalonFoot = Cyber("talon-foot", 500, CyberwareType.LimbOption, BodyLocation.Leg, 3, "1d6", requires: Cyberleg);
    public static readonly CatalogItem PlasticCovering = Cyber("plastic-covering", 100, CyberwareType.LimbOption, BodyLocation.Arm, 0, null, slots: 0);
    public static readonly CatalogItem RealskinnCovering = Cyber("realskinn-covering", 500, CyberwareType.LimbOption, BodyLocation.Arm, 0, null, slots: 0);
    public static readonly CatalogItem HardenedShielding = Cyber("hardened-shielding", 1000, CyberwareType.LimbOption, BodyLocation.FullBody, 3, "1d6");
    public static readonly CatalogItem HeavyPlating = Cyber("heavy-subdermal-plating", 5000, CyberwareType.Borgware, BodyLocation.FullBody, 14, "4d6",
        requirements: "BODY 10 ou Full Body Conversion; Hospital");
    public static readonly CatalogItem Biosystem = Cyber("biosystem", 10000, CyberwareType.Borgware, BodyLocation.FullBody, 14, "4d6");
    public static readonly CatalogItem FbcBody = Cyber("rocklin-augmentics-alpha-class", 9900, CyberwareType.Borgware, BodyLocation.FullBody, 21, null,
        requirements: "Biosystem; Hospital");

    public static readonly CatalogItem AssaultRifle = Add(new CatalogItem(++_id, "assault-rifle", "Assault Rifle", ItemCategory.Weapon, 500, Core, 91,
        Weapon: new WeaponInfo(WeaponClass.AssaultRifle, WeaponSkill.ShoulderArms, WeaponQuality.Standard, false, null, "5d6", 1, 2, false, 25, [Caliber.Rifle], 4)));
    public static readonly CatalogItem HeavyPistol = Add(new CatalogItem(++_id, "heavy-pistol", "Heavy Pistol", ItemCategory.Weapon, 100, Core, 91,
        Weapon: new WeaponInfo(WeaponClass.HeavyPistol, WeaponSkill.Handgun, WeaponQuality.Standard, false, null, "3d6", 2, 1, true, 8, [Caliber.HeavyPistol])));
    public static readonly CatalogItem ExoticPistol = Add(new CatalogItem(++_id, "stun-gun", "Stun Gun", ItemCategory.Weapon, 100, Core, 92,
        Weapon: new WeaponInfo(WeaponClass.HeavyPistol, WeaponSkill.Handgun, WeaponQuality.Standard, true, null, "3d6", 2, 1, true, 8, [Caliber.HeavyPistol])));
    public static readonly CatalogItem SmartgunLink = Attachment("smartgun-link", 500, WeaponAttachmentType.SmartLink, 2, false,
        [WeaponClass.HeavyPistol, WeaponClass.AssaultRifle]);
    public static readonly CatalogItem Underbarrel = Attachment("grenade-launcher-underbarrel", 500, WeaponAttachmentType.Underbarrel, 2, false,
        [WeaponClass.Shotgun, WeaponClass.AssaultRifle, WeaponClass.SniperRifle]);
    public static readonly CatalogItem SnipingScope = Attachment("sniping-scope", 100, WeaponAttachmentType.Scope, 1, false,
        [WeaponClass.HeavyPistol, WeaponClass.AssaultRifle]);
    public static readonly CatalogItem PistolAutosear = Attachment("pistol-autosear", 100, WeaponAttachmentType.Internals, 0, true, [WeaponClass.HeavyPistol]);

    // Wearable hosts: options worn in gear and gloves (CP:R p.347, 354; DGD p.148; IR5 p.99)
    public static readonly CatalogItem SmartGlasses = Wearable("smart-glasses", 500, GearType.Electronics);
    public static readonly CatalogItem SmartLens = Wearable("smart-lens", 500, GearType.Electronics);
    public static readonly CatalogItem Battleglove = Wearable("battleglove", 1000, GearType.Tool);
    public static readonly CatalogItem CyberDudeGlove = Wearable("cyberdude-smart-glove", 750, GearType.Tool);
    public static readonly CatalogItem SubdermalGrip = Cyber("subdermal-grip", 100, CyberwareType.LimbOption, BodyLocation.Arm, 3, "1d6", requires: NeuralLink, slots: 1);
    public static readonly CatalogItem Scratchers = Cyber("scratchers", 100, CyberwareType.LimbOption, BodyLocation.Hand, 2, "1d6/2", requires: Cyberarm, slots: 1);

    public static readonly NpcCatalog Catalog = new(Items);

    // ------------------------------------------------------------ reference sheets

    public static Dictionary<string, int> Stats(params (string Key, int Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, int> StatsFromRow(int[] row) =>
        Rules.StatOrder.Select((k, i) => (k, row[i])).ToDictionary(x => x.k, x => x.Item2, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Complete Package Solo inside every limit: STATs total 62; skills cost exactly 86 — ten Basic Skills at 2 (20),
    /// Perception/Evasion/Stealth at 6 (18), Autofire (x2) 6 (12), six more skills at 6 (36) — and English 4 is free.
    /// </summary>
    public static CharacterSheet CompletePackage() => new CharacterSheet
    {
        Identity = new CharacterIdentity { Name = "Jay", NativeLanguage = "English", Home = "Watson" },
        Roles = [new CharacterRole { Role = "Solo", Rank = 4 }],
        Creation = new CreationState { Method = CreationMethod.CompletePackage },
        Stats = Stats(("INT", 6), ("REF", 7), ("DEX", 7), ("TECH", 5), ("COOL", 6), ("WILL", 6), ("LUCK", 6), ("MOVE", 6), ("BODY", 7), ("EMP", 6)),
        Skills = BasicSkillsAt2(new()
        {
            ["language:English"] = 4,
            ["perception"] = 6, ["evasion"] = 6, ["stealth"] = 6,
            ["autofire"] = 6, ["handgun"] = 6, ["shoulder-arms"] = 6, ["melee-weapon"] = 6, ["tactics"] = 6, ["interrogation"] = 6,
            ["resist-torture-drugs"] = 6,
        }),
    }.Normalize();

    /// <summary>Streetrat Solo copied from row 1 of the Solo table (CP:R p.74) and the Solo template (CP:R p.86).</summary>
    public static CharacterSheet Streetrat(int row = 1) => new CharacterSheet
    {
        Identity = new CharacterIdentity { Name = "Rook", NativeLanguage = "Japanese", Home = "Kabuki" },
        Roles = [new CharacterRole { Role = "Solo", Rank = 4 }],
        Creation = new CreationState
        {
            Method = CreationMethod.Streetrat,
            TemplateRow = row,
            StartingChoices = new(StringComparer.OrdinalIgnoreCase) { ["weapons:2"] = 0, ["cyberware:2"] = 0 },
        },
        Stats = StatsFromRow(Rules.Role("Solo").StatTable.Rows[row - 1]),
        Skills = new Dictionary<string, int>(Rules.RoleSkillLevels("Solo", "Kabuki"), StringComparer.OrdinalIgnoreCase) { ["language:Japanese"] = 4 },
        Wallet = new Wallet { Balance = 500 },
    }.Normalize();

    /// <summary>
    /// Edgerunner Solo: every STAT rolled 3 (row 3 of the Solo table), the 20 Role skills at 4 except Handgun and
    /// Evasion at 5: 19 × 4 + Autofire (x2) 4 × 2 + 2 = 86.
    /// </summary>
    public static CharacterSheet Edgerunner()
    {
        var skills = Rules.RoleSkillLevels("Solo", "Watson").Keys.ToDictionary(k => k, _ => 4, StringComparer.OrdinalIgnoreCase);
        skills["handgun"] = 5;
        skills["evasion"] = 5;
        skills["language:Spanish"] = 4;
        return new CharacterSheet
        {
            Identity = new CharacterIdentity { Name = "Vex", NativeLanguage = "Spanish", Home = "Watson" },
            Roles = [new CharacterRole { Role = "Solo", Rank = 4 }],
            Creation = new CreationState
            {
                Method = CreationMethod.Edgerunner,
                StatRolls = Rules.StatOrder.ToDictionary(k => k, _ => 3, StringComparer.OrdinalIgnoreCase),
                StartingChoices = new(StringComparer.OrdinalIgnoreCase) { ["weapons:2"] = 1, ["cyberware:2"] = 0 },
            },
            Stats = StatsFromRow(Rules.Role("Solo").StatTable.Rows[2]),
            Skills = skills,
            Wallet = new Wallet { Balance = 500 },
        }.Normalize();
    }

    private static Dictionary<string, int> BasicSkillsAt2(Dictionary<string, int> extra)
    {
        var skills = Rules.BasicSkillKeys("Watson").ToDictionary(k => k, _ => 2, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in extra) skills[k] = v;
        return skills;
    }

    // ------------------------------------------------------------ builders

    private static CatalogItem Add(CatalogItem item)
    {
        Items.Add(item);
        return item;
    }

    private static CatalogItem Armor(string slug, int price, int sp, int penalty = 0) =>
        Add(new CatalogItem(++_id, slug, slug, ItemCategory.Armor, price, Core, 97,
            Armor: new ArmorInfo(ArmorType.BodyArmor, sp, penalty, penalty, penalty, null, false, null)));

    private static CatalogItem Cyber(string slug, int price, CyberwareType type, BodyLocation location, int hl, string? dice,
        bool foundational = false, int? provides = null, CatalogItem? requires = null, int? slots = null, bool pair = false, string? requirements = null) =>
        Add(new CatalogItem(++_id, slug, slug, ItemCategory.Cyberware, price, Core, 359,
            Cyber: new CyberInfo(type, location, foundational, provides, slots, requires?.Id, pair, InstallLocation.Clinic, requirements, hl, dice)));

    private static CatalogItem Wearable(string slug, int price, GearType type) =>
        Add(new CatalogItem(++_id, slug, slug, ItemCategory.Gear, price, Core, 354, Gear: new GearInfo(type, null)));

    private static CatalogItem Attachment(string slug, int price, WeaponAttachmentType type, int slots, bool exotic, WeaponClass[] classes) =>
        Add(new CatalogItem(++_id, slug, slug, ItemCategory.WeaponAttachment, price, Core, 344,
            Attachment: new AttachmentInfo(type, slots, exotic, classes, null)));
}
