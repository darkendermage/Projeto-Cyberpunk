using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog.Categories;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Catalog;

/// <summary>Single entry point to the category definitions. Adding a category = adding one definition here.</summary>
public sealed class CategoryRegistry : ISearchTextBuilder
{
    private readonly Dictionary<string, ICategoryDefinition> _byKey;
    private readonly Dictionary<ItemCategory, ICategoryDefinition> _byCategory;

    public CategoryRegistry() : this(
    [
        new WeaponCategory(), new AmmunitionCategory(), new WeaponAttachmentCategory(), new GearCategory(), new ArmorCategory(), new CyberwareCategory(),
        new NetrunningCategory(), new DrugCategory(), new VehicleCategory(), new VehicleUpgradeCategory(), new HqUpgradeCategory(),
    ])
    { }

    public CategoryRegistry(IEnumerable<ICategoryDefinition> definitions)
    {
        All = definitions.OrderBy(d => d.Category).ToList();
        _byKey = All.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);
        _byCategory = All.ToDictionary(d => d.Category);
    }

    public IReadOnlyList<ICategoryDefinition> All { get; }

    public ICategoryDefinition Get(string key)
    {
        if (_byKey.TryGetValue(key, out var def)) return def;
        // Also accept the enum name ("Weapon") which is what imports and the Items table store.
        if (Enum.TryParse<ItemCategory>(key, true, out var category) && _byCategory.TryGetValue(category, out def)) return def;
        throw new NotFoundException($"Categoria '{key}' não existe.");
    }

    public ICategoryDefinition Get(ItemCategory category) => _byCategory[category];

    public ICategoryDefinition For(Item item) => _byCategory[item.Category];

    public string Build(Item item) => For(item).BuildSearchText(item);
}
