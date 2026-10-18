using System.ComponentModel.DataAnnotations;

namespace CyberpunkDb.Domain.Enums;

/// <summary>Top-level catalog categories. Each maps to one entity type (TPT table).</summary>
public enum ItemCategory
{
    [Display(Name = "Armas")] Weapon,
    [Display(Name = "Munições")] Ammunition,
    [Display(Name = "Upgrades de Armas")] WeaponAttachment,
    [Display(Name = "Itens Avulsos")] Gear,
    [Display(Name = "Armaduras")] Armor,
    [Display(Name = "Cyberware")] Cyberware,
    [Display(Name = "Netrunning")] Netrunning,
    [Display(Name = "Drogas")] Drug,
    [Display(Name = "Veículos")] Vehicle,
    [Display(Name = "Upgrades Veiculares")] VehicleUpgrade,
    [Display(Name = "Melhorias de QG")] HqUpgrade,
}

public enum RuleSystem
{
    [Display(Name = "Cyberpunk 2020")] Cyberpunk2020,
    [Display(Name = "Cyberpunk RED")] CyberpunkRed,
}

/// <summary>Where a record came from. Shown on every card so demo data is never mistaken for book data.</summary>
public enum DataOrigin
{
    [Display(Name = "Compêndio importado (não verificado)")] Compendium,
    [Display(Name = "Dados de demonstração")] Demo,
    [Display(Name = "Cadastro manual")] Custom,
    [Display(Name = "Conferido com PDF oficial")] Verified,
}

/// <summary>Cyberpunk RED price categories.</summary>
public enum PriceCategory
{
    [Display(Name = "Cheap (10eb)")] Cheap,
    [Display(Name = "Everyday (20eb)")] Everyday,
    [Display(Name = "Costly (50eb)")] Costly,
    [Display(Name = "Premium (100eb)")] Premium,
    [Display(Name = "Expensive (500eb)")] Expensive,
    [Display(Name = "Very Expensive (1.000eb)")] VeryExpensive,
    [Display(Name = "Luxury (5.000eb)")] Luxury,
    [Display(Name = "Super Luxury (10.000eb+)")] SuperLuxury,
}

public enum InstallLocation
{
    [Display(Name = "Mall")] Mall,
    [Display(Name = "Clínica")] Clinic,
    [Display(Name = "Hospital")] Hospital,
    [Display(Name = "Encaixe")] Socket,
    [Display(Name = "Não informado")] Unknown,
}

public enum BodyLocation
{
    [Display(Name = "Cabeça")] Head,
    [Display(Name = "Sistema Nervoso")] NervousSystem,
    [Display(Name = "Olhos")] Eyes,
    [Display(Name = "Ouvidos")] Ears,
    [Display(Name = "Interno")] Internal,
    [Display(Name = "Pele")] Skin,
    [Display(Name = "Braço")] Arm,
    [Display(Name = "Perna")] Leg,
    [Display(Name = "Mão")] Hand,
    [Display(Name = "Corpo Inteiro")] FullBody,
    [Display(Name = "Cabelo")] Hair,
}
