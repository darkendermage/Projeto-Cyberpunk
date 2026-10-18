using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Catalog.Schema;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Catalog.Categories;

public sealed class CyberwareCategory : CategoryDefinition<CyberwareItem>
{
    /// <summary>Attribute key used by imports/forms to reference the foundational piece by slug or name.</summary>
    public const string RequiresKey = "requiresItem";

    public override ItemCategory Category => ItemCategory.Cyberware;
    public override string Key => "cyberware";
    public override string Label => "Cyberware";
    public override string Description => "Neuralware, cyberoptics, cyberaudio, implantes internos e externos, membros cibernéticos e borgware.";
    public override string Icon => "cyberware";
    protected override string SubtypeField => "cyberwareType";
    protected override string[] CardFields => ["humanityLoss", "installLocation", "slotsRequired", "slotsProvided", "bodyLocation"];
    protected override string[] TableFields => ["bodyLocation", "humanityLoss", "installLocation", "slotsRequired", "requiresItem"];

    protected override IEnumerable<FieldDefinition<CyberwareItem>> DefineFields() =>
    [
        Choice("cyberwareType", "Tipo de cyberware", c => c.CyberwareType).Required(),
        Choice("bodyLocation", "Localização", c => c.BodyLocation),
        Choice("installLocation", "Instalação", c => c.InstallLocation).InGroup("Instalação"),
        Number("humanityLoss", "Perda de Humanidade", c => c.HumanityLoss).WithUnit("HL").InGroup("Instalação").Sortable().Range(0, 100),
        Dice("humanityLossDice", "HL (dados)", c => c.HumanityLossDice).InGroup("Instalação"),
        Flag("foundational", "Peça base (foundational)", c => c.IsFoundational),
        Number("slotsProvided", "Slots oferecidos", c => c.SlotsProvided).InGroup("Slots").Range(0, 20),
        Number("slotsRequired", "Slots ocupados", c => c.SlotsRequired).InGroup("Slots").Sortable().Range(0, 10),
        Flag("requiresPair", "Exige par", c => c.RequiresPair),
        Choice(RequiresKey, "Requer (peça base)", c => c.RequiresItem != null ? c.RequiresItem.Name : null)
            .Describe("Peça base onde o item é instalado."),
        Text("requirements", "Requisitos", c => c.Requirements),
        LongText("effect", "Efeitos", c => c.Effect),
    ];

    protected override IQueryable<CyberwareItem> IncludeDetails(IQueryable<CyberwareItem> query) => query.Include(c => c.RequiresItem);

    protected override async Task<IReadOnlyList<RelationGroupDto>> GetCategoryRelationsAsync(IAppDbContext db, CyberwareItem item, CancellationToken ct)
    {
        var groups = new List<RelationGroupDto>();
        if (item.RequiresItem is { } foundation)
            groups.Add(new RelationGroupDto("requires", "Requer instalação em",
                [new ItemSummaryDto(foundation.Id, foundation.Slug, foundation.Name, Key, foundation.PriceEb, null)]));

        var options = await db.Set<CyberwareItem>().AsNoTracking()
            .Where(c => c.RequiresItemId == item.Id)
            .OrderBy(c => c.Name)
            .Select(c => new ItemSummaryDto(c.Id, c.Slug, c.Name, "cyberware", c.PriceEb, null))
            .ToListAsync(ct);
        if (options.Count > 0) groups.Add(new RelationGroupDto("options", "Opções instaláveis", options));
        return groups;
    }
}
