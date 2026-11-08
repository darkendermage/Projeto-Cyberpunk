using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Domain.Entities;

// Base for every catalog entry. Table-per-type, so each category's attributes get their own table.
public abstract class Item
{
    protected Item(ItemCategory category) => Category = category;

    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ItemCategory Category { get; private set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    // null when the book has no fixed price
    public int? PriceEb { get; set; }
    // prices that aren't one number ("20eb/50eb", "por 10 un.")
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

    // rare attributes that don't deserve a column
    public string? ExtraAttributesJson { get; set; }

    // lowercase, no accents, kept up to date on save
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
