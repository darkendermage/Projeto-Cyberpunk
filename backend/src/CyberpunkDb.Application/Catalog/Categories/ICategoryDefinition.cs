using System.Text.Json;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Catalog.Categories;

// non-generic so services can dispatch on the category key
public interface ICategoryDefinition
{
    ItemCategory Category { get; }
    // "weapons", "cyberware"...
    string Key { get; }
    string Label { get; }
    string? Sublabel { get; }
    string Icon { get; }
    Type EntityType { get; }

    Task<SearchResult> SearchAsync(IAppDbContext db, SearchRequest request, CancellationToken ct);
    Task<CategorySchemaDto> GetSchemaAsync(IAppDbContext db, CancellationToken ct);
    Task<int> CountAsync(IAppDbContext db, CancellationToken ct);

    Task<Item?> LoadAsync(IAppDbContext db, int id, bool tracking, CancellationToken ct);
    Task<IReadOnlyList<ItemDto>> LoadManyAsync(IAppDbContext db, IReadOnlyCollection<int> ids, CancellationToken ct);
    Task<IReadOnlyList<RelationGroupDto>> GetRelationsAsync(IAppDbContext db, Item item, CancellationToken ct);

    ItemDto ToDto(Item item);
    string BuildSearchText(Item item);

    Item CreateEntity();
    // errors keyed by field
    Dictionary<string, string[]> ApplyAttributes(Item item, IReadOnlyDictionary<string, JsonElement> attributes, bool isCreate);
}
