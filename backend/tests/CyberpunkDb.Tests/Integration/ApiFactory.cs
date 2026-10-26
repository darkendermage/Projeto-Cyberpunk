using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CyberpunkDb.Tests.Integration;

/// <summary>
/// Boots the real API against a throw-away SQLite file seeded with the real seed files.
/// Shared by all integration tests (seeding happens once).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;

    public ApiFactory() : this(Path.Combine(Path.GetTempPath(), $"cyberpunkdb-tests-{Guid.NewGuid():N}.db")) { }

    /// <summary>Boots the API on an existing database file, as a restarted application would.</summary>
    internal ApiFactory(string dbPath) => _dbPath = dbPath;

    public string DbPath => _dbPath;

    /// <summary>Keeps the database file when this instance is disposed (used to simulate a restart).</summary>
    public bool KeepDatabase { get; init; }

    /// <summary>The tests read the messages in Portuguese, the language the texts were written in.</summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:Sqlite"] = $"Data Source={_dbPath};Pooling=False",
            ["Seed:Enabled"] = "true",
            ["Seed:Path"] = Path.Combine(AppContext.BaseDirectory, "Seed"),
        }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (KeepDatabase) return;
        try { File.Delete(_dbPath); } catch (IOException) { /* best effort */ }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
