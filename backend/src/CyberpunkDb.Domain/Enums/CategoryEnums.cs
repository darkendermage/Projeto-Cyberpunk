using System.ComponentModel.DataAnnotations;

namespace CyberpunkDb.Domain.Enums;

public enum WeaponClass
{
    [Display(Name = "Pistola Média")] MediumPistol,
    [Display(Name = "Pistola Pesada")] HeavyPistol,
    [Display(Name = "Pistola Muito Pesada")] VeryHeavyPistol,
    [Display(Name = "SMG")] Smg,
    [Display(Name = "SMG Pesada")] HeavySmg,
    [Display(Name = "Escopeta")] Shotgun,
    [Display(Name = "Rifle de Assalto")] AssaultRifle,
    [Display(Name = "Rifle de Precisão")] SniperRifle,
    [Display(Name = "Metralhadora")] MachineGun,
    [Display(Name = "Arco")] Bow,
    [Display(Name = "Besta")] Crossbow,
    [Display(Name = "Lança-Granadas")] GrenadeLauncher,
    [Display(Name = "Lança-Foguetes")] RocketLauncher,
    [Display(Name = "Corpo a Corpo Leve")] LightMelee,
    [Display(Name = "Corpo a Corpo Médio")] MediumMelee,
    [Display(Name = "Corpo a Corpo Pesado")] HeavyMelee,
    [Display(Name = "Corpo a Corpo Muito Pesado")] VeryHeavyMelee,
}

public enum WeaponSkill
{
    [Display(Name = "Handgun")] Handgun,
    [Display(Name = "Shoulder Arms")] ShoulderArms,
    [Display(Name = "Heavy Weapons")] HeavyWeapons,
    [Display(Name = "Archery")] Archery,
    [Display(Name = "Melee Weapon")] MeleeWeapon,
    [Display(Name = "Pilot Air Vehicle")] PilotAirVehicle,
    [Display(Name = "Autofire")] Autofire,
    [Display(Name = "Athletics")] Athletics,
}

public enum WeaponQuality
{
    [Display(Name = "Baixa Qualidade")] Poor,
    [Display(Name = "Padrão")] Standard,
    [Display(Name = "Qualidade Excelente")] Excellent,
}

/// <summary>Ammunition families shared by weapons and ammo; compatibility = shared caliber.</summary>
public enum Caliber
{
    [Display(Name = "Pistola Média")] MediumPistol,
    [Display(Name = "Pistola Pesada")] HeavyPistol,
    [Display(Name = "Pistola Muito Pesada")] VeryHeavyPistol,
    [Display(Name = "Slug")] Slug,
    [Display(Name = "Cartucho (Shell)")] ShotgunShell,
    [Display(Name = "Rifle")] Rifle,
    [Display(Name = "Flecha / Dardo")] Arrow,
    [Display(Name = "Granada")] Grenade,
    [Display(Name = "Foguete")] Rocket,
    [Display(Name = "Bateria / Carga")] Battery,
    [Display(Name = "Especial")] Special,
}

public enum WeaponAttachmentType
{
    [Display(Name = "Mira")] Scope,
    [Display(Name = "Pente")] Magazine,
    [Display(Name = "Sob o cano")] Underbarrel,
    [Display(Name = "Baioneta")] Bayonet,
    [Display(Name = "Smartgun Link")] SmartLink,
    [Display(Name = "Silenciador")] Muzzle,
    [Display(Name = "Cano / Alcance")] Barrel,
    [Display(Name = "Mecanismo interno")] Internals,
    [Display(Name = "Rebuild")] Rebuild,
    [Display(Name = "Tech Upgrade inventado")] TechUpgrade,
}

public enum AmmoKind
{
    [Display(Name = "Munição")] Round,
    [Display(Name = "Granada")] Grenade,
    [Display(Name = "Explosivo")] Explosive,
    [Display(Name = "Detonador / Acessório")] Accessory,
}

public enum AmmoEffect
{
    [Display(Name = "Dano letal")] Lethal,
    [Display(Name = "Perfurante (AP)")] ArmorPiercing,
    [Display(Name = "Não-letal")] NonLethal,
    [Display(Name = "Toxina / Veneno")] Toxin,
    [Display(Name = "Incendiário")] Incendiary,
    [Display(Name = "EMP / Eletrônico")] Emp,
    [Display(Name = "Controle (fumaça, gás, flash)")] Control,
    [Display(Name = "Lesão crítica ampliada")] CriticalInjury,
    [Display(Name = "Precisão / Mira")] Accuracy,
    [Display(Name = "Utilidade")] Utility,
}

public enum GearType
{
    [Display(Name = "Eletrônico")] Electronics,
    [Display(Name = "Ferramenta")] Tool,
    [Display(Name = "Médico")] Medical,
    [Display(Name = "Sobrevivência")] Survival,
    [Display(Name = "Segurança / Infiltração")] Security,
    [Display(Name = "Estilo de Vida")] Lifestyle,
    [Display(Name = "Upgrade Veicular")] VehicleUpgrade,
}

public enum ArmorType
{
    [Display(Name = "Armadura Corporal")] BodyArmor,
    [Display(Name = "Escudo")] Shield,
    [Display(Name = "Roupa")] Clothing,
    [Display(Name = "Linear Frame")] LinearFrame,
    [Display(Name = "ACPA")] PoweredArmor,
}

public enum ClothingSlot
{
    [Display(Name = "Parte de cima")] Top,
    [Display(Name = "Parte de baixo")] Bottom,
    [Display(Name = "Jaqueta")] Jacket,
    [Display(Name = "Calçado")] Footwear,
    [Display(Name = "Joia / Acessório")] Jewelry,
    [Display(Name = "Óculos")] Eyewear,
    [Display(Name = "Chapéu")] Headwear,
    [Display(Name = "Traje completo")] FullOutfit,
}

public enum ClothingStyle
{
    [Display(Name = "Generic Chic")] GenericChic,
    [Display(Name = "Leisurewear")] Leisurewear,
    [Display(Name = "Bag Lady Chic")] BagLadyChic,
    [Display(Name = "Gang Colors")] GangColors,
    [Display(Name = "Nomad Leathers")] NomadLeathers,
    [Display(Name = "Asia Pop")] AsiaPop,
    [Display(Name = "Urban Flash")] UrbanFlash,
    [Display(Name = "Businesswear")] Businesswear,
    [Display(Name = "High Fashion")] HighFashion,
    [Display(Name = "Bohemian")] Bohemian,
}

public enum CyberwareType
{
    [Display(Name = "Neuralware")] Neuralware,
    [Display(Name = "Cyberoptics")] Cyberoptics,
    [Display(Name = "Cyberaudio")] Cyberaudio,
    [Display(Name = "Corpo Interno")] InternalBody,
    [Display(Name = "Corpo Externo")] ExternalBody,
    [Display(Name = "Membro Cibernético")] Cyberlimb,
    [Display(Name = "Opção de Membro")] LimbOption,
    [Display(Name = "Cyberfinger")] Cyberfinger,
    [Display(Name = "Borgware")] Borgware,
    [Display(Name = "Bioexotics®")] Bioexotic,
    [Display(Name = "Fashionware")] Fashionware,
}

public enum NetItemType
{
    [Display(Name = "Cyberdeck")] Cyberdeck,
    [Display(Name = "Programa")] Program,
    [Display(Name = "Black ICE")] BlackIce,
    [Display(Name = "Hardware de Cyberdeck")] Hardware,
}

public enum ProgramClass
{
    [Display(Name = "Booster")] Booster,
    [Display(Name = "Defender")] Defender,
    [Display(Name = "Anti-Program")] AntiProgram,
    [Display(Name = "Anti-Personnel")] AntiPersonnel,
    [Display(Name = "Demon")] Demon,
}

public enum DrugClassification
{
    [Display(Name = "Combate")] Combat,
    [Display(Name = "Rua / Recreativa")] Street,
    [Display(Name = "Netrunning")] Netrunning,
    [Display(Name = "Social")] Social,
}

public enum AddictionRisk
{
    [Display(Name = "Baixo")] Low,
    [Display(Name = "Moderado")] Moderate,
    [Display(Name = "Alto")] High,
}

public enum VehicleType
{
    [Display(Name = "Bicicleta")] Bicycle,
    [Display(Name = "Motocicleta")] Motorcycle,
    [Display(Name = "Carro")] Car,
    [Display(Name = "Esportivo")] SportsCar,
    [Display(Name = "Tático / Blindado")] Tactical,
    [Display(Name = "Aeródino (AV)")] Aerodyne,
    [Display(Name = "Embarcação")] Boat,
    [Display(Name = "Helicóptero")] Helicopter,
    [Display(Name = "Aerozep (dirigível)")] Aerozep,
    [Display(Name = "Girocóptero")] Gyrocopter,
    [Display(Name = "Hoverboard")] Hoverboard,
}

public enum VehicleUpgradeType
{
    [Display(Name = "Upgrade de veículo")] Vehicle,
    [Display(Name = "Cômodo de veículo")] Room,
    [Display(Name = "Upgrade de bicicleta")] Bicycle,
    [Display(Name = "Consumível")] Consumable,
}

public enum HqUpgradeType
{
    [Display(Name = "Oficina / Bancada")] Workshop,
    [Display(Name = "Armaria")] Armory,
    [Display(Name = "Enfermaria")] Medical,
    [Display(Name = "Garagem")] Garage,
    [Display(Name = "Segurança / Vigilância")] Security,
    [Display(Name = "Habitação")] Living,
    [Display(Name = "Investigação")] Investigation,
    [Display(Name = "Cativeiro")] Detention,
    [Display(Name = "Reuniões / Social")] Social,
    [Display(Name = "Moral / Lazer")] Morale,
    [Display(Name = "Servidor / NET")] Server,
    [Display(Name = "Estúdio")] Studio,
    [Display(Name = "Treino")] Training,
    [Display(Name = "Equipe do Exec")] Team,
}
