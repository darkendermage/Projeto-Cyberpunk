using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Catalog;

/// <summary>Read side of the catalog: schemas, faceted search, global search and item details.</summary>
public sealed class CatalogService(IAppDbContext db, CategoryRegistry registry)
{
    public async Task<IReadOnlyList<CategorySummaryDto>> GetCategoriesAsync(CancellationToken ct)
    {
        var counts = await db.Items.AsNoTracking()
            .GroupBy(i => i.Category)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return registry.All
            .Select(d => new CategorySummaryDto(d.Key, d.Category.ToString(), d.Label, d.Sublabel, d.Icon, counts.GetValueOrDefault(d.Category)))
            .ToList();
    }

    public Task<CategorySchemaDto> GetSchemaAsync(string categoryKey, CancellationToken ct) =>
        registry.Get(categoryKey).GetSchemaAsync(db, ct);

    public Task<SearchResult> SearchAsync(string categoryKey, SearchRequest request, CancellationToken ct) =>
        registry.Get(categoryKey).SearchAsync(db, request, ct);

    /// <summary>Searches every category and returns the best matches grouped by category.</summary>
    public async Task<IReadOnlyList<GlobalSearchGroup>> GlobalSearchAsync(string? query, int perCategory, CancellationToken ct)
    {
        var tokens = TextNormalizer.Tokenize(query);
        if (tokens.Count == 0) return [];

        var matches = db.Items.AsNoTracking();
        foreach (var token in tokens) matches = matches.Where(i => i.SearchText.Contains(token));
        var counts = await matches.GroupBy(i => i.Category)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var groups = new List<GlobalSearchGroup>();
        foreach (var def in registry.All.Where(d => counts.ContainsKey(d.Category)))
        {
            var result = await def.SearchAsync(db, new SearchRequest
            {
                Q = query,
                PageSize = Math.Clamp(perCategory, 1, 20),
                IncludeFacets = false,
            }, ct);
            groups.Add(new GlobalSearchGroup(def.Key, def.Label, counts[def.Category], result.Items));
        }
        return groups;
    }

    public async Task<ItemDetailDto> GetItemAsync(int id, CancellationToken ct)
    {
        var category = await db.Items.AsNoTracking().Where(i => i.Id == id).Select(i => (Domain.Enums.ItemCategory?)i.Category)
                           .FirstOrDefaultAsync(ct)
                       ?? throw new NotFoundException($"Item {id} não encontrado.");
        var def = registry.Get(category);
        var item = await def.LoadAsync(db, id, tracking: false, ct) ?? throw new NotFoundException($"Item {id} não encontrado.");
        return new ItemDetailDto { Item = def.ToDto(item), Relations = await def.GetRelationsAsync(db, item, ct) };
    }

    public async Task<ItemDetailDto> GetItemBySlugAsync(string slug, CancellationToken ct)
    {
        var id = await db.Items.AsNoTracking().Where(i => i.Slug == slug).Select(i => (int?)i.Id).FirstOrDefaultAsync(ct)
                 ?? throw new NotFoundException($"Item '{slug}' não encontrado.");
        return await GetItemAsync(id, ct);
    }

    /// <summary>Loads items of any category, preserving the order of <paramref name="ids"/>.</summary>
    public async Task<IReadOnlyList<ItemDto>> GetItemsAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        if (ids.Count > 200) throw RequestValidationException.For("ids", "Máximo de 200 itens por consulta.");

        var categories = await db.Items.AsNoTracking().Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.Category }).ToListAsync(ct);

        var loaded = new Dictionary<int, ItemDto>();
        foreach (var group in categories.GroupBy(c => c.Category))
        {
            var dtos = await registry.Get(group.Key).LoadManyAsync(db, group.Select(g => g.Id).ToList(), ct);
            foreach (var dto in dtos) loaded[dto.Id] = dto;
        }
        return ids.Where(loaded.ContainsKey).Select(id => loaded[id]).ToList();
    }
}
