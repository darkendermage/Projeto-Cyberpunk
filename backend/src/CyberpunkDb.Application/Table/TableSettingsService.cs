using System.Text.RegularExpressions;
using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

/// <summary>A dice color (#rrggbb); null clears it (the seat's color, or the default dice for the GM).</summary>
public sealed record DiceColorRequest(string? Color);

/// <summary>Settings of the virtual table kept across restarts, by key (the GM's dice color, the music).</summary>
public sealed partial class TableSettingsService(IAppDbContext db)
{
    public const string GmDiceColor = "gmDiceColor";

    public async Task<string?> GetAsync(string key, CancellationToken ct) =>
        await db.TableSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>; null removes it.</summary>
    public async Task SetAsync(string key, string? value, CancellationToken ct)
    {
        var row = await db.TableSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (value is null)
        {
            if (row is not null) db.TableSettings.Remove(row);
        }
        else if (row is null) db.TableSettings.Add(new TableSetting { Key = key, Value = value });
        else row.Value = value;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>A dice color as stored: #rrggbb in lower case, or null; anything else is refused.</summary>
    public static string? CleanColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return null;
        var c = color.Trim();
        return HexColor().IsMatch(c) ? c.ToLowerInvariant()
            : throw RequestValidationException.For("color", L.T("Use uma cor no formato #rrggbb.", "Use a color like #rrggbb."));
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
