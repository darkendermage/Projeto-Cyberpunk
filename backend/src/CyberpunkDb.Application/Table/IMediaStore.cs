namespace CyberpunkDb.Application.Table;

// a folder on disk in the desktop app
public interface IMediaStore
{
    Task SaveAsync(Guid id, Stream content, CancellationToken ct);
    // null when the file is gone
    Stream? OpenRead(Guid id);
    void Delete(Guid id);
}
