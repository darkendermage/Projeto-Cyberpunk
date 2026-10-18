using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Collections;
using Microsoft.AspNetCore.Mvc;

namespace CyberpunkDb.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class CollectionsController(CollectionsService collections) : ControllerBase
{
    [HttpGet("favorites")]
    public Task<IReadOnlyList<ItemDto>> GetFavorites(CancellationToken ct) => collections.GetFavoritesAsync(ct);

    [HttpGet("favorites/ids")]
    public Task<IReadOnlyList<int>> GetFavoriteIds(CancellationToken ct) => collections.GetFavoriteIdsAsync(ct);

    [HttpPut("favorites/{itemId:int}")]
    public async Task<IActionResult> AddFavorite(int itemId, CancellationToken ct)
    {
        await collections.AddFavoriteAsync(itemId, ct);
        return NoContent();
    }

    [HttpDelete("favorites/{itemId:int}")]
    public async Task<IActionResult> RemoveFavorite(int itemId, CancellationToken ct)
    {
        await collections.RemoveFavoriteAsync(itemId, ct);
        return NoContent();
    }

    [HttpGet("lists")]
    public Task<IReadOnlyList<GearListSummaryDto>> GetLists(CancellationToken ct) => collections.GetListsAsync(ct);

    [HttpGet("lists/{id:int}")]
    public Task<GearListDetailDto> GetList(int id, CancellationToken ct) => collections.GetListAsync(id, ct);

    [HttpPost("lists")]
    public async Task<ActionResult<GearListDetailDto>> CreateList(GearListUpsertRequest request, CancellationToken ct)
    {
        var list = await collections.CreateListAsync(request, ct);
        return CreatedAtAction(nameof(GetList), new { id = list.Id }, list);
    }

    [HttpPut("lists/{id:int}")]
    public Task<GearListDetailDto> UpdateList(int id, GearListUpsertRequest request, CancellationToken ct) =>
        collections.UpdateListAsync(id, request, ct);

    [HttpDelete("lists/{id:int}")]
    public async Task<IActionResult> DeleteList(int id, CancellationToken ct)
    {
        await collections.DeleteListAsync(id, ct);
        return NoContent();
    }

    [HttpPost("lists/{id:int}/entries")]
    public Task<GearListDetailDto> AddEntry(int id, GearListEntryRequest request, CancellationToken ct) =>
        collections.AddEntryAsync(id, request, ct);

    [HttpPut("lists/{id:int}/entries/{entryId:int}")]
    public Task<GearListDetailDto> UpdateEntry(int id, int entryId, GearListEntryUpdateRequest request, CancellationToken ct) =>
        collections.UpdateEntryAsync(id, entryId, request, ct);

    [HttpDelete("lists/{id:int}/entries/{entryId:int}")]
    public Task<GearListDetailDto> RemoveEntry(int id, int entryId, CancellationToken ct) =>
        collections.RemoveEntryAsync(id, entryId, ct);
}
