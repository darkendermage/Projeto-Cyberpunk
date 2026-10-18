using CyberpunkDb.Application.Catalog.Schema;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Catalog.Categories;

public sealed class DrugCategory : CategoryDefinition<Drug>
{
    public override ItemCategory Category => ItemCategory.Drug;
    public override string Key => "drugs";
    public override string Label => "Drogas";
    public override string Sublabel => "Street Drugs";
    public override string Description => "Drogas de combate, de rua e de Netrunning, com efeitos, duração e riscos.";
    public override string Icon => "drugs";
    protected override string SubtypeField => "classification";
    protected override string[] CardFields => ["resistDv", "duration", "addictionRisk", "humanityLoss"];

    protected override IEnumerable<FieldDefinition<Drug>> DefineFields() =>
    [
        Choice("classification", "Classificação", d => d.Classification).Required(),
        Number("resistDv", "DV de resistência", d => d.ResistDv).Sortable().Range(1, 40),
        Choice("addictionRisk", "Risco de dependência", d => d.AddictionRisk),
        Text("duration", "Duração", d => d.Duration),
        Number("durationMinutes", "Duração (minutos)", d => d.DurationMinutes).WithUnit("min").Sortable().Range(0, 100_000),
        Number("humanityLoss", "Perda de Humanidade por dose", d => d.HumanityLoss).WithUnit("HL").Range(0, 100),
        LongText("primaryEffect", "Efeito primário", d => d.PrimaryEffect),
        LongText("sideEffects", "Efeitos colaterais", d => d.SideEffects),
    ];
}

public sealed class VehicleCategory : CategoryDefinition<Vehicle>
{
    public override ItemCategory Category => ItemCategory.Vehicle;
    public override string Key => "vehicles";
    public override string Label => "Veículos";
    public override string Description => "Motos, carros, veículos táticos e aeródinos. Compare velocidade, ocupantes, SDP, blindagem e o rank de Moto do Nomad.";
    public override string Icon => "vehicle";
    protected override string SubtypeField => "vehicleType";
    protected override string[] CardFields => ["speedMove", "topSpeedMph", "seats", "sdp", "armorSp", "motoRank"];
    protected override string[] TableFields => ["motoRank", "speedMove", "topSpeedMph", "seats", "sdp", "armorSp"];

    protected override IEnumerable<FieldDefinition<Vehicle>> DefineFields() =>
    [
        Choice("vehicleType", "Tipo de veículo", v => v.VehicleType).Required(),
        Number("speedMove", "Velocidade (MOVE)", v => v.SpeedMove).Sortable().Range(0, 200),
        Number("topSpeedMph", "Velocidade máx.", v => v.TopSpeedMph).WithUnit("mph").Sortable().Range(0, 2000),
        Number("maneuverability", "Manobrabilidade", v => v.Maneuverability).Sortable().Range(-10, 10),
        Number("seats", "Ocupantes", v => v.Seats).Sortable().Range(1, 1000),
        Number("cargoKg", "Carga", v => v.CargoKg).WithUnit("kg").Sortable().Range(0, 100_000),
        Number("sdp", "SDP (HP estrutural)", v => v.StructuralDamagePoints).Sortable().Range(1, 1000),
        Number("armorSp", "Blindagem (SP)", v => v.ArmorSp).Sortable().Range(0, 40),
        Flag("armored", "Blindado", v => v.ArmorSp != null),
        Number("motoRank", "Rank de Moto (Nomad)", v => v.MotoRank).Sortable().Range(1, 10)
            .Describe("Rank de Moto do Nomad para receber o veículo no Family Motorpool (CP:R p.162; \"Nomad Access\" nos suplementos). Vazio quando o livro diz que não pode ser obtido pelo Moto."),
        LongText("includedUpgrades", "Upgrades incluídos", v => v.IncludedUpgrades),
    ];
}

public sealed class HqUpgradeCategory : CategoryDefinition<HqUpgrade>
{
    public override ItemCategory Category => ItemCategory.HqUpgrade;
    public override string Key => "hq-upgrades";
    public override string Label => "Melhorias de QG";
    public override string Description => "Melhorias do quartel-general da equipe (No Place Like Home), compradas com IP de QG: oficina, enfermaria, garagem, servidor, estúdio e outras.";
    public override string Icon => "hq";
    protected override string SubtypeField => "upgradeType";
    protected override string[] CardFields => ["improvementPoints", "upgradeType", "spaceRequired"];
    protected override string[] TableFields => ["improvementPoints", "spaceRequired", "requirements"];

    protected override IEnumerable<FieldDefinition<HqUpgrade>> DefineFields() =>
    [
        Choice("upgradeType", "Tipo de melhoria", h => h.UpgradeType).Required(),
        Number("improvementPoints", "Custo em IP de QG", h => h.ImprovementPoints).WithUnit("IP").Sortable().Range(0, 1000),
        Text("spaceRequired", "Espaço exigido", h => h.SpaceRequired),
        Text("requirements", "Requisitos", h => h.Requirements),
        LongText("benefits", "Benefícios", h => h.Benefits),
    ];
}
