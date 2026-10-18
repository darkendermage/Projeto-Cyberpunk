using CyberpunkDb.Application.Catalog;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class CatalogController(CatalogService catalog) : ControllerBase
{
    /// <summary>All categories with item counts.</summary>
    [HttpGet("categories")]
    public Task<IReadOnlyList<CategorySummaryDto>> GetCategories(CancellationToken ct) => catalog.GetCategoriesAsync(ct);

    /// <summary>Field schema of a category: filters, operators, options, numeric bounds, card layout and sort options.</summary>
    [HttpGet("categories/{category}/schema")]
    public Task<CategorySchemaDto> GetSchema(string category, CancellationToken ct) => catalog.GetSchemaAsync(category, ct);

    /// <summary>Faceted search inside one category. Criteria are combined with AND.</summary>
    [HttpPost("catalog/{category}/search")]
    public Task<SearchResult> Search(string category, [FromBody] SearchRequest request, CancellationToken ct) =>
        catalog.SearchAsync(category, request, ct);

    /// <summary>Text search across the whole catalog, grouped by category.</summary>
    [HttpGet("catalog/search")]
    public Task<IReadOnlyList<GlobalSearchGroup>> GlobalSearch([FromQuery] string? q, [FromQuery] int perCategory = 5, CancellationToken ct = default) =>
        catalog.GlobalSearchAsync(q, perCategory, ct);

    [HttpGet("items/{id:int}")]
    public Task<ItemDetailDto> GetItem(int id, CancellationToken ct) => catalog.GetItemAsync(id, ct);

    [HttpGet("items/by-slug/{slug}")]
    public Task<ItemDetailDto> GetItemBySlug(string slug, CancellationToken ct) => catalog.GetItemBySlugAsync(slug, ct);

    /// <summary>Several items of any category (used by the comparison view). Example: /api/items?ids=1,5,9</summary>
    [HttpGet("items")]
    public Task<IReadOnlyList<ItemDto>> GetItems([FromQuery] string? ids, CancellationToken ct)
    {
        var parsed = new List<int>();
        foreach (var part in (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var id)) throw RequestValidationException.For("ids", $"Id inválido: '{part}'.");
            if (!parsed.Contains(id)) parsed.Add(id);
        }
        return catalog.GetItemsAsync(parsed, ct);
    }
}
