using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Catalog.Schema;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Catalog.Categories;

public sealed class WeaponCategory : CategoryDefinition<Weapon>
{
    public static readonly WeaponClass[] MeleeClasses =
        [WeaponClass.LightMelee, WeaponClass.MediumMelee, WeaponClass.HeavyMelee, WeaponClass.VeryHeavyMelee];

    private static readonly Enum[] RangedClasses =
        Enum.GetValues<WeaponClass>().Except(MeleeClasses).Cast<Enum>().ToArray();

    public override ItemCategory Category => ItemCategory.Weapon;
    public override string Key => "weapons";
    public override string Label => "Armas";
    public override string Description => "Pistolas, SMGs, escopetas, rifles, armas pesadas e armas brancas. Combine os filtros para encontrar exatamente o que precisa.";
    public override string Icon => "weapons";
    protected override string SubtypeField => "weaponClass";
    protected override string[] CardFields => ["damage", "rof", "maxRange", "concealable", "magazine"];
    protected override string[] TableFields => ["damage", "rof", "maxRange", "concealable", "calibers"];

    protected override IEnumerable<FieldDefinition<Weapon>> DefineFields() =>
    [
        Choice("weaponClass", "Tipo de arma", w => w.WeaponClass).Required(),
        Choice("skill", "Perícia", w => w.Skill),
        Dice("damage", "Dano", w => w.Damage).InGroup("Combate"),
        Number("damageDice", "Dano (nº de d6)", w => w.DamageDice).WithUnit("d6").InGroup("Combate").Sortable().Range(0, 20)
            .Describe("Quantidade de d6 do dano principal. Use para comparar armas numericamente."),
        Number("rof", "ROF", w => w.Rof).InGroup("Combate").Sortable().Range(0, 10),
        Number("autofire", "Autofire (multiplicador)", w => w.AutofireMultiplier).InGroup("Combate").Range(1, 10)
            .AppliesTo(RangedClasses),
        Number("magazine", "Pente", w => w.MagazineCapacity).WithUnit("tiros").InGroup("Combate").Sortable().Range(0, 1000)
            .AppliesTo(RangedClasses),
        Number("maxRange", "Alcance máx.", w => w.MaxRangeMeters).WithUnit("m").InGroup("Combate").Sortable().Range(0, 5000)
            .Describe("Maior faixa da tabela de alcance da classe da arma (0 = corpo a corpo). Valor derivado da classe; confira a tabela do livro."),
        Number("hands", "Mãos", w => w.Hands).Range(1, 2),
        Flag("concealable", "Ocultável", w => w.Concealable),
        Choice("quality", "Qualidade", w => w.Quality),
        Flag("exotic", "Arma exótica", w => w.IsExotic),
        Number("minBody", "BODY mínimo", w => w.MinBody).Range(1, 20),
        Set("calibers", "Munição compatível", w => w.Calibers, c => c.Caliber,
                (w, values) => Sync(w.Calibers, values, c => c.Caliber, v => new WeaponCaliber { Caliber = v }))
            .DefaultOp(FilterOps.In).AppliesTo(RangedClasses),
    ];

    protected override IQueryable<Weapon> IncludeDetails(IQueryable<Weapon> query) => query.Include(w => w.Calibers);

    protected override IEnumerable<(string Field, string Message)> ValidateItem(Weapon item)
    {
        if (item.DamageDice is not null && string.IsNullOrWhiteSpace(item.Damage))
            yield return ("damage", "Informe o dano como impresso (ex.: 3d6) quando o nº de d6 for preenchido.");
        if (item.AutofireMultiplier is not null && item.MagazineCapacity is < 10)
            yield return ("autofire", "Autofire exige pente de pelo menos 10 tiros.");
    }

    protected override async Task<IReadOnlyList<RelationGroupDto>> GetCategoryRelationsAsync(IAppDbContext db, Weapon item, CancellationToken ct)
    {
        var groups = new List<RelationGroupDto>();
        var calibers = item.Calibers.Select(c => c.Caliber).ToList();
        if (calibers.Count > 0)
        {
            var ammo = await db.Set<Ammunition>().AsNoTracking()
                .Where(a => a.Calibers.Any(c => calibers.Contains(c.Caliber)))
                .OrderBy(a => a.Kind).ThenBy(a => a.Name)
                .Select(a => new ItemSummaryDto(a.Id, a.Slug, a.Name, "ammunition", a.PriceEb, null))
                .ToListAsync(ct);
            if (ammo.Count > 0) groups.Add(new RelationGroupDto("compatibleAmmo", "Munições compatíveis", ammo));
        }

        var upgrades = await db.Set<WeaponAttachment>().AsNoTracking()
            .Where(a => a.CompatibleClasses.Any(c => c.WeaponClass == item.WeaponClass) && (!item.IsExotic || a.ExoticAllowed))
            .OrderBy(a => a.AttachmentType).ThenBy(a => a.Name)
            .Select(a => new ItemSummaryDto(a.Id, a.Slug, a.Name, "weapon-attachments", a.PriceEb, null))
            .ToListAsync(ct);
        if (upgrades.Count > 0) groups.Add(new RelationGroupDto("compatibleUpgrades", "Upgrades compatíveis", upgrades));
        return groups;
    }
}
