using CyberpunkDb.Application.Catalog.Schema;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Catalog.Categories;

public sealed class GearCategory : CategoryDefinition<GearItem>
{
    public override ItemCategory Category => ItemCategory.Gear;
    public override string Key => "gear";
    public override string Label => "Itens Avulsos";
    public override string Description => "Equipamentos, ferramentas, eletrônicos, Agents e Apps. Cyberdecks e programas ficam em Netrunning.";
    public override string Icon => "gear";
    protected override string SubtypeField => "gearType";
    protected override string[] CardFields => ["gearClass", "skillBonus", "weightKg", "gearType"];
    protected override string[] TableFields => ["gearClass", "skillBonus", "requirements"];

    protected override IEnumerable<FieldDefinition<GearItem>> DefineFields() =>
    [
        Choice("gearType", "Tipo de item", g => g.GearType).Required(),
        Choice("gearClass", "Classe", g => g.Subtype)
            .Describe("Classificação interna, como a classe de um programa (Attacker, Booster...)."),
        Number("weightKg", "Peso", g => g.WeightKg).WithUnit("kg").Sortable().Range(0, 10_000),
        Flag("hasSkillBonus", "Concede bônus de perícia", g => g.SkillBonus != null),
        Text("skillBonus", "Bônus", g => g.SkillBonus),
        Text("requirements", "Requisitos", g => g.Requirements),
        LongText("effect", "Função / efeito", g => g.Effect),
    ];
}
