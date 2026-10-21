using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ISearchTextBuilder? searchTextBuilder = null)
    : DbContext(options), IAppDbContext
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<SourceBook> SourceBooks => Set<SourceBook>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<GearList> GearLists => Set<GearList>();
    public DbSet<GearListEntry> GearListEntries => Set<GearListEntry>();
    public DbSet<Npc> Npcs => Set<Npc>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<CharacterEvent> CharacterEvents => Set<CharacterEvent>();
    public DbSet<CharacterPortrait> CharacterPortraits => Set<CharacterPortrait>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as their names: readable in SQL and safe when members are reordered.
        foreach (var enumType in typeof(Item).Assembly.GetTypes().Where(t => t.IsEnum))
            configurationBuilder.Properties(enumType).HaveConversion<string>().HaveMaxLength(40);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        CatalogModelConfiguration.Apply(modelBuilder, Database.IsNpgsql());

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BeforeSave();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BeforeSave();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Maintains timestamps and the denormalized search text of items touched in this unit of work.</summary>
    private void BeforeSave()
    {
        ChangeTracker.DetectChanges();
        var now = DateTime.UtcNow;

        var touchedIds = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Deleted or EntityState.Modified)
            .Select(e => e.Entity switch
            {
                ItemTag t => t.ItemId,
                WeaponCaliber c => c.WeaponId,
                AmmunitionCaliber c => c.AmmunitionId,
                WeaponAttachmentClass c => c.WeaponAttachmentId,
                _ => (int?)null,
            })
            .Where(id => id is > 0)
            .ToHashSet();

        foreach (var entry in ChangeTracker.Entries<Item>())
        {
            var touched = entry.State is EntityState.Added or EntityState.Modified || touchedIds.Contains(entry.Entity.Id);
            if (!touched || entry.State == EntityState.Deleted) continue;

            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = now;
            entry.Entity.UpdatedAt = now;
            if (searchTextBuilder is not null) entry.Entity.SearchText = searchTextBuilder.Build(entry.Entity);
        }
    }
}
