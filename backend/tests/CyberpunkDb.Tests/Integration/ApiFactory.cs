using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>The players' port of the virtual table; requests carrying <see cref="PortHeader"/> arrive on it.</summary>
    public const int PlayerPort = 5091;
    public const string PortHeader = "X-Test-Local-Port";

    public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), $"cyberpunkdb-media-{Guid.NewGuid():N}");

    /// <summary>A client whose requests arrive on the players' port, as the tunnel's would.</summary>
    public HttpClient CreatePlayerClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(PortHeader, PlayerPort.ToString());
        return client;
    }

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
            ["Table:PlayerPort"] = PlayerPort.ToString(),
            ["Media:Root"] = MediaRoot,
        }));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, TestPortFilter>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (KeepDatabase) return;
        try { File.Delete(_dbPath); } catch (IOException) { /* best effort */ }
        try { Directory.Delete(MediaRoot, recursive: true); } catch (IOException) { /* best effort */ }
    }

    /// <summary>TestServer has no ports: this sets the local port from a header, before the app's own middleware.</summary>
    private sealed class TestPortFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (int.TryParse(context.Request.Headers[PortHeader], out var port)) context.Connection.LocalPort = port;
                await nextMiddleware(context);
            });
            next(app);
        };
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
