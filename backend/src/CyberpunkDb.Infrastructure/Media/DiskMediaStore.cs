using CyberpunkDb.Application.Table;
using Microsoft.Extensions.Configuration;

namespace CyberpunkDb.Infrastructure.Media;

/// <summary>
/// Uploaded images as files named by their id, in Media:Root (%LOCALAPPDATA% style variables expand). Keeping them out
/// of the database keeps the SQLite file small and its backups fast.
/// </summary>
public sealed class DiskMediaStore(IConfiguration configuration) : IMediaStore
{
    private string Root
    {
        get
        {
            var configured = configuration["Media:Root"];
            var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(
                string.IsNullOrWhiteSpace(configured) ? Path.Combine(AppContext.BaseDirectory, "media") : configured));
            Directory.CreateDirectory(root);
            return root;
        }
    }

    public async Task SaveAsync(Guid id, Stream content, CancellationToken ct)
    {
        var path = PathOf(id);
        var temp = path + ".tmp";
        await using (var file = File.Create(temp)) await content.CopyToAsync(file, ct);
        File.Move(temp, path, overwrite: true);
    }

    public Stream? OpenRead(Guid id)
    {
        var path = PathOf(id);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true) : null;
    }

    public void Delete(Guid id)
    {
        try { File.Delete(PathOf(id)); } catch (IOException) { /* best effort */ }
    }

    private string PathOf(Guid id) => Path.Combine(Root, id.ToString("N"));
}
