using CyberpunkDb.Application.Catalog.Schema;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Catalog.Categories;

/// <summary>
/// Armor, shields, clothing, Linear Frames and ACPA share one table but not the same attributes:
/// each field declares which subtypes it applies to, and the base class rejects values set on the others.
/// </summary>
public sealed class ArmorCategory : CategoryDefinition<ArmorItem>
{
    private static readonly Enum[] Protective = [ArmorType.BodyArmor, ArmorType.PoweredArmor];

    public override ItemCategory Category => ItemCategory.Armor;
    public override string Key => "armor";
    public override string Label => "Armaduras";
    public override string Sublabel => "Linear Frames / Roupas";
    public override string Description => "Armaduras corporais, escudos, roupas, Linear Frames e ACPAs. Cada tipo exibe apenas os atributos que fazem sentido para ele.";
    public override string Icon => "armor";
    protected override string SubtypeField => "armorType";
    protected override string[] CardFields =>
        ["sp", "shieldHp", "bodyValue", "hitPoints", "refPenalty", "movePenalty", "humanityLoss", "clothingSlot", "clothingStyle"];

    protected override string[] TableFields => ["sp", "shieldHp", "refPenalty", "movePenalty", "bodyValue", "humanityLoss"];

    protected override IEnumerable<FieldDefinition<ArmorItem>> DefineFields() =>
    [
        Choice("armorType", "Tipo de equipamento", a => a.ArmorType).Required(),
        Number("sp", "SP (Stopping Power)", a => a.StoppingPower).InGroup("Proteção").Sortable().Range(0, 30).AppliesTo(Protective),
        Number("shieldHp", "HP do escudo", a => a.ShieldHp).InGroup("Proteção").Sortable().Range(1, 200).AppliesTo(ArmorType.Shield),
        Text("protectedLocations", "Locais protegidos", a => a.ProtectedLocations).InGroup("Proteção")
            .AppliesTo(ArmorType.BodyArmor, ArmorType.PoweredArmor, ArmorType.Shield),
        Number("refPenalty", "Penalidade REF", a => a.RefPenalty).WithUnit("penalty").InGroup("Penalidades").Range(0, 10).AppliesTo(Protective),
        Number("dexPenalty", "Penalidade DEX", a => a.DexPenalty).WithUnit("penalty").InGroup("Penalidades").Range(0, 10).AppliesTo(Protective),
        Number("movePenalty", "Penalidade MOVE", a => a.MovePenalty).WithUnit("penalty").InGroup("Penalidades").Range(0, 10).AppliesTo(Protective),
        Flag("hasPenalty", "Possui penalidade", a => (a.RefPenalty ?? 0) + (a.DexPenalty ?? 0) + (a.MovePenalty ?? 0) > 0),
        Number("bodyValue", "BODY concedido", a => a.BodyValue).InGroup("Estrutura").Sortable().Range(1, 20)
            .AppliesTo(ArmorType.LinearFrame, ArmorType.PoweredArmor),
        Number("hitPoints", "HP estrutural", a => a.HitPoints).InGroup("Estrutura").Sortable().Range(1, 500).AppliesTo(ArmorType.PoweredArmor),
        Number("humanityLoss", "Perda de Humanidade", a => a.HumanityLoss).WithUnit("HL").InGroup("Instalação").Sortable().Range(0, 100)
            .AppliesTo(ArmorType.LinearFrame),
        Dice("humanityLossDice", "HL (dados)", a => a.HumanityLossDice).InGroup("Instalação").AppliesTo(ArmorType.LinearFrame),
        Choice("installLocation", "Instalação", a => a.InstallLocation).InGroup("Instalação").AppliesTo(ArmorType.LinearFrame),
        Choice("clothingSlot", "Peça de roupa", a => a.ClothingSlot).InGroup("Roupa").AppliesTo(ArmorType.Clothing),
        Choice("clothingStyle", "Estilo", a => a.ClothingStyle).InGroup("Roupa").AppliesTo(ArmorType.Clothing),
        Text("requirements", "Requisitos", a => a.Requirements),
        LongText("effect", "Efeitos", a => a.Effect),
    ];

    protected override IEnumerable<(string Field, string Message)> ValidateItem(ArmorItem item)
    {
        if (item.ArmorType == ArmorType.Shield && item.ShieldHp is null)
            yield return ("shieldHp", "Escudos precisam de HP.");
        if (item.ArmorType == ArmorType.LinearFrame && item.BodyValue is null)
            yield return ("bodyValue", "Linear Frames precisam informar o BODY concedido.");
        if (item.ArmorType == ArmorType.Clothing && item.ClothingSlot is null)
            yield return ("clothingSlot", "Informe que peça de roupa é.");
    }
}
