using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Item> Items { get; }
    DbSet<Manufacturer> Manufacturers { get; }
    DbSet<SourceBook> SourceBooks { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<GearList> GearLists { get; }
    DbSet<GearListEntry> GearListEntries { get; }
    DbSet<Npc> Npcs { get; }
    DbSet<Character> Characters { get; }
    DbSet<CharacterEvent> CharacterEvents { get; }
    DbSet<CharacterPortrait> CharacterPortraits { get; }
    DbSet<NightMarket> NightMarkets { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Builds the normalized search text of an item. Called by the persistence layer on save.</summary>
public interface ISearchTextBuilder
{
    string Build(Item item);
}
