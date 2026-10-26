using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Catalog;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using CyberpunkDb.Application.Localization;

namespace CyberpunkDb.Application.Collections;

/// <summary>Favorites and named equipment lists. Single local profile; ready to be scoped by user later.</summary>
public sealed class CollectionsService(
    IAppDbContext db,
    CatalogService catalog,
    IValidator<GearListUpsertRequest> listValidator,
    IValidator<GearListEntryRequest> entryValidator,
    IValidator<GearListEntryUpdateRequest> entryUpdateValidator)
{
    // ------------------------------------------------------------ favorites

    public async Task<IReadOnlyList<int>> GetFavoriteIdsAsync(CancellationToken ct) =>
        await db.Favorites.AsNoTracking().OrderByDescending(f => f.CreatedAt).Select(f => f.ItemId).ToListAsync(ct);

    public async Task<IReadOnlyList<ItemDto>> GetFavoritesAsync(CancellationToken ct) =>
        await catalog.GetItemsAsync(await GetFavoriteIdsAsync(ct), ct);

    public async Task AddFavoriteAsync(int itemId, CancellationToken ct)
    {
        await EnsureItemExistsAsync(itemId, ct);
        if (await db.Favorites.AnyAsync(f => f.ItemId == itemId, ct)) return;
        db.Favorites.Add(new Favorite { ItemId = itemId, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveFavoriteAsync(int itemId, CancellationToken ct) =>
        await db.Favorites.Where(f => f.ItemId == itemId).ExecuteDeleteAsync(ct);

    // ------------------------------------------------------------ lists

    public async Task<IReadOnlyList<GearListSummaryDto>> GetListsAsync(CancellationToken ct) =>
        await db.GearLists.AsNoTracking()
            .OrderByDescending(l => l.UpdatedAt)
            .Select(l => new GearListSummaryDto(
                l.Id, l.Name, l.Description,
                l.Entries.Count,
                l.Entries.Sum(e => (int?)e.Quantity) ?? 0,
                l.Entries.Sum(e => (long?)((e.Item!.PriceEb ?? 0) * e.Quantity)) ?? 0,
                l.UpdatedAt))
            .ToListAsync(ct);

    public async Task<GearListDetailDto> GetListAsync(int id, CancellationToken ct)
    {
        var list = await db.GearLists.AsNoTracking().Include(l => l.Entries).FirstOrDefaultAsync(l => l.Id == id, ct)
                   ?? throw new NotFoundException(L.T($"Lista {id} não encontrada.", $"List {id} not found."));
        var items = (await catalog.GetItemsAsync(list.Entries.Select(e => e.ItemId).Distinct().ToList(), ct))
            .ToDictionary(i => i.Id);

        var entries = list.Entries
            .Where(e => items.ContainsKey(e.ItemId))
            .OrderBy(e => e.AddedAt)
            .Select(e => new GearListEntryDto(e.Id, e.Quantity, e.Note, e.AddedAt, items[e.ItemId]))
            .ToList();

        return new GearListDetailDto(list.Id, list.Name, list.Description, list.CreatedAt, list.UpdatedAt, entries,
            entries.Sum(e => (long)(e.Item.Price ?? 0) * e.Quantity),
            entries.Count(e => e.Item.Price is null));
    }

    public async Task<GearListDetailDto> CreateListAsync(GearListUpsertRequest request, CancellationToken ct)
    {
        await ValidateAsync(listValidator, request, ct);
        var now = DateTime.UtcNow;
        var list = new GearList { Name = request.Name.Trim(), Description = request.Description?.Trim(), CreatedAt = now, UpdatedAt = now };
        db.GearLists.Add(list);
        await db.SaveChangesAsync(ct);
        return await GetListAsync(list.Id, ct);
    }

    public async Task<GearListDetailDto> UpdateListAsync(int id, GearListUpsertRequest request, CancellationToken ct)
    {
        await ValidateAsync(listValidator, request, ct);
        var list = await FindListAsync(id, ct);
        list.Name = request.Name.Trim();
        list.Description = request.Description?.Trim();
        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetListAsync(id, ct);
    }

    public async Task DeleteListAsync(int id, CancellationToken ct)
    {
        var list = await FindListAsync(id, ct);
        db.GearLists.Remove(list);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Adds an item; if it is already in the list the quantities are summed.</summary>
    public async Task<GearListDetailDto> AddEntryAsync(int listId, GearListEntryRequest request, CancellationToken ct)
    {
        await ValidateAsync(entryValidator, request, ct);
        await EnsureItemExistsAsync(request.ItemId, ct);
        var list = await db.GearLists.Include(l => l.Entries).FirstOrDefaultAsync(l => l.Id == listId, ct)
                   ?? throw new NotFoundException(L.T($"Lista {listId} não encontrada.", $"List {listId} not found."));

        var existing = list.Entries.FirstOrDefault(e => e.ItemId == request.ItemId);
        if (existing is not null)
        {
            existing.Quantity = Math.Min(999, existing.Quantity + request.Quantity);
            if (!string.IsNullOrWhiteSpace(request.Note)) existing.Note = request.Note.Trim();
        }
        else
        {
            if (list.Entries.Count >= 500) throw RequestValidationException.For("itemId", L.T("Uma lista aceita no máximo 500 itens.", "A list takes at most 500 items."));
            list.Entries.Add(new GearListEntry
            {
                ItemId = request.ItemId, Quantity = request.Quantity, Note = request.Note?.Trim(), AddedAt = DateTime.UtcNow,
            });
        }
        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetListAsync(listId, ct);
    }

    public async Task<GearListDetailDto> UpdateEntryAsync(int listId, int entryId, GearListEntryUpdateRequest request, CancellationToken ct)
    {
        await ValidateAsync(entryUpdateValidator, request, ct);
        var entry = await db.GearListEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.GearListId == listId, ct)
                    ?? throw new NotFoundException(L.T("Item da lista não encontrado.", "List item not found."));
        entry.Quantity = request.Quantity;
        entry.Note = request.Note?.Trim();
        await TouchAsync(listId, ct);
        await db.SaveChangesAsync(ct);
        return await GetListAsync(listId, ct);
    }

    public async Task<GearListDetailDto> RemoveEntryAsync(int listId, int entryId, CancellationToken ct)
    {
        var entry = await db.GearListEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.GearListId == listId, ct)
                    ?? throw new NotFoundException(L.T("Item da lista não encontrado.", "List item not found."));
        db.GearListEntries.Remove(entry);
        await TouchAsync(listId, ct);
        await db.SaveChangesAsync(ct);
        return await GetListAsync(listId, ct);
    }

    // ------------------------------------------------------------ helpers

    private async Task<GearList> FindListAsync(int id, CancellationToken ct) =>
        await db.GearLists.FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException(L.T($"Lista {id} não encontrada.", $"List {id} not found."));

    private async Task TouchAsync(int listId, CancellationToken ct) => (await FindListAsync(listId, ct)).UpdatedAt = DateTime.UtcNow;

    private async Task EnsureItemExistsAsync(int itemId, CancellationToken ct)
    {
        if (!await db.Items.AnyAsync(i => i.Id == itemId, ct)) throw new NotFoundException(L.T($"Item {itemId} não encontrado.", $"Item {itemId} not found."));
    }

    private static async Task ValidateAsync<TRequest>(IValidator<TRequest> validator, TRequest request, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(request, ct);
        if (!result.IsValid)
            throw new RequestValidationException(result.Errors
                .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    }
}
