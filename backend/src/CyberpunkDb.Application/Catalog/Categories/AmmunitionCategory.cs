using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Catalog.Schema;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Catalog.Categories;

public sealed class AmmunitionCategory : CategoryDefinition<Ammunition>
{
    public override ItemCategory Category => ItemCategory.Ammunition;
    public override string Key => "ammunition";
    public override string Label => "Munições";
    public override string Description => "Munições especiais, granadas e explosivos. Filtre pelo calibre para ver o que serve na sua arma.";
    public override string Icon => "ammo";
    protected override string SubtypeField => "kind";
    protected override string[] CardFields => ["quantityPerPack", "damage", "resistDv", "area", "effectType"];
    protected override string[] TableFields => ["effectType", "calibers", "quantityPerPack", "damage", "resistDv"];

    protected override IEnumerable<FieldDefinition<Ammunition>> DefineFields() =>
    [
        Choice("kind", "Tipo", a => a.Kind).Required(),
        Choice("effectType", "Efeito principal", a => a.EffectType),
        Set("calibers", "Calibre / armas compatíveis", a => a.Calibers, c => c.Caliber,
                (a, values) => Sync(a.Calibers, values, c => c.Caliber, v => new AmmunitionCaliber { Caliber = v }))
            .DefaultOp(FilterOps.In),
        Number("quantityPerPack", "Qtd. por pacote", a => a.QuantityPerPack).WithUnit("un.").Sortable().Range(1, 1000),
        Dice("damage", "Dano", a => a.Damage),
        Number("damageDice", "Dano (nº de d6)", a => a.DamageDice).WithUnit("d6").Sortable().Range(0, 20),
        Text("area", "Área de efeito", a => a.AreaOfEffect),
        Number("resistDv", "DV de resistência", a => a.ResistDv).Sortable().Range(1, 40),
        Text("requirements", "Requisitos", a => a.Requirements),
        LongText("effect", "Efeito detalhado", a => a.Effect),
    ];

    protected override IQueryable<Ammunition> IncludeDetails(IQueryable<Ammunition> query) => query.Include(a => a.Calibers);

    protected override async Task<IReadOnlyList<RelationGroupDto>> GetCategoryRelationsAsync(IAppDbContext db, Ammunition item, CancellationToken ct)
    {
        var calibers = item.Calibers.Select(c => c.Caliber).ToList();
        if (calibers.Count == 0) return [];
        var weapons = await db.Set<Weapon>().AsNoTracking()
            .Where(w => w.Calibers.Any(c => calibers.Contains(c.Caliber)))
            .OrderBy(w => w.WeaponClass).ThenBy(w => w.Name)
            .Take(60)
            .Select(w => new ItemSummaryDto(w.Id, w.Slug, w.Name, "weapons", w.PriceEb, null))
            .ToListAsync(ct);
        return weapons.Count == 0 ? [] : [new RelationGroupDto("compatibleWeapons", "Armas compatíveis", weapons)];
    }
}
