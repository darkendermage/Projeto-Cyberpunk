using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Table;
using CyberpunkDb.Infrastructure.Media;
using CyberpunkDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CyberpunkDb.Infrastructure;

public static class DependencyInjection
{
    // column types differ from Postgres, so SQLite has its own migrations
    public const string SqliteMigrationsAssembly = "CyberpunkDb.Migrations.Sqlite";

    // expands %LOCALAPPDATA% and creates the folder
    public static string SqliteConnectionString(string? configured)
    {
        var builder = new SqliteConnectionStringBuilder(configured ?? "Data Source=cyberpunk.db");
        builder.DataSource = Environment.ExpandEnvironmentVariables(builder.DataSource);
        var folder = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        return builder.ToString();
    }

    // Database:Provider = "Postgres" (migrations) or "Sqlite" (zero-setup local mode)
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // read when the context is built so hosts and tests can override it late
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var provider = config["Database:Provider"] ?? "Postgres";
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(SqliteConnectionString(config.GetConnectionString("Sqlite")),
                    sqlite => sqlite.MigrationsAssembly(SqliteMigrationsAssembly));
            }
            else if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
            {
                var connection = config.GetConnectionString("Postgres")
                                 ?? throw new InvalidOperationException("ConnectionStrings:Postgres não configurada.");
                options.UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            }
            else
            {
                throw new InvalidOperationException($"Database:Provider '{provider}' inválido. Use 'Postgres' ou 'Sqlite'.");
            }
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.TryAddSingleton<ISeedSource, FolderSeedSource>();
        services.Configure<SeedOptions>(configuration.GetSection("Seed"));
        services.AddScoped<DatabaseInitializer>();
        services.TryAddSingleton<IMediaStore, DiskMediaStore>();
        return services;
    }
}
