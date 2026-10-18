using System.Security.Cryptography;
using System.Text.Json;
using CyberpunkDb.Application.Catalog;
using CyberpunkDb.Application.Catalog.Contracts;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Domain.Entities;
using CyberpunkDb.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CyberpunkDb.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public bool Enabled { get; set; } = true;
    /// <summary>Folder with *.json files in the import format. Relative paths resolve from the app folder.</summary>
    public string Path { get; set; } = "Seed";
}

/// <summary>
/// Applies migrations (backing up a SQLite file first) and imports the seed catalog whenever its
/// content changes. Imports upsert by slug, so favorites and lists are kept across catalog updates.
/// </summary>
public sealed class DatabaseInitializer(
    AppDbContext db,
    ItemWriteService writer,
    CategoryRegistry registry,
    ISeedSource seedSource,
    IOptions<SeedOptions> options,
    ILogger<DatabaseInitializer> logger)
{
    private const string SeedHashKey = "SeedHash";
    private const int BackupsToKeep = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (db.Database.IsSqlite()) BackupSqliteBeforeMigration();
        await db.Database.MigrateAsync(ct);

        if (!options.Value.Enabled) return;
        var files = seedSource.GetFiles();
        if (files.Count == 0)
        {
            logger.LogWarning("Nenhum arquivo de seed encontrado; o catálogo começa vazio.");
            return;
        }

        var contents = files.Select(f => (f.Name, Bytes: ReadAll(f))).ToList();
        var hash = Convert.ToHexString(SHA256.HashData(contents.SelectMany(c => c.Bytes).ToArray()));
        var stored = await db.Set<AppMetadata>().FirstOrDefaultAsync(m => m.Key == SeedHashKey, ct);
        if (stored?.Value == hash) return;

        logger.LogInformation(stored is null ? "Importando o catálogo..." : "Catálogo atualizado; reimportando...");
        var seeds = contents.Select(c => (c.Name, Rows: JsonSerializer.Deserialize<List<ItemUpsertDto>>(c.Bytes, JsonOptions) ?? [])).ToList();
        List<MovedItem> moved = stored is null ? [] : await DetachRecategorizedAsync(seeds.SelectMany(s => s.Rows), ct);
        var seededSlugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, rows) in seeds)
        {
            seededSlugs.UnionWith(rows.Select(r => r.Slug).OfType<string>());
            var result = await writer.ImportAsync(rows, ct);
            logger.LogInformation("Seed {File}: {Created} criados, {Updated} atualizados, {Failed} com erro.",
                name, result.Created, result.Updated, result.Failed);
            foreach (var error in result.Errors.Take(20))
                logger.LogWarning("Seed {File} #{Index} ({Name}): {Errors}", name, error.Index, error.Name,
                    string.Join(" | ", error.Errors.Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value)}")));
            db.ChangeTracker.Clear();
        }
        if (moved.Count > 0) await ReattachRecategorizedAsync(moved, ct);
        if (stored is not null) await RemoveRetiredItemsAsync(seededSlugs, ct);

        if (stored is null) db.Set<AppMetadata>().Add(new AppMetadata { Key = SeedHashKey, Value = hash });
        else
        {
            db.Attach(stored);
            stored.Value = hash;
        }
        await db.SaveChangesAsync(ct);
    }

    private sealed record MovedItem(string Slug, DateTime? FavoritedAt, List<GearListEntry> Entries);

    /// <summary>
    /// A seed item whose category changed (weapon attachments that left "Itens Avulsos", for instance) lives in
    /// another table, so it cannot be updated in place. It is deleted here and recreated by the import; its
    /// favorite and list entries are kept aside and re-attached by slug afterwards.
    /// </summary>
    private async Task<List<MovedItem>> DetachRecategorizedAsync(IEnumerable<ItemUpsertDto> rows, CancellationToken ct)
    {
        var seeded = new Dictionary<string, ItemCategory>(StringComparer.Ordinal);
        foreach (var row in rows.Where(r => r.Slug is not null))
        {
            // Rows with an unknown category are reported by the import itself.
            try { seeded[row.Slug!] = registry.Get(row.Category).Category; }
            catch (NotFoundException) { }
        }

        var existing = await db.Items.Where(i => i.DataOrigin != DataOrigin.Custom)
            .Select(i => new { i.Id, i.Slug, i.Category }).ToListAsync(ct);
        var changed = existing.Where(i => seeded.TryGetValue(i.Slug, out var category) && category != i.Category).ToList();
        if (changed.Count == 0) return [];

        var ids = changed.Select(i => i.Id).ToList();
        var favorites = await db.Favorites.Where(f => ids.Contains(f.ItemId)).ToDictionaryAsync(f => f.ItemId, f => f.CreatedAt, ct);
        var entries = await db.GearListEntries.AsNoTracking().Where(e => ids.Contains(e.ItemId)).ToListAsync(ct);
        var moved = changed.Select(i => new MovedItem(i.Slug, favorites.TryGetValue(i.Id, out var at) ? at : null,
            entries.Where(e => e.ItemId == i.Id).ToList())).ToList();

        await db.Set<CyberwareItem>().Where(c => c.RequiresItemId != null && ids.Contains(c.RequiresItemId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.RequiresItemId, (int?)null), ct);
        foreach (var id in ids)
        {
            var item = await db.Items.FindAsync([id], ct);
            if (item is not null) db.Items.Remove(item);
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        logger.LogInformation("Catálogo: {Count} itens mudaram de categoria e serão recriados.", changed.Count);
        return moved;
    }

    private async Task ReattachRecategorizedAsync(List<MovedItem> moved, CancellationToken ct)
    {
        var slugs = moved.Select(m => m.Slug).ToList();
        var ids = await db.Items.Where(i => slugs.Contains(i.Slug)).ToDictionaryAsync(i => i.Slug, i => i.Id, ct);
        foreach (var item in moved)
        {
            if (!ids.TryGetValue(item.Slug, out var id)) continue;
            if (item.FavoritedAt is { } at) db.Favorites.Add(new Favorite { ItemId = id, CreatedAt = at });
            foreach (var entry in item.Entries)
                db.GearListEntries.Add(new GearListEntry
                {
                    GearListId = entry.GearListId, ItemId = id, Quantity = entry.Quantity, Note = entry.Note, AddedAt = entry.AddedAt,
                });
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <summary>
    /// Deletes catalog items that an earlier seed created but the current one no longer ships (a duplicate
    /// merged away, demo data replaced by book data). Items registered by the user (Custom) are never touched.
    /// </summary>
    private async Task RemoveRetiredItemsAsync(HashSet<string> seededSlugs, CancellationToken ct)
    {
        var retired = (await db.Items.Where(i => i.DataOrigin != DataOrigin.Custom).Select(i => new { i.Id, i.Slug }).ToListAsync(ct))
            .Where(i => !seededSlugs.Contains(i.Slug)).Select(i => i.Id).ToList();
        if (retired.Count == 0) return;

        // "Requer peça base" is a restricting reference; detach it before the base item goes away.
        await db.Set<CyberwareItem>().Where(c => c.RequiresItemId != null && retired.Contains(c.RequiresItemId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.RequiresItemId, (int?)null), ct);
        foreach (var id in retired)
        {
            var item = await db.Items.FindAsync([id], ct);
            if (item is not null) db.Items.Remove(item);
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        logger.LogInformation("Catálogo: {Count} itens que saíram do catálogo foram removidos.", retired.Count);
    }

    private static byte[] ReadAll(SeedFile file)
    {
        using var stream = file.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>Copies the SQLite file before pending migrations change it (keeps the newest three copies).</summary>
    private void BackupSqliteBeforeMigration()
    {
        var path = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        if (string.IsNullOrWhiteSpace(path) || path == ":memory:" || !File.Exists(path)) return;
        if (!db.Database.GetPendingMigrations().Any()) return;

        var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!;
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var backup = System.IO.Path.Combine(folder, $"{name}-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        SqliteConnection.ClearAllPools();
        File.Copy(path, backup, overwrite: true);
        logger.LogInformation("Backup do banco antes da atualização: {Backup}", backup);

        foreach (var old in Directory.GetFiles(folder, $"{name}-backup-*.db").OrderDescending().Skip(BackupsToKeep))
            File.Delete(old);
    }
}
