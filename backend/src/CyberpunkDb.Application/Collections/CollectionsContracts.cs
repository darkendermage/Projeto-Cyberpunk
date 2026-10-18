using CyberpunkDb.Application.Catalog.Contracts;

namespace CyberpunkDb.Application.Collections;

public sealed record GearListSummaryDto(int Id, string Name, string? Description, int EntryCount, int TotalQuantity, long TotalPrice, DateTime UpdatedAt);

public sealed record GearListEntryDto(int Id, int Quantity, string? Note, DateTime AddedAt, ItemDto Item);

public sealed record GearListDetailDto(
    int Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<GearListEntryDto> Entries,
    long TotalPrice,
    int UnpricedEntries);

public sealed class GearListUpsertRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class GearListEntryRequest
{
    public int ItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Note { get; set; }
}

public sealed class GearListEntryUpdateRequest
{
    public int Quantity { get; set; } = 1;
    public string? Note { get; set; }
}
