using CyberpunkDb.Application.Catalog.Schema;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Catalog.Categories;

public sealed class VehicleUpgradeCategory : CategoryDefinition<VehicleUpgrade>
{
    public override ItemCategory Category => ItemCategory.VehicleUpgrade;
    public override string Key => "vehicle-upgrades";
    public override string Label => "Upgrades Veiculares";
    public override string Description => "Upgrades de veículos e bicicletas e cômodos de veículo, com o rank de Moto do Nomad e os veículos compatíveis.";
    public override string Icon => "vehicleUpgrade";
    protected override string SubtypeField => "upgradeType";
    protected override string[] CardFields => ["motoRank", "eligibleVehicles"];
    protected override string[] TableFields => ["motoRank", "eligibleVehicles"];

    protected override IEnumerable<FieldDefinition<VehicleUpgrade>> DefineFields() =>
    [
        Choice("upgradeType", "Tipo de upgrade", v => v.UpgradeType).Required(),
        Number("motoRank", "Rank de Moto (Nomad)", v => v.MotoRank).Sortable().Range(1, 10),
        Text("eligibleVehicles", "Veículos compatíveis", v => v.EligibleVehicles),
        LongText("effect", "Efeito", v => v.Effect),
    ];
}
