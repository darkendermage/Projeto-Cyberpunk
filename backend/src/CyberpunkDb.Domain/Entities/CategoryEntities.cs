using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

public class Weapon() : Item(ItemCategory.Weapon)
{
    public WeaponClass WeaponClass { get; set; }
    public WeaponSkill Skill { get; set; }
    // as printed ("3d6", "3d6 / 5d6")
    public string? Damage { get; set; }
    // d6 count of the main damage, for the numeric filter
    public int? DamageDice { get; set; }
    public int? Rof { get; set; }
    // x3, x4...; null = no autofire
    public int? AutofireMultiplier { get; set; }
    public int Hands { get; set; } = 1;
    public bool Concealable { get; set; }
    public int? MagazineCapacity { get; set; }
    public WeaponQuality Quality { get; set; } = WeaponQuality.Standard;
    public bool IsExotic { get; set; }
    // in meters, 0 = melee
    public int? MaxRangeMeters { get; set; }
    public int? MinBody { get; set; }
    public List<WeaponCaliber> Calibers { get; set; } = [];
}

public class WeaponCaliber
{
    public int WeaponId { get; set; }
    public Caliber Caliber { get; set; }
}

public class Ammunition() : Item(ItemCategory.Ammunition)
{
    public AmmoKind Kind { get; set; }
    public AmmoEffect EffectType { get; set; }
    public string? Effect { get; set; }
    public int? QuantityPerPack { get; set; }
    public string? Damage { get; set; }
    public int? DamageDice { get; set; }
    public string? AreaOfEffect { get; set; }
    public int? ResistDv { get; set; }
    public string? Requirements { get; set; }
    public List<AmmunitionCaliber> Calibers { get; set; } = [];
}

public class AmmunitionCaliber
{
    public int AmmunitionId { get; set; }
    public Caliber Caliber { get; set; }
}

public class GearItem() : Item(ItemCategory.Gear)
{
    public GearType GearType { get; set; }
    // e.g. program class (Attacker, Booster) or Black ICE class
    public string? Subtype { get; set; }
    public decimal? WeightKg { get; set; }
    public string? Requirements { get; set; }
    public string? Effect { get; set; }
    public string? SkillBonus { get; set; }
}

public class ArmorItem() : Item(ItemCategory.Armor)
{
    public ArmorType ArmorType { get; set; }
    // armor, ACPA and a few suits
    public int? StoppingPower { get; set; }
    public int? ShieldHp { get; set; }
    public int? RefPenalty { get; set; }
    public int? DexPenalty { get; set; }
    public int? MovePenalty { get; set; }
    // Linear Frames / ACPA
    public int? BodyValue { get; set; }
    // ACPA structural HP
    public int? HitPoints { get; set; }
    public int? HumanityLoss { get; set; }
    public string? HumanityLossDice { get; set; }
    public InstallLocation? InstallLocation { get; set; }
    public ClothingSlot? ClothingSlot { get; set; }
    public ClothingStyle? ClothingStyle { get; set; }
    public string? ProtectedLocations { get; set; }
    public string? Requirements { get; set; }
    public string? Effect { get; set; }
}

public class CyberwareItem() : Item(ItemCategory.Cyberware)
{
    public CyberwareType CyberwareType { get; set; }
    public BodyLocation BodyLocation { get; set; }
    // pieces with option slots (Cybereye, Neural Link...)
    public bool IsFoundational { get; set; }
    public int? SlotsProvided { get; set; }
    public int? SlotsRequired { get; set; }
    public int? HumanityLoss { get; set; }
    public string? HumanityLossDice { get; set; }
    public InstallLocation InstallLocation { get; set; }
    public bool RequiresPair { get; set; }
    public int? RequiresItemId { get; set; }
    public CyberwareItem? RequiresItem { get; set; }
    public string? Requirements { get; set; }
    public string? Effect { get; set; }
}

public class WeaponAttachmentClass
{
    public int WeaponAttachmentId { get; set; }
    public WeaponClass WeaponClass { get; set; }
}

// attachments, Rebuilds and weapon Tech Upgrades
public class WeaponAttachment() : Item(ItemCategory.WeaponAttachment)
{
    public WeaponAttachmentType AttachmentType { get; set; }
    // 0 when it doesn't use one
    public int? SlotsRequired { get; set; }
    public bool ExoticAllowed { get; set; }
    public string? EligibleWeapons { get; set; }
    // conditions stay in EligibleWeapons
    public List<WeaponAttachmentClass> CompatibleClasses { get; set; } = [];
    public string? Installation { get; set; }
    public string? Effect { get; set; }
}

// CP:R p.196-208
public class NetrunningItem() : Item(ItemCategory.Netrunning)
{
    public NetItemType NetType { get; set; }
    public ProgramClass? ProgramClass { get; set; }
    // Black ICE only
    public int? Perception { get; set; }
    public int? Speed { get; set; }
    public int? Attack { get; set; }
    public int? Defense { get; set; }
    public int? Rez { get; set; }
    public int? SlotsRequired { get; set; }
    public int? ProgramSlots { get; set; }
    public int? HardwareSlots { get; set; }
    public int? SharedSlots { get; set; }
    public string? Requirements { get; set; }
    public string? Effect { get; set; }
}

public class Drug() : Item(ItemCategory.Drug)
{
    public DrugClassification Classification { get; set; }
    public int? ResistDv { get; set; }
    public string? PrimaryEffect { get; set; }
    public string? Duration { get; set; }
    public int? DurationMinutes { get; set; }
    public string? SideEffects { get; set; }
    public AddictionRisk? AddictionRisk { get; set; }
    public int? HumanityLoss { get; set; }
}

public class Vehicle() : Item(ItemCategory.Vehicle)
{
    public VehicleType VehicleType { get; set; }
    public int? SpeedMove { get; set; }
    public int? TopSpeedMph { get; set; }
    public int? Maneuverability { get; set; }
    public int? Seats { get; set; }
    public int? CargoKg { get; set; }
    public int? StructuralDamagePoints { get; set; }
    public int? ArmorSp { get; set; }
    // to join the Family Motorpool (CP:R p.162; "Nomad Access" in later books)
    public int? MotoRank { get; set; }
    public string? IncludedUpgrades { get; set; }
}

public class VehicleUpgrade() : Item(ItemCategory.VehicleUpgrade)
{
    public VehicleUpgradeType UpgradeType { get; set; }
    public int? MotoRank { get; set; }
    public string? EligibleVehicles { get; set; }
    public string? Effect { get; set; }
}

public class HqUpgrade() : Item(ItemCategory.HqUpgrade)
{
    public HqUpgradeType UpgradeType { get; set; }
    public string? Benefits { get; set; }
    public string? Requirements { get; set; }
    public int? ImprovementPoints { get; set; }
    public string? SpaceRequired { get; set; }
}
