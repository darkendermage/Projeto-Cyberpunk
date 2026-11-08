using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CyberpunkDb.Infrastructure.Persistence;

// For "dotnet ef". Postgres by default; pass "-- --provider Sqlite" for the SQLite migrations. Never connects.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var sqlite = args.SkipWhile(a => a != "--provider").Skip(1).FirstOrDefault()?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlite)
            builder.UseSqlite("Data Source=design.db", o => o.MigrationsAssembly(DependencyInjection.SqliteMigrationsAssembly));
        else
            builder.UseNpgsql(Environment.GetEnvironmentVariable("CYBERPUNKDB_POSTGRES")
                              ?? "Host=localhost;Port=5432;Database=cyberpunkdb;Username=cyberpunk;Password=cyberpunk");
        return new AppDbContext(builder.Options);
    }
}
