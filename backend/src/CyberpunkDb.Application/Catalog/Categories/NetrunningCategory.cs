using CyberpunkDb.Application.Catalog.Schema;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Catalog.Categories;

/// <summary>Cyberdecks, Programs, Black ICE and Cyberdeck Hardware (CP:R p.196-208; Interface RED 3).</summary>
public sealed class NetrunningCategory : CategoryDefinition<NetrunningItem>
{
    private static readonly Enum[] Programs = [NetItemType.Program, NetItemType.BlackIce];

    public override ItemCategory Category => ItemCategory.Netrunning;
    public override string Key => "netrunning";
    public override string Label => "Netrunning";
    public override string Sublabel => "Decks / Programas";
    public override string Description => "Cyberdecks, Programas, Black ICE e Hardware de Cyberdeck, com ATK, DEF, REZ e os slots que cada um ocupa.";
    public override string Icon => "netrunning";
    protected override string SubtypeField => "netType";
    protected override string[] CardFields => ["programClass", "atk", "def", "rez", "per", "spd", "totalSlots", "slotsRequired"];
    protected override string[] TableFields => ["programClass", "atk", "def", "rez", "per", "spd", "slotsRequired", "totalSlots"];

    protected override IEnumerable<FieldDefinition<NetrunningItem>> DefineFields() =>
    [
        Choice("netType", "Tipo", n => n.NetType).Required(),
        Choice("programClass", "Classe", n => n.ProgramClass).AppliesTo(Programs)
            .Describe("Booster, Defender e Attacker são Programas comuns; Anti-Personnel e Anti-Program também classificam Black ICE (CP:R p.202 e 205)."),
        Number("per", "PER", n => n.Perception).InGroup("Atributos").Sortable().Range(0, 20).AppliesTo(NetItemType.BlackIce)
            .Describe("Perception: dificuldade de fugir dela com Slide."),
        Number("spd", "SPD", n => n.Speed).InGroup("Atributos").Sortable().Range(0, 20).AppliesTo(NetItemType.BlackIce)
            .Describe("Speed: chance de acertar primeiro quando é encontrada."),
        Number("atk", "ATK", n => n.Attack).InGroup("Atributos").Sortable().Range(0, 20).AppliesTo(Programs),
        Number("def", "DEF", n => n.Defense).InGroup("Atributos").Sortable().Range(0, 20).AppliesTo(Programs),
        Number("rez", "REZ", n => n.Rez).InGroup("Atributos").Sortable().Range(0, 100).AppliesTo(Programs)
            .Describe("Os \"Hit Points\" do programa."),
        Number("slotsRequired", "Slots ocupados", n => n.SlotsRequired).InGroup("Slots").Sortable().Range(0, 10)
            .AppliesTo(NetItemType.Program, NetItemType.BlackIce, NetItemType.Hardware)
            .Describe("Slots do Cyberdeck. Black ICE ocupa 2 (CP:R p.204); Hardware ocupa 1, salvo indicação (CP:R p.208)."),
        Number("totalSlots", "Slots do deck", n => n.ProgramSlots == null && n.HardwareSlots == null && n.SharedSlots == null
                ? null
                : (int?)((n.ProgramSlots ?? 0) + (n.HardwareSlots ?? 0) + (n.SharedSlots ?? 0)))
            .InGroup("Slots").Sortable().AppliesTo(NetItemType.Cyberdeck),
        Number("sharedSlots", "Slots livres (Programa ou Hardware)", n => n.SharedSlots).InGroup("Slots").Range(0, 20).AppliesTo(NetItemType.Cyberdeck),
        Number("programSlots", "Slots só de Programa", n => n.ProgramSlots).InGroup("Slots").Range(0, 20).AppliesTo(NetItemType.Cyberdeck),
        Number("hardwareSlots", "Slots só de Hardware", n => n.HardwareSlots).InGroup("Slots").Range(0, 20).AppliesTo(NetItemType.Cyberdeck),
        Text("requirements", "Requisitos", n => n.Requirements),
        LongText("effect", "Efeito", n => n.Effect),
    ];

    protected override IEnumerable<(string Field, string Message)> ValidateItem(NetrunningItem item)
    {
        if (item.NetType != NetItemType.Cyberdeck && (item.ProgramSlots ?? item.HardwareSlots ?? item.SharedSlots) is not null)
            yield return ("sharedSlots", "Só Cyberdecks têm slots próprios.");
        if (item.NetType == NetItemType.Hardware && item.ProgramClass is not null)
            yield return ("programClass", "Hardware não tem classe de programa.");
    }
}
