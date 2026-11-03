namespace CyberpunkDb.Application.Table;

/// <summary>Where the bytes of uploaded maps and token images are kept (a folder on disk in the desktop app).</summary>
public interface IMediaStore
{
    Task SaveAsync(Guid id, Stream content, CancellationToken ct);
    /// <summary>Null when the file is gone.</summary>
    Stream? OpenRead(Guid id);
    void Delete(Guid id);
}
