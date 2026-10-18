using System.Text.Json;

namespace CyberpunkDb.Application.Catalog.Contracts;

public static class FilterOps
{
    public const string Eq = "eq";
    public const string Neq = "neq";
    public const string Gt = "gt";
    public const string Gte = "gte";
    public const string Lt = "lt";
    public const string Lte = "lte";
    public const string Between = "between";
    public const string In = "in";
    public const string NotIn = "nin";
    public const string All = "all";
    public const string Contains = "contains";
    public const string IsNull = "isnull";
    public const string NotNull = "notnull";

    public static readonly string[] Numeric = [Eq, Neq, Gt, Gte, Lt, Lte, Between, IsNull, NotNull];
    public static readonly string[] Enum = [In, NotIn, Eq, Neq];
    public static readonly string[] Set = [All, In, NotIn];
    public static readonly string[] Boolean = [Eq];
    public static readonly string[] Text = [Contains, IsNull, NotNull];
}

/// <summary>One filter criterion. Criteria are combined with AND; values inside "in" are OR-ed.</summary>
public sealed class FilterCriterion
{
    public string Field { get; set; } = string.Empty;
    public string Op { get; set; } = FilterOps.Eq;
    public JsonElement? Value { get; set; }
    public List<JsonElement>? Values { get; set; }
    public JsonElement? Min { get; set; }
    public JsonElement? Max { get; set; }
}

public sealed class SearchRequest
{
    public string? Q { get; set; }
    public List<FilterCriterion> Filters { get; set; } = [];
    public string? Sort { get; set; }
    public string? Dir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
    public bool IncludeFacets { get; set; } = true;
}

public sealed record FacetValue(string Value, string Label, int Count);

public sealed record SearchResult(
    IReadOnlyList<ItemDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages,
    IReadOnlyDictionary<string, IReadOnlyList<FacetValue>> Facets);

public sealed record NamedRef(string Slug, string Name);

public sealed record OptionDto(string Value, string Label);

public sealed class ItemDto
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public OptionDto? Subtype { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public int? Price { get; init; }
    public string? PriceNote { get; init; }
    public string? PriceCategory { get; init; }
    public NamedRef? Manufacturer { get; init; }
    public NamedRef? Source { get; init; }
    public int? Page { get; init; }
    public string? ImageUrl { get; init; }
    public string RuleSystem { get; init; } = string.Empty;
    public string DataOrigin { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new Dictionary<string, object?>();
    public JsonElement? Extra { get; init; }
}

public sealed record ItemSummaryDto(int Id, string Slug, string Name, string Category, int? Price, string? Subtype);

public sealed record RelationGroupDto(string Key, string Label, IReadOnlyList<ItemSummaryDto> Items);

public sealed class ItemDetailDto
{
    public required ItemDto Item { get; init; }
    public IReadOnlyList<RelationGroupDto> Relations { get; init; } = [];
}

public sealed record GlobalSearchGroup(string Category, string Label, int Total, IReadOnlyList<ItemDto> Items);

// ---------- schema ----------

public sealed class FieldDescriptorDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public string? Group { get; init; }
    public string? Description { get; init; }
    public bool Filterable { get; init; }
    public bool Sortable { get; init; }
    public bool Facet { get; init; }
    public bool Editable { get; init; }
    public bool Required { get; init; }
    public bool Base { get; init; }
    public string? DefaultOp { get; init; }
    public IReadOnlyList<string> Operators { get; init; } = [];
    public IReadOnlyList<string>? AppliesTo { get; init; }
    public IReadOnlyList<OptionDto>? Options { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    /// <summary>Distinct values of small numeric domains (ROF, dice, ranges), used for min/max pickers.</summary>
    public IReadOnlyList<double>? Values { get; init; }
}

public sealed record SortOptionDto(string Key, string Label);

public sealed class CategorySchemaDto
{
    public string Key { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Sublabel { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string? SubtypeField { get; init; }
    public IReadOnlyList<string> CardFields { get; init; } = [];
    public IReadOnlyList<string> TableFields { get; init; } = [];
    public IReadOnlyList<string> FilterOrder { get; init; } = [];
    public IReadOnlyList<SortOptionDto> SortOptions { get; init; } = [];
    public string DefaultSort { get; init; } = "name";
    public IReadOnlyList<FieldDescriptorDto> Fields { get; init; } = [];
    public int ItemCount { get; init; }
}

public sealed record CategorySummaryDto(string Key, string Category, string Label, string? Sublabel, string Icon, int ItemCount);

// ---------- write model (also the import/export format) ----------

public sealed class ItemUpsertDto
{
    public string? Slug { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public int? Price { get; set; }
    public string? PriceNote { get; set; }
    public string? PriceCategory { get; set; }
    public string? Manufacturer { get; set; }
    public string? Source { get; set; }
    public int? Page { get; set; }
    public string? ImageUrl { get; set; }
    public string? RuleSystem { get; set; }
    public string? DataOrigin { get; set; }
    public List<string>? Tags { get; set; }
    public Dictionary<string, JsonElement>? Attributes { get; set; }
    public JsonElement? Extra { get; set; }
}

public sealed record ImportResult(int Created, int Updated, int Failed, IReadOnlyList<ImportError> Errors);

public sealed record ImportError(int Index, string? Slug, string? Name, IDictionary<string, string[]> Errors);
