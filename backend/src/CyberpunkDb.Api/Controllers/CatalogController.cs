using CyberpunkDb.Application.Catalog;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Common;
using Microsoft.AspNetCore.Mvc;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class CatalogController(CatalogService catalog) : ControllerBase
{
    [HttpGet("categories")]
    public Task<IReadOnlyList<CategorySummaryDto>> GetCategories(CancellationToken ct) => catalog.GetCategoriesAsync(ct);

    // filters, operators, options, bounds, card layout, sorts
    [HttpGet("categories/{category}/schema")]
    public Task<CategorySchemaDto> GetSchema(string category, CancellationToken ct) => catalog.GetSchemaAsync(category, ct);

    // criteria are ANDed
    [HttpPost("catalog/{category}/search")]
    public Task<SearchResult> Search(string category, [FromBody] SearchRequest request, CancellationToken ct) =>
        catalog.SearchAsync(category, request, ct);

    [HttpGet("catalog/search")]
    public Task<IReadOnlyList<GlobalSearchGroup>> GlobalSearch([FromQuery] string? q, [FromQuery] int perCategory = 5, CancellationToken ct = default) =>
        catalog.GlobalSearchAsync(q, perCategory, ct);

    [HttpGet("items/{id:int}")]
    public Task<ItemDetailDto> GetItem(int id, CancellationToken ct) => catalog.GetItemAsync(id, ct);

    [HttpGet("items/by-slug/{slug}")]
    public Task<ItemDetailDto> GetItemBySlug(string slug, CancellationToken ct) => catalog.GetItemBySlugAsync(slug, ct);

    // comparison view, e.g. /api/items?ids=1,5,9
    [HttpGet("items")]
    public Task<IReadOnlyList<ItemDto>> GetItems([FromQuery] string? ids, CancellationToken ct)
    {
        var parsed = new List<int>();
        foreach (var part in (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var id)) throw RequestValidationException.For("ids", L.T($"Id inválido: '{part}'.", $"Invalid id: '{part}'."));
            if (!parsed.Contains(id)) parsed.Add(id);
        }
        return catalog.GetItemsAsync(parsed, ct);
    }
}
