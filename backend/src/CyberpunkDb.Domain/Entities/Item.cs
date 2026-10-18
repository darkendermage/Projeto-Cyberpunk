using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

/// <summary>
/// Shared data for every catalog entry. Each category derives from this type and is mapped
/// table-per-type, so category attributes live in their own strongly-typed table.
/// </summary>
public abstract class Item
{
    protected Item(ItemCategory category) => Category = category;

    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ItemCategory Category { get; private set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    /// <summary>Price in eurodollars. Null when the source gives no fixed price.</summary>
    public int? PriceEb { get; set; }
    /// <summary>Free text for prices that do not fit a single number ("20eb/50eb", "por 10 un.").</summary>
    public string? PriceNote { get; set; }
    public PriceCategory? PriceCategory { get; set; }

    public int? ManufacturerId { get; set; }
    public Manufacturer? Manufacturer { get; set; }
    public int? SourceBookId { get; set; }
    public SourceBook? SourceBook { get; set; }
    public int? Page { get; set; }
    public string? ImageUrl { get; set; }

    public RuleSystem RuleSystem { get; set; } = RuleSystem.CyberpunkRed;
    public DataOrigin DataOrigin { get; set; } = DataOrigin.Custom;

    public List<ItemTag> Tags { get; set; } = [];

    /// <summary>Optional JSON object for rare attributes that do not justify a column.</summary>
    public string? ExtraAttributesJson { get; set; }

    /// <summary>Normalized (lowercase, unaccented) text used for full-catalog search. Maintained on save.</summary>
    public string SearchText { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ItemTag
{
    public int ItemId { get; set; }
    public string Value { get; set; } = string.Empty;
}

public class Manufacturer
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class SourceBook
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public RuleSystem RuleSystem { get; set; } = RuleSystem.CyberpunkRed;
}
