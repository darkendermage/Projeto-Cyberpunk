using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Catalog.Schema;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Catalog.Categories;

public sealed class WeaponAttachmentCategory : CategoryDefinition<WeaponAttachment>
{
    public override ItemCategory Category => ItemCategory.WeaponAttachment;
    public override string Key => "weapon-attachments";
    public override string Label => "Upgrades de Armas";
    public override string Sublabel => "Acessórios / Rebuilds";
    public override string Description => "Acessórios de arma, Rebuilds e Tech Upgrades: slots ocupados, armas compatíveis e efeito.";
    public override string Icon => "attachment";
    protected override string SubtypeField => "attachmentType";
    protected override string[] CardFields => ["compatibleClasses", "slotsRequired", "exoticAllowed", "installation"];
    protected override string[] TableFields => ["compatibleClasses", "slotsRequired", "exoticAllowed"];

    protected override IEnumerable<FieldDefinition<WeaponAttachment>> DefineFields() =>
    [
        Choice("attachmentType", "Tipo de upgrade", a => a.AttachmentType).Required(),
        Number("slotsRequired", "Slots de acessório", a => a.SlotsRequired).Sortable().Range(0, 5),
        Flag("exoticAllowed", "Instala em Exóticas", a => a.ExoticAllowed),
        Set("compatibleClasses", "Compatível com", a => a.CompatibleClasses, c => c.WeaponClass,
                (a, values) => Sync(a.CompatibleClasses, values, c => c.WeaponClass, v => new WeaponAttachmentClass { WeaponClass = v }))
            .DefaultOp(FilterOps.In)
            .Describe("Classes de arma em que o upgrade pode ser instalado. As condições (não Exóticas, só Shoulder Arms...) ficam em \"Restrições de compatibilidade\"."),
        Text("eligibleWeapons", "Restrições de compatibilidade", a => a.EligibleWeapons),
        Text("installation", "Instalação", a => a.Installation),
        LongText("effect", "Efeito", a => a.Effect),
    ];

    protected override IQueryable<WeaponAttachment> IncludeDetails(IQueryable<WeaponAttachment> query) => query.Include(a => a.CompatibleClasses);

    protected override async Task<IReadOnlyList<RelationGroupDto>> GetCategoryRelationsAsync(IAppDbContext db, WeaponAttachment item, CancellationToken ct)
    {
        var classes = item.CompatibleClasses.Select(c => c.WeaponClass).ToList();
        if (classes.Count == 0) return [];
        var weapons = await db.Set<Weapon>().AsNoTracking()
            .Where(w => classes.Contains(w.WeaponClass) && (item.ExoticAllowed || !w.IsExotic))
            .OrderBy(w => w.WeaponClass).ThenBy(w => w.Name)
            .Take(60)
            .Select(w => new ItemSummaryDto(w.Id, w.Slug, w.Name, "weapons", w.PriceEb, null))
            .ToListAsync(ct);
        return weapons.Count == 0 ? [] : [new RelationGroupDto("compatibleWeapons", "Armas compatíveis", weapons)];
    }
}
