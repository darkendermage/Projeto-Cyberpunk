using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

public class Weapon() : Item(ItemCategory.Weapon)
{
    public WeaponClass WeaponClass { get; set; }
    public WeaponSkill Skill { get; set; }
    /// <summary>Damage as printed ("3d6", "3d6 / 5d6").</summary>
    public string? Damage { get; set; }
    /// <summary>Number of d6 of the primary damage, for numeric filtering.</summary>
    public int? DamageDice { get; set; }
    public int? Rof { get; set; }
    /// <summary>Autofire multiplier (x3, x4...). Null when the weapon has no autofire.</summary>
    public int? AutofireMultiplier { get; set; }
    public int Hands { get; set; } = 1;
    public bool Concealable { get; set; }
    public int? MagazineCapacity { get; set; }
    public WeaponQuality Quality { get; set; } = WeaponQuality.Standard;
    public bool IsExotic { get; set; }
    /// <summary>Longest range band usable by the weapon class, in meters (0 = melee).</summary>
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
    /// <summary>Finer classification, e.g. program class (Attacker, Booster) or Black ICE class.</summary>
    public string? Subtype { get; set; }
    public decimal? WeightKg { get; set; }
    public string? Requirements { get; set; }
    public string? Effect { get; set; }
    public string? SkillBonus { get; set; }
}

public class ArmorItem() : Item(ItemCategory.Armor)
{
    public ArmorType ArmorType { get; set; }
    /// <summary>Stopping Power. Armor, ACPA and some clothing-like suits only.</summary>
    public int? StoppingPower { get; set; }
    public int? ShieldHp { get; set; }
    public int? RefPenalty { get; set; }
    public int? DexPenalty { get; set; }
    public int? MovePenalty { get; set; }
    /// <summary>BODY granted by Linear Frames / ACPA.</summary>
    public int? BodyValue { get; set; }
    /// <summary>Structural HP of ACPA.</summary>
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
    /// <summary>True for foundational pieces that provide option slots (Cybereye, Neural Link...).</summary>
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

/// <summary>Weapon attachments, Rebuilds and weapon Tech Upgrades.</summary>
public class WeaponAttachment() : Item(ItemCategory.WeaponAttachment)
{
    public WeaponAttachmentType AttachmentType { get; set; }
    /// <summary>Attachment Slots taken on the weapon; 0 when it does not use one.</summary>
    public int? SlotsRequired { get; set; }
    public bool ExoticAllowed { get; set; }
    public string? EligibleWeapons { get; set; }
    /// <summary>Weapon classes the upgrade fits (the conditions stay in <see cref="EligibleWeapons"/>).</summary>
    public List<WeaponAttachmentClass> CompatibleClasses { get; set; } = [];
    public string? Installation { get; set; }
    public string? Effect { get; set; }
}

/// <summary>Cyberdecks, Programs, Black ICE and Cyberdeck Hardware (CP:R p.196-208).</summary>
public class NetrunningItem() : Item(ItemCategory.Netrunning)
{
    public NetItemType NetType { get; set; }
    public ProgramClass? ProgramClass { get; set; }
    /// <summary>Black ICE only: how hard it is to Slide away from.</summary>
    public int? Perception { get; set; }
    /// <summary>Black ICE only.</summary>
    public int? Speed { get; set; }
    public int? Attack { get; set; }
    public int? Defense { get; set; }
    /// <summary>Program hit points.</summary>
    public int? Rez { get; set; }
    /// <summary>Cyberdeck slots taken by a Program, Black ICE or piece of Hardware.</summary>
    public int? SlotsRequired { get; set; }
    /// <summary>Cyberdeck slots that only accept Programs.</summary>
    public int? ProgramSlots { get; set; }
    /// <summary>Cyberdeck slots that only accept Hardware.</summary>
    public int? HardwareSlots { get; set; }
    /// <summary>Cyberdeck slots that accept either Programs or Hardware.</summary>
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
    /// <summary>Nomad Moto Rank needed to add the vehicle to the Family Motorpool (CP:R p.162; "Nomad Access" in later books).</summary>
    public int? MotoRank { get; set; }
    public string? IncludedUpgrades { get; set; }
}

public class VehicleUpgrade() : Item(ItemCategory.VehicleUpgrade)
{
    public VehicleUpgradeType UpgradeType { get; set; }
    /// <summary>Nomad Moto Role Ability rank at which the upgrade becomes available.</summary>
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
