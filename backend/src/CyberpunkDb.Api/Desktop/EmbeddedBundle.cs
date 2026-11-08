using System.Reflection;
using CyberpunkDb.Infrastructure.Persistence;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace CyberpunkDb.Api.Desktop;

// Files embedded by the desktop build (DesktopBundle=true): frontend, seed catalog and appsettings.
// Outside that build there are no resources and everything comes from disk.
public static class EmbeddedBundle
{
    public const string WebRootPrefix = "bundle/wwwroot/";
    public const string SeedPrefix = "bundle/seed/";

    private static readonly Assembly Assembly = typeof(EmbeddedBundle).Assembly;

    // MSBuild writes "\" in names built from RecursiveDir on Windows
    private static readonly Dictionary<string, string> Names = Assembly.GetManifestResourceNames()
        .Where(n => n.Replace('\\', '/').StartsWith("bundle/", StringComparison.Ordinal))
        .ToDictionary(n => n.Replace('\\', '/'), n => n, StringComparer.OrdinalIgnoreCase);

    public static bool HasWebRoot => Names.ContainsKey(WebRootPrefix + "index.html");
    public static bool HasSeed => Names.Keys.Any(n => n.StartsWith(SeedPrefix, StringComparison.OrdinalIgnoreCase));
    public static IEnumerable<string> All => Names.Keys;

    public static Stream? Open(string name) =>
        Names.TryGetValue(name.Replace('\\', '/'), out var actual) ? Assembly.GetManifestResourceStream(actual) : null;
}

public sealed class EmbeddedSeedSource : ISeedSource
{
    public IReadOnlyList<SeedFile> GetFiles() => EmbeddedBundle.All
        .Where(n => n.StartsWith(EmbeddedBundle.SeedPrefix, StringComparison.OrdinalIgnoreCase))
        .Order(StringComparer.Ordinal)
        .Select(n => new SeedFile(n[EmbeddedBundle.SeedPrefix.Length..], () => EmbeddedBundle.Open(n)!))
        .ToList();
}

// serves the embedded frontend as wwwroot
public sealed class EmbeddedWebRootProvider : IFileProvider
{
    private static readonly DateTimeOffset BuildTime = File.GetLastWriteTimeUtc(Environment.ProcessPath ?? AppContext.BaseDirectory);

    public IFileInfo GetFileInfo(string subpath)
    {
        var path = EmbeddedBundle.WebRootPrefix + subpath.TrimStart('/');
        using var probe = EmbeddedBundle.Open(path);
        return probe is null ? new NotFoundFileInfo(subpath) : new ResourceFileInfo(path, Path.GetFileName(path), probe.Length);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        var prefix = EmbeddedBundle.WebRootPrefix + subpath.Trim('/');
        if (!prefix.EndsWith('/')) prefix += "/";

        var entries = new Dictionary<string, IFileInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in EmbeddedBundle.All.Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var rest = name[prefix.Length..];
            var slash = rest.IndexOf('/');
            if (slash >= 0)
            {
                entries.TryAdd(rest[..slash], new ResourceDirectoryInfo(rest[..slash]));
            }
            else
            {
                using var stream = EmbeddedBundle.Open(name)!;
                entries[rest] = new ResourceFileInfo(name, rest, stream.Length);
            }
        }
        return entries.Count == 0 ? NotFoundDirectoryContents.Singleton : new Contents(entries.Values.ToList());
    }

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    private sealed class ResourceFileInfo(string resource, string name, long length) : IFileInfo
    {
        public bool Exists => true;
        public long Length => length;
        public string? PhysicalPath => null;
        public string Name => name;
        public DateTimeOffset LastModified => BuildTime;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => EmbeddedBundle.Open(resource)!;
    }

    private sealed class ResourceDirectoryInfo(string name) : IFileInfo
    {
        public bool Exists => true;
        public long Length => -1;
        public string? PhysicalPath => null;
        public string Name => name;
        public DateTimeOffset LastModified => BuildTime;
        public bool IsDirectory => true;
        public Stream CreateReadStream() => throw new InvalidOperationException("É uma pasta.");
    }

    private sealed class Contents(IReadOnlyList<IFileInfo> items) : IDirectoryContents
    {
        public bool Exists => true;
        public IEnumerator<IFileInfo> GetEnumerator() => items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
