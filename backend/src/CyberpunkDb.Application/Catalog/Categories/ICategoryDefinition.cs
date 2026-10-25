using System.Text.Json;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;

namespace CyberpunkDb.Application.Catalog.Categories;

/// <summary>Non-generic view of a category so services can dispatch on a runtime category key.</summary>
public interface ICategoryDefinition
{
    ItemCategory Category { get; }
    /// <summary>URL-friendly key ("weapons", "cyberware"...).</summary>
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
    /// <summary>Applies attribute values and runs field + cross-field validation. Returns errors keyed by field.</summary>
    Dictionary<string, string[]> ApplyAttributes(Item item, IReadOnlyDictionary<string, JsonElement> attributes, bool isCreate);
}
