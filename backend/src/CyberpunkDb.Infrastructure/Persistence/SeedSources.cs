using Microsoft.Extensions.Options;

namespace CyberpunkDb.Infrastructure.Persistence;

public sealed record SeedFile(string Name, Func<Stream> Open);

/// <summary>Where the seed catalog comes from: a folder on disk or resources embedded in the desktop executable.</summary>
public interface ISeedSource
{
    IReadOnlyList<SeedFile> GetFiles();
}

public sealed class FolderSeedSource(IOptions<SeedOptions> options) : ISeedSource
{
    public IReadOnlyList<SeedFile> GetFiles()
    {
        var path = Environment.ExpandEnvironmentVariables(options.Value.Path);
        var folder = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
        if (!Directory.Exists(folder)) return [];
        return Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal)
            .Select(f => new SeedFile(Path.GetFileName(f), () => File.OpenRead(f)))
            .ToList();
    }
}

/// <summary>Key/value rows used by the application itself (e.g. hash of the last imported seed).</summary>
public class AppMetadata
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
