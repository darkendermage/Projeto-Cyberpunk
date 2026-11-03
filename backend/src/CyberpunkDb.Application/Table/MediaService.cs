using CyberpunkDb.Application.Abstractions;
using CyberpunkDb.Application.Common;
using CyberpunkDb.Application.Localization;
using CyberpunkDb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CyberpunkDb.Application.Table;

/// <summary>Maps and token images of the virtual table. The browser shrinks big images before sending them.</summary>
public sealed class MediaService(IAppDbContext db, IMediaStore store)
{
    public const long MaxBytes = 15 * 1024 * 1024;

    public static string UrlOf(Guid id) => $"/api/table/media/{id:N}";

    public async Task<MediaDto> UploadAsync(Stream content, long length, CancellationToken ct)
    {
        if (length <= 0 || length > MaxBytes)
            throw RequestValidationException.For("file", L.T("A imagem deve ter até 15 MB.", "The image must be 15 MB or less."));
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var type = Sniff(buffer.GetBuffer().AsSpan(0, (int)buffer.Length))
                   ?? throw RequestValidationException.For("file", L.T("Use uma imagem PNG, JPEG, WebP ou GIF.", "Use a PNG, JPEG, WebP or GIF image."));

        var asset = new MediaAsset { Id = Guid.NewGuid(), ContentType = type, Length = buffer.Length, CreatedAt = DateTime.UtcNow };
        buffer.Position = 0;
        await store.SaveAsync(asset.Id, buffer, ct);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync(ct);
        return new MediaDto(asset.Id, UrlOf(asset.Id), asset.ContentType, asset.Length);
    }

    public async Task<(Stream Content, string ContentType)> OpenAsync(Guid id, CancellationToken ct)
    {
        var asset = await db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        var stream = asset is null ? null : store.OpenRead(id);
        return stream is null ? throw new NotFoundException(L.T("Imagem não encontrada.", "Image not found.")) : (stream, asset!.ContentType);
    }

    /// <summary>The image type read from the file's first bytes, never from what the browser claims.</summary>
    public static string? Sniff(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        if (b.Length >= 6 && (b[..6].SequenceEqual("GIF87a"u8) || b[..6].SequenceEqual("GIF89a"u8))) return "image/gif";
        return null;
    }
}
